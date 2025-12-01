using Microsoft.Extensions.Caching.Memory;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;
using ZakYip.WheelDiverterSorter.Configuration.Persistence.Repositories.LiteDb;

namespace ZakYip.WheelDiverterSorter.Application.Services.Caching;

/// <summary>
/// 带内存缓存的感应IO配置仓储包装器
/// </summary>
/// <remarks>
/// 使用滑动过期策略，无绝对过期时间，减少数据库访问。
/// 当配置热更新时自动更新缓存。
/// </remarks>
public class CachedSensorConfigurationRepository : ISensorConfigurationRepository
{
    private readonly ISensorConfigurationRepository _innerRepository;
    private readonly IMemoryCache _cache;
    private const string CacheKey = "SensorConfiguration";
    private static readonly TimeSpan SlidingExpiration = TimeSpan.FromMinutes(30);

    /// <summary>
    /// 初始化带缓存的感应IO配置仓储
    /// </summary>
    /// <param name="innerRepository">内部仓储实现（LiteDB仓储）</param>
    /// <param name="cache">内存缓存</param>
    public CachedSensorConfigurationRepository(
        LiteDbSensorConfigurationRepository innerRepository,
        IMemoryCache cache)
    {
        _innerRepository = innerRepository ?? throw new ArgumentNullException(nameof(innerRepository));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    /// <summary>
    /// 获取感应IO配置（优先从缓存获取）
    /// </summary>
    public SensorConfiguration Get()
    {
        return _cache.GetOrCreate(CacheKey, entry =>
        {
            entry.SlidingExpiration = SlidingExpiration;
            entry.Priority = CacheItemPriority.High;
            return _innerRepository.Get();
        })!;
    }

    /// <summary>
    /// 更新感应IO配置（同时更新缓存）
    /// </summary>
    public void Update(SensorConfiguration configuration)
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
    public void InitializeDefault()
    {
        _innerRepository.InitializeDefault();
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
