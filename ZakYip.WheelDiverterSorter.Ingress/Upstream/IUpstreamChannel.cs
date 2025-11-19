namespace ZakYip.WheelDiverterSorter.Ingress.Upstream;

/// <summary>
/// 上游通道接口，定义与上游规则引擎的连接生命周期
/// </summary>
public interface IUpstreamChannel : IDisposable
{
    /// <summary>
    /// 通道名称
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 通道类型（HTTP, SignalR, MQTT等）
    /// </summary>
    string ChannelType { get; }

    /// <summary>
    /// 是否已连接
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// 连接到上游系统
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>连接任务</returns>
    Task ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 断开与上游系统的连接
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>断开任务</returns>
    Task DisconnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 检查连接健康状态
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>健康检查任务，返回是否健康</returns>
    Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default);
}
