namespace ZakYip.WheelDiverterSorter.Host.Models.Communication;

/// <summary>
/// 通信状态响应模型 - Communication Status Response Model
/// </summary>
public class CommunicationStatusResponse
{
    /// <summary>
    /// 当前通信模式 - Current communication mode
    /// </summary>
    public required string Mode { get; init; }

    /// <summary>
    /// 连接状态 - Connection status
    /// </summary>
    public required bool IsConnected { get; init; }

    /// <summary>
    /// 发送消息计数 - Sent messages count
    /// </summary>
    public required long MessagesSent { get; init; }

    /// <summary>
    /// 接收消息计数 - Received messages count
    /// </summary>
    public required long MessagesReceived { get; init; }

    /// <summary>
    /// 连接时长（秒） - Connection duration in seconds
    /// </summary>
    public long? ConnectionDurationSeconds { get; init; }

    /// <summary>
    /// 最后连接时间 - Last connection time
    /// </summary>
    public DateTimeOffset? LastConnectedAt { get; init; }

    /// <summary>
    /// 最后断开时间 - Last disconnection time
    /// </summary>
    public DateTimeOffset? LastDisconnectedAt { get; init; }

    /// <summary>
    /// 服务器地址 - Server address
    /// </summary>
    public string? ServerAddress { get; init; }

    /// <summary>
    /// 错误信息 - Error message
    /// </summary>
    public string? ErrorMessage { get; init; }
}
