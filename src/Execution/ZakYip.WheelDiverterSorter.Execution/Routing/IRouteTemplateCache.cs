using System.Collections.Concurrent;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;

namespace ZakYip.WheelDiverterSorter.Execution.Routing;

/// <summary>
/// 路由模板缓存服务接口
/// Interface for route template caching service
/// </summary>
public interface IRouteTemplateCache
{
    /// <summary>
    /// 尝试从缓存中获取路由配置
    /// Tries to get route configuration from cache
    /// </summary>
    /// <param name="chuteId">格口ID</param>
    /// <param name="configuration">路由配置</param>
    /// <returns>如果缓存命中返回true，否则返回false</returns>
    bool TryGet(long chuteId, out ChuteRouteConfiguration? configuration);

    /// <summary>
    /// 将路由配置添加到缓存
    /// Adds route configuration to cache
    /// </summary>
    /// <param name="chuteId">格口ID</param>
    /// <param name="configuration">路由配置</param>
    void Add(long chuteId, ChuteRouteConfiguration configuration);

    /// <summary>
    /// 从缓存中移除指定格口的路由配置
    /// Removes route configuration from cache for specified chute
    /// </summary>
    /// <param name="chuteId">格口ID</param>
    /// <returns>如果成功移除返回true，否则返回false</returns>
    bool Remove(long chuteId);

    /// <summary>
    /// 清空缓存
    /// Clears the cache
    /// </summary>
    void Clear();

    /// <summary>
    /// 获取缓存中的条目数量
    /// Gets the number of cached entries
    /// </summary>
    int Count { get; }
}

/// <summary>
/// 默认的路由模板缓存实现
/// Default implementation of route template cache
/// </summary>
public class DefaultRouteTemplateCache : IRouteTemplateCache
{
    private readonly ConcurrentDictionary<long, ChuteRouteConfiguration> _cache;

    /// <summary>
    /// 初始化路由模板缓存
    /// Initializes route template cache
    /// </summary>
    public DefaultRouteTemplateCache()
    {
        _cache = new ConcurrentDictionary<long, ChuteRouteConfiguration>();
    }

    /// <inheritdoc/>
    public bool TryGet(long chuteId, out ChuteRouteConfiguration? configuration)
    {
        return _cache.TryGetValue(chuteId, out configuration);
    }

    /// <inheritdoc/>
    public void Add(long chuteId, ChuteRouteConfiguration configuration)
    {
        _cache[chuteId] = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    /// <inheritdoc/>
    public bool Remove(long chuteId)
    {
        return _cache.TryRemove(chuteId, out _);
    }

    /// <inheritdoc/>
    public void Clear()
    {
        _cache.Clear();
    }

    /// <inheritdoc/>
    public int Count => _cache.Count;
}
