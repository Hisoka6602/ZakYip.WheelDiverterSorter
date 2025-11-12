namespace ZakYip.WheelDiverterSorter.Communication.Abstractions;

/// <summary>
/// 规则引擎通信客户端接口
/// </summary>
/// <remarks>
/// 定义与RuleEngine通信的标准接口，支持多种通信协议实现。
/// 使用推送模型：系统通知RuleEngine包裹到达，然后等待RuleEngine推送格口分配。
/// </remarks>
public interface IRuleEngineClient : IDisposable
{
    /// <summary>
    /// 客户端是否已连接
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// 格口分配通知事件
    /// </summary>
    /// <remarks>
    /// 当RuleEngine推送格口分配时触发此事件
    /// </remarks>
    event EventHandler<ChuteAssignmentNotificationEventArgs>? ChuteAssignmentReceived;

    /// <summary>
    /// 连接到RuleEngine
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否连接成功</returns>
    Task<bool> ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 断开与RuleEngine的连接
    /// </summary>
    /// <returns>异步任务</returns>
    Task DisconnectAsync();

    /// <summary>
    /// 通知RuleEngine包裹已到达
    /// </summary>
    /// <param name="parcelId">包裹ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功发送通知</returns>
    /// <remarks>
    /// 此方法不等待响应，仅发送通知。格口分配将通过ChuteAssignmentReceived事件推送。
    /// </remarks>
    Task<bool> NotifyParcelDetectedAsync(
        string parcelId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 请求包裹的格口号（保留用于兼容性，不推荐使用）
    /// </summary>
    /// <param name="parcelId">包裹ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>格口分配响应</returns>
    /// <remarks>
    /// ⚠️ 已废弃：此方法使用请求/响应模型，不符合新的推送模型架构。
    /// 请使用NotifyParcelDetectedAsync配合ChuteAssignmentReceived事件。
    /// </remarks>
    [Obsolete("使用NotifyParcelDetectedAsync配合ChuteAssignmentReceived事件代替")]
    Task<ChuteAssignmentResponse> RequestChuteAssignmentAsync(
        string parcelId,
        CancellationToken cancellationToken = default);
}
