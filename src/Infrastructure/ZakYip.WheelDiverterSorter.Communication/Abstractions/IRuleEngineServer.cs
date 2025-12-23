namespace ZakYip.WheelDiverterSorter.Communication.Abstractions;

/// <summary>
/// RuleEngine服务器接口
/// RuleEngine server interface
/// </summary>
/// <remarks>
/// 定义服务器模式下的监听、客户端管理和消息分发功能
/// Defines listening, client management, and message dispatch functionality in server mode
/// </remarks>
public interface IRuleEngineServer : IDisposable
{
    /// <summary>
    /// 服务器是否正在运行
    /// Whether the server is running
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// 当前连接的客户端数量
    /// Number of currently connected clients
    /// </summary>
    int ConnectedClientsCount { get; }

    /// <summary>
    /// 获取所有已连接的客户端信息
    /// Get all connected clients information
    /// </summary>
    /// <returns>已连接客户端的信息列表 / List of connected client information</returns>
    IReadOnlyList<ClientConnectionEventArgs> GetConnectedClients();

    /// <summary>
    /// 启动服务器
    /// Start the server
    /// </summary>
    /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 停止服务器
    /// Stop the server
    /// </summary>
    /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 向所有连接的客户端广播格口分配通知
    /// Broadcast chute assignment notification to all connected clients
    /// </summary>
    /// <param name="parcelId">包裹ID / Parcel ID</param>
    /// <param name="chuteId">格口ID / Chute ID</param>
    /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
    Task BroadcastChuteAssignmentAsync(
        long parcelId, 
        string chuteId, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 向所有连接的客户端广播包裹检测通知
    /// Broadcast parcel detected notification to all connected clients
    /// </summary>
    /// <param name="parcelId">包裹ID / Parcel ID</param>
    /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
    Task BroadcastParcelDetectedAsync(
        long parcelId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 向所有连接的客户端广播分拣完成通知
    /// Broadcast sorting completed notification to all connected clients
    /// </summary>
    /// <param name="notification">分拣完成通知 / Sorting completed notification</param>
    /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
    Task BroadcastSortingCompletedAsync(
        Core.Abstractions.Upstream.SortingCompletedNotification notification,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 客户端连接事件
    /// Client connected event
    /// </summary>
    event EventHandler<ClientConnectionEventArgs>? ClientConnected;

    /// <summary>
    /// 客户端断开事件
    /// Client disconnected event
    /// </summary>
    event EventHandler<ClientConnectionEventArgs>? ClientDisconnected;

    /// <summary>
    /// 接收到包裹通知事件
    /// Parcel notification received event
    /// </summary>
    event EventHandler<ParcelNotificationReceivedEventArgs>? ParcelNotificationReceived;

    /// <summary>
    /// 格口分配事件（接收到上游客户端的格口分配通知时触发）
    /// Chute assigned event (triggered when receiving chute assignment notification from upstream client)
    /// </summary>
    /// <remarks>
    /// 与客户端模式保持一致，服务端收到格口分配通知后，应触发此事件。
    /// 订阅者（如 SortingOrchestrator）通过此事件接收格口分配信息并更新 RoutePlan。
    /// </remarks>
    event EventHandler<Core.Abstractions.Upstream.ChuteAssignmentEventArgs>? ChuteAssigned;
}

/// <summary>
/// 客户端连接事件参数
/// Client connection event args
/// PR-PERF-EVENTS01: 转换为 sealed record class 以优化性能
/// </summary>
public sealed record class ClientConnectionEventArgs
{
    /// <summary>
    /// 客户端ID
    /// Client ID
    /// </summary>
    public required string ClientId { get; init; }

    /// <summary>
    /// 连接时间
    /// Connection time
    /// </summary>
    public required DateTimeOffset ConnectedAt { get; init; }

    /// <summary>
    /// 客户端地址（IP:Port）
    /// Client address (IP:Port)
    /// </summary>
    public string? ClientAddress { get; init; }
}

/// <summary>
/// 包裹通知接收事件参数
/// Parcel notification received event args
/// PR-PERF-EVENTS01: 转换为 sealed record class 以优化性能
/// </summary>
public sealed record class ParcelNotificationReceivedEventArgs
{
    /// <summary>
    /// 包裹ID
    /// Parcel ID
    /// </summary>
    public required long ParcelId { get; init; }

    /// <summary>
    /// 发送客户端ID
    /// Sender client ID
    /// </summary>
    public required string ClientId { get; init; }

    /// <summary>
    /// 接收时间
    /// Received time
    /// </summary>
    public required DateTimeOffset ReceivedAt { get; init; }
}
