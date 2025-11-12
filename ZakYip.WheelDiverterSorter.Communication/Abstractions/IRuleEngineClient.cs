namespace ZakYip.WheelDiverterSorter.Communication.Abstractions;

/// <summary>
/// 规则引擎通信客户端接口
/// </summary>
/// <remarks>
/// 定义与RuleEngine通信的标准接口，支持多种通信协议实现
/// </remarks>
public interface IRuleEngineClient : IDisposable
{
    /// <summary>
    /// 客户端是否已连接
    /// </summary>
    bool IsConnected { get; }

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
    /// 请求包裹的格口号
    /// </summary>
    /// <param name="parcelId">包裹ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>格口分配响应</returns>
    Task<ChuteAssignmentResponse> RequestChuteAssignmentAsync(
        string parcelId,
        CancellationToken cancellationToken = default);
}
