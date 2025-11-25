using Microsoft.Extensions.Caching.Memory;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;

namespace ZakYip.WheelDiverterSorter.Host.Application.Services;

/// <summary>
/// 带内存缓存的线体拓扑配置仓储包装器
/// </summary>
/// <remarks>
/// 使用滑动过期策略，无绝对过期时间，减少数据库访问。
/// 当配置热更新时自动更新缓存。
/// </remarks>
public class CachedLineTopologyRepository : ILineTopologyRepository
{
    private readonly ILineTopologyRepository _innerRepository;
    private readonly IMemoryCache _cache;
    private const string CacheKey = "LineTopologyConfiguration";
    private static readonly TimeSpan SlidingExpiration = TimeSpan.FromMinutes(30);

    /// <summary>
    /// 初始化带缓存的线体拓扑配置仓储
    /// </summary>
    /// <param name="innerRepository">内部仓储实现（LiteDB仓储）</param>
    /// <param name="cache">内存缓存</param>
    public CachedLineTopologyRepository(
        LiteDbLineTopologyRepository innerRepository,
        IMemoryCache cache)
    {
        _innerRepository = innerRepository ?? throw new ArgumentNullException(nameof(innerRepository));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    /// <summary>
    /// 获取线体拓扑配置（优先从缓存获取）
    /// </summary>
    public LineTopologyConfig Get()
    {
        return _cache.GetOrCreate(CacheKey, entry =>
        {
            entry.SlidingExpiration = SlidingExpiration;
            entry.Priority = CacheItemPriority.High;
            return _innerRepository.Get();
        })!;
    }

    /// <summary>
    /// 更新线体拓扑配置（同时更新缓存）
    /// </summary>
    public void Update(LineTopologyConfig configuration)
    {
        _innerRepository.Update(configuration);
        
        // 热更新：更新缓存
        var cacheOptions = new MemoryCacheEntryOptions
        {
            SlidingExpiration = SlidingExpiration,
            Priority = CacheItemPriority.High
        };
        _cache.Set(CacheKey, configuration, cacheOptions);
    }

    /// <summary>
    /// 初始化默认配置
    /// </summary>
    public void InitializeDefault(DateTime? currentTime = null)
    {
        _innerRepository.InitializeDefault(currentTime);
        // 清除缓存，下次Get时重新加载
        _cache.Remove(CacheKey);
    }

    /// <summary>
    /// 清除缓存
    /// </summary>
    public void InvalidateCache()
    {
        _cache.Remove(CacheKey);
    }
}
