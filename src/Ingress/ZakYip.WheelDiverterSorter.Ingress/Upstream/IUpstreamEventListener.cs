namespace ZakYip.WheelDiverterSorter.Ingress.Upstream;

/// <summary>
/// 上游事件监听器接口，用于订阅上游事件（如配置变更通知）
/// </summary>
public interface IUpstreamEventListener
{
    /// <summary>
    /// 订阅上游事件
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="eventName">事件名称</param>
    /// <param name="handler">事件处理器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>订阅任务</returns>
    Task SubscribeAsync<TEvent>(
        string eventName,
        Func<TEvent, Task> handler,
        CancellationToken cancellationToken = default)
        where TEvent : class;

    /// <summary>
    /// 取消订阅上游事件
    /// </summary>
    /// <param name="eventName">事件名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>取消订阅任务</returns>
    Task UnsubscribeAsync(
        string eventName,
        CancellationToken cancellationToken = default);
}
