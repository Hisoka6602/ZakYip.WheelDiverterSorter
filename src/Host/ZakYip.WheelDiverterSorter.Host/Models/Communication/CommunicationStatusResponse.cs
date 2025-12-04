using Swashbuckle.AspNetCore.Annotations;

namespace ZakYip.WheelDiverterSorter.Host.Models.Communication;

/// <summary>
/// 通信状态响应模型 - Communication Status Response Model
/// </summary>
[SwaggerSchema(Description = "与上游RuleEngine的通信状态信息")]
public class CommunicationStatusResponse
{
    /// <summary>
    /// 当前通信模式 - Current communication mode
    /// </summary>
    /// <example>TCP</example>
    [SwaggerSchema(Description = "当前使用的通信协议，可能值：TCP、MQTT、SignalR、HTTP")]
    public required string Mode { get; init; }

    /// <summary>
    /// 连接状态 - Connection status
    /// </summary>
    /// <example>true</example>
    [SwaggerSchema(Description = "当前是否已连接到上游")]
    public required bool IsConnected { get; init; }

    /// <summary>
    /// 发送消息计数 - Sent messages count
    /// </summary>
    /// <example>1523</example>
    [SwaggerSchema(Description = "已发送的消息总数")]
    public required long MessagesSent { get; init; }

    /// <summary>
    /// 接收消息计数 - Received messages count
    /// </summary>
    /// <example>1487</example>
    [SwaggerSchema(Description = "已接收的消息总数")]
    public required long MessagesReceived { get; init; }

    /// <summary>
    /// 连接时长（秒） - Connection duration in seconds
    /// </summary>
    /// <example>3600</example>
    [SwaggerSchema(Description = "当前连接持续的时间，单位：秒")]
    public long? ConnectionDurationSeconds { get; init; }

    /// <summary>
    /// 最后连接时间 - Last connection time
    /// </summary>
    /// <example>2025-11-17T09:00:00Z</example>
    [SwaggerSchema(Description = "最后一次成功连接的时间")]
    public DateTimeOffset? LastConnectedAt { get; init; }

    /// <summary>
    /// 最后断开时间 - Last disconnection time
    /// </summary>
    /// <example>2025-11-17T08:55:00Z</example>
    [SwaggerSchema(Description = "最后一次断开连接的时间")]
    public DateTimeOffset? LastDisconnectedAt { get; init; }

    /// <summary>
    /// 服务器地址 - Server address
    /// </summary>
    /// <example>192.168.1.100:8888</example>
    [SwaggerSchema(Description = "连接的服务器地址")]
    public string? ServerAddress { get; init; }

    /// <summary>
    /// 错误信息 - Error message
    /// </summary>
    /// <example>null</example>
    [SwaggerSchema(Description = "如果连接失败，此字段包含错误信息")]
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// 连接模式 - Connection mode
    /// </summary>
    /// <example>Client</example>
    [SwaggerSchema(Description = "连接模式，可能值：Client（客户端模式）、Server（服务端模式）")]
    public string? ConnectionMode { get; init; }

    /// <summary>
    /// 已连接的客户端列表（仅Server模式有效） - Connected clients list (Server mode only)
    /// </summary>
    /// <example>null</example>
    [SwaggerSchema(Description = "Server模式下，当前连接的所有客户端信息；Client模式下为null")]
    public List<ConnectedClientDto>? ConnectedClients { get; init; }
}

/// <summary>
/// 已连接客户端信息 - Connected Client Information
/// </summary>
[SwaggerSchema(Description = "Server模式下已连接客户端的详细信息")]
public record ConnectedClientDto
{
    /// <summary>
    /// 客户端ID - Client ID
    /// </summary>
    /// <example>client-001</example>
    [SwaggerSchema(Description = "客户端的唯一标识符")]
    public required string ClientId { get; init; }

    /// <summary>
    /// 客户端地址 - Client Address
    /// </summary>
    /// <example>192.168.1.50:54321</example>
    [SwaggerSchema(Description = "客户端的IP地址和端口")]
    public string? ClientAddress { get; init; }

    /// <summary>
    /// 连接时间 - Connected At
    /// </summary>
    /// <example>2025-11-17T09:00:00Z</example>
    [SwaggerSchema(Description = "客户端连接的时间")]
    public required DateTimeOffset ConnectedAt { get; init; }

    /// <summary>
    /// 连接时长（秒） - Connection Duration in Seconds
    /// </summary>
    /// <example>3600</example>
    [SwaggerSchema(Description = "客户端已连接的时长，单位：秒")]
    public long ConnectionDurationSeconds { get; init; }
}
