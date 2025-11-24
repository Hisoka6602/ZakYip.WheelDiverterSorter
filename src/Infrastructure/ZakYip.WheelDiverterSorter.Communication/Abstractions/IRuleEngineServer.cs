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
}

/// <summary>
/// 客户端连接事件参数
/// Client connection event args
/// </summary>
public class ClientConnectionEventArgs : EventArgs
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
    public DateTime ConnectedAt { get; init; }

    /// <summary>
    /// 客户端地址（IP:Port）
    /// Client address (IP:Port)
    /// </summary>
    public string? ClientAddress { get; init; }
}

/// <summary>
/// 包裹通知接收事件参数
/// Parcel notification received event args
/// </summary>
public class ParcelNotificationReceivedEventArgs : EventArgs
{
    /// <summary>
    /// 包裹ID
    /// Parcel ID
    /// </summary>
    public long ParcelId { get; init; }

    /// <summary>
    /// 发送客户端ID
    /// Sender client ID
    /// </summary>
    public required string ClientId { get; init; }

    /// <summary>
    /// 接收时间
    /// Received time
    /// </summary>
    public DateTime ReceivedAt { get; init; }
}
