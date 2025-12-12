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
/// - 使用 readonly record struct 作为缓存键，避免频繁创建堆对象
/// - 缓存键是值类型，不会造成内存泄漏
/// </remarks>
public class CachedSwitchingPathGenerator : ISwitchingPathGenerator, IPathCacheManager
{
    /// <summary>
    /// 路径缓存键（值类型，避免堆分配）
    /// </summary>
    private readonly record struct PathCacheKey(long ChuteId);

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

    public SwitchingPath? GeneratePath(long targetChuteId)
    {
        if (targetChuteId <= 0)
        {
            return null;
        }

        // 使用值类型缓存键，避免堆分配
        var cacheKey = new PathCacheKey(targetChuteId);

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
        var cacheKey = new PathCacheKey(targetChuteId);
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
                var cacheKey = new PathCacheKey(chuteId);
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

    /// <summary>
    /// 生成队列任务列表（直接委托给底层生成器，不缓存）
    /// </summary>
    /// <param name="parcelId">包裹ID</param>
    /// <param name="targetChuteId">目标格口ID</param>
    /// <param name="createdAt">创建时间</param>
    /// <returns>队列任务列表</returns>
    /// <remarks>
    /// 队列任务包含时间戳，每次都不同，因此不适合缓存。
    /// 直接委托给底层生成器。
    /// </remarks>
    public List<Core.Abstractions.Execution.PositionQueueItem> GenerateQueueTasks(
        long parcelId,
        long targetChuteId,
        DateTime createdAt)
    {
        // 队列任务包含时间戳，不适合缓存，直接委托
        return _innerGenerator.GenerateQueueTasks(parcelId, targetChuteId, createdAt);
    }
}
