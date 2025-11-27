namespace ZakYip.WheelDiverterSorter.Execution.Abstractions;

/// <summary>
/// 上游路由通讯客户端接口
/// </summary>
/// <remarks>
/// 抽象了与上游系统（RuleEngine等）的通信，隐藏底层协议（TCP/HTTP/SignalR/MQTT）细节。
/// 
/// <para><b>职责</b>：</para>
/// <list type="bullet">
///   <item>连接/断开上游系统</item>
///   <item>通知上游包裹到达并等待格口分配</item>
///   <item>接收上游推送的格口分配</item>
/// </list>
/// 
/// <para><b>实现层</b>：</para>
/// Communication 项目实现此接口，内部使用 IRuleEngineClient 等具体协议实现。
/// </remarks>
public interface IUpstreamRoutingClient
{
    /// <summary>
    /// 是否已连接到上游系统
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// 格口分配接收事件
    /// </summary>
    /// <remarks>
    /// 当上游系统推送格口分配时触发此事件。
    /// 事件参数包含包裹ID和分配的格口ID。
    /// </remarks>
    event EventHandler<ChuteAssignmentEventArgs>? ChuteAssignmentReceived;

    /// <summary>
    /// 连接到上游系统
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否连接成功</returns>
    Task<bool> ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 断开与上游系统的连接
    /// </summary>
    /// <returns>异步任务</returns>
    Task DisconnectAsync();

    /// <summary>
    /// 通知上游系统包裹已到达
    /// </summary>
    /// <param name="parcelId">包裹ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功发送通知</returns>
    /// <remarks>
    /// 此方法仅发送通知，不等待格口分配响应。
    /// 格口分配将通过 ChuteAssignmentReceived 事件异步推送。
    /// </remarks>
    Task<bool> NotifyParcelDetectedAsync(long parcelId, CancellationToken cancellationToken = default);
}

/// <summary>
/// 格口分配事件参数
/// </summary>
/// <remarks>
/// 纯数据对象，不依赖具体的 Communication 层类型。
/// </remarks>
public record ChuteAssignmentEventArgs
{
    /// <summary>
    /// 包裹ID
    /// </summary>
    public required long ParcelId { get; init; }

    /// <summary>
    /// 分配的格口ID
    /// </summary>
    public required long ChuteId { get; init; }

    /// <summary>
    /// 通知时间
    /// </summary>
    public required DateTimeOffset NotificationTime { get; init; }

    /// <summary>
    /// 额外的元数据（可选）
    /// </summary>
    public Dictionary<string, string>? Metadata { get; init; }
}
