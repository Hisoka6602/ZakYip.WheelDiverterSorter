using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.LineModel.Topology;
using ZakYip.WheelDiverterSorter.Core.Utilities;

namespace ZakYip.WheelDiverterSorter.Application.Services.Caching;

/// <summary>
/// 路径缓存管理接口
/// </summary>
/// <remarks>
/// 用于在拓扑配置变更时清除路径缓存
/// </remarks>
public interface IPathCacheManager
{
    /// <summary>
    /// 清除指定格口的路径缓存
    /// </summary>
    /// <param name="targetChuteId">目标格口ID</param>
    void InvalidateCache(long targetChuteId);

    /// <summary>
    /// 清除所有路径缓存
    /// </summary>
    /// <param name="knownChuteIds">已知的格口ID列表，用于逐个清除</param>
    void InvalidateAllCache(IEnumerable<long>? knownChuteIds = null);
}

/// <summary>
/// 带缓存优化的路径生成器包装器
/// </summary>
/// <remarks>
/// 使用统一的 ISlidingConfigCache，采用 1 小时滑动过期策略。
/// 当拓扑配置变更时，应调用 InvalidateCache 清除相关缓存。
/// 
/// 性能优化：
/// - 使用对象池化的缓存键，避免频繁创建对象
/// - 缓存键按格口ID池化，重复使用同一对象
/// </remarks>
public class CachedSwitchingPathGenerator : ISwitchingPathGenerator, IPathCacheManager
{
    /// <summary>
    /// 路径缓存键，使用池化避免频繁创建对象
    /// </summary>
    private sealed class PathCacheKey : IEquatable<PathCacheKey>
    {
        public long ChuteId { get; }

        public PathCacheKey(long chuteId)
        {
            ChuteId = chuteId;
        }

        public bool Equals(PathCacheKey? other)
        {
            return other is not null && ChuteId == other.ChuteId;
        }

        public override bool Equals(object? obj)
        {
            return obj is PathCacheKey other && ChuteId == other.ChuteId;
        }

        public override int GetHashCode()
        {
            return ChuteId.GetHashCode();
        }
    }

    /// <summary>
    /// 缓存键对象池，按格口ID池化缓存键对象
    /// </summary>
    private static readonly ConcurrentDictionary<long, PathCacheKey> CacheKeyPool = new();

    private readonly ISwitchingPathGenerator _innerGenerator;
    private readonly ISlidingConfigCache _configCache;
    private readonly ISystemClock _clock;
    private readonly ILogger<CachedSwitchingPathGenerator> _logger;

    public CachedSwitchingPathGenerator(
        ISwitchingPathGenerator innerGenerator,
        ISlidingConfigCache configCache,
        ISystemClock clock,
        ILogger<CachedSwitchingPathGenerator> logger)
    {
        _innerGenerator = innerGenerator ?? throw new ArgumentNullException(nameof(innerGenerator));
        _configCache = configCache ?? throw new ArgumentNullException(nameof(configCache));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 获取或创建池化的缓存键
    /// </summary>
    private static PathCacheKey GetOrCreateCacheKey(long chuteId)
    {
        return CacheKeyPool.GetOrAdd(chuteId, id => new PathCacheKey(id));
    }

    public SwitchingPath? GeneratePath(long targetChuteId)
    {
        if (targetChuteId <= 0)
        {
            return null;
        }

        // 使用池化的缓存键，避免频繁创建对象
        var cacheKey = GetOrCreateCacheKey(targetChuteId);

        // 尝试从缓存中获取
        if (_configCache.TryGetValue<SwitchingPath>(cacheKey, out var cachedPath) && cachedPath != null)
        {
            _logger.LogDebug("路径缓存命中: {ChuteId}", targetChuteId);

            // 返回缓存的路径，更新生成时间
            return cachedPath with { GeneratedAt = new DateTimeOffset(_clock.LocalNow) };
        }

        // 缓存未命中，生成新路径
        _logger.LogDebug("路径缓存未命中: {ChuteId}", targetChuteId);
        var path = _innerGenerator.GeneratePath(targetChuteId);

        // 将路径放入缓存（只缓存成功的路径）
        if (path != null)
        {
            _configCache.Set(cacheKey, path);
            _logger.LogDebug("路径已缓存（1小时滑动过期）: {ChuteId}", targetChuteId);
        }

        return path;
    }

    /// <inheritdoc />
    public void InvalidateCache(long targetChuteId)
    {
        var cacheKey = GetOrCreateCacheKey(targetChuteId);
        _configCache.Remove(cacheKey);
        _logger.LogInformation("已清除格口路径缓存: {ChuteId}", targetChuteId);
    }

    /// <inheritdoc />
    public void InvalidateAllCache(IEnumerable<long>? knownChuteIds = null)
    {
        if (knownChuteIds != null)
        {
            var count = 0;
            foreach (var chuteId in knownChuteIds)
            {
                var cacheKey = GetOrCreateCacheKey(chuteId);
                _configCache.Remove(cacheKey);
                count++;
            }
            _logger.LogInformation("已清除 {Count} 个格口的路径缓存", count);
        }
        else
        {
            _logger.LogWarning("InvalidateAllCache 被调用但未提供格口ID列表，无法清除缓存");
        }
    }
}
