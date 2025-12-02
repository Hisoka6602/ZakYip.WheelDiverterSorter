namespace ZakYip.WheelDiverterSorter.Application.Services.Caching;

/// <summary>
/// 统一滑动过期配置缓存接口
/// </summary>
/// <remarks>
/// 采用 1 小时滑动过期策略，无绝对过期时间。
/// 所有配置服务统一使用此接口，避免缓存实现"影分身"。
/// 
/// 热更新语义：
/// - 读取：命中缓存时只有一次字典查找 + 对象引用读取
/// - 更新：先写持久化，再 Set 缓存，保证热更新"立即生效"
/// </remarks>
public interface ISlidingConfigCache
{
    /// <summary>
    /// 获取或添加配置缓存，采用 1 小时滑动过期策略。
    /// </summary>
    /// <typeparam name="TConfig">配置类型</typeparam>
    /// <param name="cacheKey">缓存键（建议使用 static readonly object 以提高性能）</param>
    /// <param name="factory">缓存未命中时的数据加载工厂</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>配置对象</returns>
    Task<TConfig> GetOrAddAsync<TConfig>(
        object cacheKey,
        Func<CancellationToken, Task<TConfig>> factory,
        CancellationToken cancellationToken = default)
        where TConfig : class;

    /// <summary>
    /// 同步获取或添加配置缓存
    /// </summary>
    /// <typeparam name="TConfig">配置类型</typeparam>
    /// <param name="cacheKey">缓存键</param>
    /// <param name="factory">缓存未命中时的数据加载工厂</param>
    /// <returns>配置对象</returns>
    TConfig GetOrAdd<TConfig>(object cacheKey, Func<TConfig> factory)
        where TConfig : class;

    /// <summary>
    /// 主动刷新配置缓存（用于热更新场景）。
    /// </summary>
    /// <typeparam name="TConfig">配置类型</typeparam>
    /// <param name="cacheKey">缓存键</param>
    /// <param name="value">新配置值</param>
    void Set<TConfig>(object cacheKey, TConfig value)
        where TConfig : class;

    /// <summary>
    /// 主动清理缓存。
    /// </summary>
    /// <param name="cacheKey">缓存键</param>
    void Remove(object cacheKey);

    /// <summary>
    /// 尝试获取缓存值
    /// </summary>
    /// <typeparam name="TConfig">配置类型</typeparam>
    /// <param name="cacheKey">缓存键</param>
    /// <param name="value">输出的配置值</param>
    /// <returns>是否命中缓存</returns>
    bool TryGetValue<TConfig>(object cacheKey, out TConfig? value)
        where TConfig : class;
}
