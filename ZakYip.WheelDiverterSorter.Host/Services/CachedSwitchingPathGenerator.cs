using ZakYip.WheelDiverterSorter.Core;
using Microsoft.Extensions.Caching.Memory;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;

namespace ZakYip.WheelDiverterSorter.Host.Services;

/// <summary>
/// 带缓存优化的路径生成器包装器
/// </summary>
public class CachedSwitchingPathGenerator : ISwitchingPathGenerator {
    private readonly ISwitchingPathGenerator _innerGenerator;
    private readonly IMemoryCache _cache;
    private readonly ILogger<CachedSwitchingPathGenerator> _logger;
    private const int CacheDurationMinutes = 5;

    public CachedSwitchingPathGenerator(
        ISwitchingPathGenerator innerGenerator,
        IMemoryCache cache,
        ILogger<CachedSwitchingPathGenerator> logger) {
        _innerGenerator = innerGenerator;
        _cache = cache;
        _logger = logger;
    }

    public SwitchingPath? GeneratePath(int targetChuteId)
    {
        if (targetChuteId <= 0)
        {
            return null;
        }

        var cacheKey = $"path_{targetChuteId}";

        // 尝试从缓存中获取
        if (_cache.TryGetValue<SwitchingPath?>(cacheKey, out var cachedPath) && cachedPath != null) {
            _logger.LogDebug("路径缓存命中: {ChuteId}", targetChuteId);

            // 返回缓存的路径，更新生成时间
            return cachedPath with { GeneratedAt = DateTimeOffset.UtcNow };
        }

        // 缓存未命中，生成新路径
        _logger.LogDebug("路径缓存未命中: {ChuteId}", targetChuteId);
        var path = _innerGenerator.GeneratePath(targetChuteId);

        // 将路径放入缓存（只缓存成功的路径）
        if (path != null) {
            var cacheOptions = new MemoryCacheEntryOptions {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(CacheDurationMinutes),
                Size = 1 // 用于LRU淘汰
            };

            _cache.Set(cacheKey, path, cacheOptions);
            _logger.LogDebug("路径已缓存: {ChuteId}, 过期时间: {Minutes}分钟", targetChuteId, CacheDurationMinutes);
        }

        return path;
    }

    /// <summary>
    /// 清除指定格口的缓存
    /// </summary>
    public void InvalidateCache(int targetChuteId)
    {
        var cacheKey = $"path_{targetChuteId}";
        _cache.Remove(cacheKey);
        _logger.LogInformation("已清除格口缓存: {ChuteId}", targetChuteId);
    }

    /// <summary>
    /// 清除所有缓存
    /// </summary>
    public void InvalidateAllCache() {
        if (_cache is MemoryCache memoryCache) {
            memoryCache.Compact(1.0); // 清除所有缓存
            _logger.LogInformation("已清除所有路径缓存");
        }
    }
}