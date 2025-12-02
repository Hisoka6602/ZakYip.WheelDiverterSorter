using Microsoft.Extensions.Caching.Memory;

namespace ZakYip.WheelDiverterSorter.Application.Services.Caching;

/// <summary>
/// 基于 IMemoryCache 的滑动过期配置缓存实现。
/// </summary>
/// <remarks>
/// 统一所有配置的缓存策略：
/// - 滑动过期时间：1 小时
/// - 无绝对过期时间（满足"无上限绝对时间"的要求）
/// - 高优先级，减少内存压力时被淘汰的概率
/// 
/// 性能优化：
/// - 命中缓存时直接返回 Task.FromResult，避免额外分配
/// - 使用 static readonly object 作为 cacheKey，避免字符串拼接
/// </remarks>
internal sealed class SlidingConfigCache : ISlidingConfigCache
{
    /// <summary>
    /// 滑动过期时间：1 小时
    /// </summary>
    private static readonly TimeSpan SlidingExpiration = TimeSpan.FromHours(1);

    private readonly IMemoryCache _memoryCache;

    /// <summary>
    /// 初始化滑动配置缓存
    /// </summary>
    /// <param name="memoryCache">内存缓存实例</param>
    public SlidingConfigCache(IMemoryCache memoryCache)
    {
        _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
    }

    /// <inheritdoc />
    public Task<TConfig> GetOrAddAsync<TConfig>(
        object cacheKey,
        Func<CancellationToken, Task<TConfig>> factory,
        CancellationToken cancellationToken = default)
        where TConfig : class
    {
        ArgumentNullException.ThrowIfNull(cacheKey);
        ArgumentNullException.ThrowIfNull(factory);

        if (_memoryCache.TryGetValue(cacheKey, out TConfig? cached) && cached is not null)
        {
            // 已命中缓存，直接返回 Task.FromResult，避免额外分配
            return Task.FromResult(cached);
        }

        return GetOrAddInternalAsync(cacheKey, factory, cancellationToken);
    }

    /// <inheritdoc />
    public TConfig GetOrAdd<TConfig>(object cacheKey, Func<TConfig> factory)
        where TConfig : class
    {
        ArgumentNullException.ThrowIfNull(cacheKey);
        ArgumentNullException.ThrowIfNull(factory);

        return _memoryCache.GetOrCreate(cacheKey, entry =>
        {
            entry.SlidingExpiration = SlidingExpiration;
            entry.Priority = CacheItemPriority.High;
            return factory();
        })!;
    }

    /// <inheritdoc />
    public void Set<TConfig>(object cacheKey, TConfig value)
        where TConfig : class
    {
        ArgumentNullException.ThrowIfNull(cacheKey);
        ArgumentNullException.ThrowIfNull(value);

        var options = new MemoryCacheEntryOptions
        {
            SlidingExpiration = SlidingExpiration,
            Priority = CacheItemPriority.High
            // 不设置 AbsoluteExpiration，满足"无上限绝对时间"的要求
        };

        _memoryCache.Set(cacheKey, value, options);
    }

    /// <inheritdoc />
    public void Remove(object cacheKey)
    {
        ArgumentNullException.ThrowIfNull(cacheKey);
        _memoryCache.Remove(cacheKey);
    }

    /// <inheritdoc />
    public bool TryGetValue<TConfig>(object cacheKey, out TConfig? value)
        where TConfig : class
    {
        ArgumentNullException.ThrowIfNull(cacheKey);
        return _memoryCache.TryGetValue(cacheKey, out value);
    }

    private async Task<TConfig> GetOrAddInternalAsync<TConfig>(
        object cacheKey,
        Func<CancellationToken, Task<TConfig>> factory,
        CancellationToken cancellationToken)
        where TConfig : class
    {
        var value = await factory(cancellationToken).ConfigureAwait(false);

        var options = new MemoryCacheEntryOptions
        {
            SlidingExpiration = SlidingExpiration,
            Priority = CacheItemPriority.High
            // 不设置 AbsoluteExpiration，满足"无上限绝对时间"的要求
        };

        _memoryCache.Set(cacheKey, value, options);
        return value;
    }
}
