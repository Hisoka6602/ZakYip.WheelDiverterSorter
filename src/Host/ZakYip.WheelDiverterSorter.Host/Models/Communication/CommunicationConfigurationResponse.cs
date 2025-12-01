using Swashbuckle.AspNetCore.Annotations;
using ZakYip.WheelDiverterSorter.Core.Enums.Communication;

namespace ZakYip.WheelDiverterSorter.Host.Models.Communication;

/// <summary>
/// 通信配置响应模型
/// </summary>
/// <remarks>
/// PR-UPSTREAM01: HTTP 模式已移除，只支持 Tcp/SignalR/Mqtt。
/// </remarks>
[SwaggerSchema(Description = "通信配置信息，参数按协议分组")]
public class CommunicationConfigurationResponse
{
    /// <summary>
    /// 通信模式
    /// </summary>
    [SwaggerSchema(Description = "当前使用的通信协议：Tcp/SignalR/Mqtt")]
    public CommunicationMode Mode { get; init; }

    /// <summary>
    /// 连接模式（客户端或服务端）
    /// </summary>
    [SwaggerSchema(Description = "连接角色")]
    public ConnectionMode ConnectionMode { get; init; }

    /// <summary>
    /// 请求超时时间（毫秒）
    /// </summary>
    [SwaggerSchema(Description = "请求超时时间（毫秒）")]
    public int TimeoutMs { get; init; }

    /// <summary>
    /// 重试次数
    /// </summary>
    [SwaggerSchema(Description = "请求失败后的重试次数")]
    public int RetryCount { get; init; }

    /// <summary>
    /// 重试延迟（毫秒）
    /// </summary>
    [SwaggerSchema(Description = "重试之间的延迟时间（毫秒）")]
    public int RetryDelayMs { get; init; }

    /// <summary>
    /// 是否启用自动重连
    /// </summary>
    [SwaggerSchema(Description = "连接断开后是否自动重连")]
    public bool EnableAutoReconnect { get; init; }

    /// <summary>
    /// TCP 相关配置
    /// </summary>
    [SwaggerSchema(Description = "TCP 协议配置（Mode=Tcp时有效）")]
    public TcpConfigDto? Tcp { get; init; }

    /// <summary>
    /// MQTT 相关配置
    /// </summary>
    [SwaggerSchema(Description = "MQTT 协议配置（Mode=Mqtt时有效）")]
    public MqttConfigDto? Mqtt { get; init; }

    /// <summary>
    /// SignalR 相关配置
    /// </summary>
    [SwaggerSchema(Description = "SignalR 协议配置（Mode=SignalR时有效）")]
    public SignalRConfigDto? SignalR { get; init; }

    /// <summary>
    /// 配置版本号
    /// </summary>
    [SwaggerSchema(Description = "配置版本号，每次更新递增")]
    public int Version { get; init; }

    /// <summary>
    /// 配置创建时间
    /// </summary>
    [SwaggerSchema(Description = "配置首次创建时间")]
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// 配置最后更新时间
    /// </summary>
    [SwaggerSchema(Description = "配置最后更新时间")]
    public DateTime UpdatedAt { get; init; }
}
