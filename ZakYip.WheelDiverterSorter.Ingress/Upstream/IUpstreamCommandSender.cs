namespace ZakYip.WheelDiverterSorter.Ingress.Upstream;

/// <summary>
/// 上游命令发送器接口，负责发送请求/命令并接收响应
/// </summary>
public interface IUpstreamCommandSender
{
    /// <summary>
    /// 发送命令到上游系统
    /// </summary>
    /// <typeparam name="TRequest">请求类型</typeparam>
    /// <typeparam name="TResponse">响应类型</typeparam>
    /// <param name="request">请求对象</param>
    /// <param name="timeout">超时时间</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>响应对象</returns>
    Task<TResponse> SendCommandAsync<TRequest, TResponse>(
        TRequest request,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
        where TRequest : class
        where TResponse : class;

    /// <summary>
    /// 发送单向命令到上游系统（无需等待响应）
    /// </summary>
    /// <typeparam name="TRequest">请求类型</typeparam>
    /// <param name="request">请求对象</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>发送任务</returns>
    Task SendOneWayAsync<TRequest>(
        TRequest request,
        CancellationToken cancellationToken = default)
        where TRequest : class;
}
