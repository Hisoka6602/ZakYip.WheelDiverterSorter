using System.ComponentModel.DataAnnotations;
using Swashbuckle.AspNetCore.Annotations;
using ZakYip.WheelDiverterSorter.Core.Enums.Communication;

namespace ZakYip.WheelDiverterSorter.Host.Models.Communication;

/// <summary>
/// 通信配置请求模型
/// </summary>
[SwaggerSchema(Description = "更新通信配置的请求，参数按协议分组")]
public class CommunicationConfigurationRequest
{
    /// <summary>
    /// 通信模式
    /// </summary>
    [SwaggerSchema(Description = "通信协议类型：Http(0), Tcp(1), SignalR(2), Mqtt(3)")]
    [Required(ErrorMessage = "通信模式不能为空")]
    public CommunicationMode Mode { get; set; }

    /// <summary>
    /// 连接模式（客户端或服务端）
    /// </summary>
    [SwaggerSchema(Description = "连接角色：Client(0)=客户端主动连接, Server(1)=服务端等待连接")]
    [Required(ErrorMessage = "连接模式不能为空")]
    public ConnectionMode ConnectionMode { get; set; }

    /// <summary>
    /// 请求超时时间（毫秒）
    /// </summary>
    [SwaggerSchema(Description = "请求超时时间，范围：1000-60000毫秒")]
    [Range(1000, 60000, ErrorMessage = "请求超时时间必须在1000-60000毫秒之间")]
    public int TimeoutMs { get; set; } = 5000;

    /// <summary>
    /// 重试次数
    /// </summary>
    [SwaggerSchema(Description = "请求失败后的重试次数，范围：0-10")]
    [Range(0, 10, ErrorMessage = "重试次数必须在0-10之间")]
    public int RetryCount { get; set; } = 3;

    /// <summary>
    /// 重试延迟（毫秒）
    /// </summary>
    [SwaggerSchema(Description = "重试之间的延迟时间，范围：100-10000毫秒")]
    [Range(100, 10000, ErrorMessage = "重试延迟必须在100-10000毫秒之间")]
    public int RetryDelayMs { get; set; } = 1000;

    /// <summary>
    /// 是否启用自动重连
    /// </summary>
    [SwaggerSchema(Description = "连接断开后是否自动尝试重新连接")]
    public bool EnableAutoReconnect { get; set; } = true;

    /// <summary>
    /// TCP 相关配置
    /// </summary>
    [SwaggerSchema(Description = "TCP 协议专用配置（仅当Mode=Tcp时生效）")]
    public TcpConfigDto? Tcp { get; set; }

    /// <summary>
    /// HTTP 相关配置
    /// </summary>
    [SwaggerSchema(Description = "HTTP 协议专用配置（仅当Mode=Http时生效）")]
    public HttpConfigDto? Http { get; set; }

    /// <summary>
    /// MQTT 相关配置
    /// </summary>
    [SwaggerSchema(Description = "MQTT 协议专用配置（仅当Mode=Mqtt时生效）")]
    public MqttConfigDto? Mqtt { get; set; }

    /// <summary>
    /// SignalR 相关配置
    /// </summary>
    [SwaggerSchema(Description = "SignalR 协议专用配置（仅当Mode=SignalR时生效）")]
    public SignalRConfigDto? SignalR { get; set; }
}

/// <summary>
/// TCP 配置
/// </summary>
[SwaggerSchema(Description = "TCP Socket 通信配置")]
public class TcpConfigDto
{
    /// <summary>
    /// TCP服务器地址（格式：host:port）
    /// </summary>
    [SwaggerSchema(Description = "TCP服务器地址，格式：IP:端口，例如：192.168.1.100:8000")]
    public string? TcpServer { get; set; }

    /// <summary>
    /// 接收缓冲区大小（字节）
    /// </summary>
    [SwaggerSchema(Description = "Socket接收缓冲区大小")]
    public int ReceiveBufferSize { get; set; } = 8192;

    /// <summary>
    /// 发送缓冲区大小（字节）
    /// </summary>
    [SwaggerSchema(Description = "Socket发送缓冲区大小")]
    public int SendBufferSize { get; set; } = 8192;

    /// <summary>
    /// 禁用Nagle算法以减少延迟
    /// </summary>
    [SwaggerSchema(Description = "是否禁用Nagle算法（true=低延迟，false=高吞吐）")]
    public bool NoDelay { get; set; } = true;
}

/// <summary>
/// HTTP 配置
/// </summary>
[SwaggerSchema(Description = "HTTP REST API 通信配置")]
public class HttpConfigDto
{
    /// <summary>
    /// HTTP API URL
    /// </summary>
    [SwaggerSchema(Description = "HTTP API端点地址，例如：http://192.168.1.100:5000/api/sorting/chute")]
    public string? HttpApi { get; set; }

    /// <summary>
    /// 每个服务器的最大连接数
    /// </summary>
    [SwaggerSchema(Description = "连接池中每个服务器的最大连接数")]
    public int MaxConnectionsPerServer { get; set; } = 10;

    /// <summary>
    /// 连接池中空闲连接的超时时间（秒）
    /// </summary>
    [SwaggerSchema(Description = "连接池中空闲连接的超时时间，单位：秒")]
    public int PooledConnectionIdleTimeout { get; set; } = 60;

    /// <summary>
    /// 连接池中连接的生命周期（秒，0表示无限制）
    /// </summary>
    [SwaggerSchema(Description = "连接池中连接的最大生命周期，0表示无限制，单位：秒")]
    public int PooledConnectionLifetime { get; set; } = 0;

    /// <summary>
    /// 是否使用HTTP/2协议
    /// </summary>
    [SwaggerSchema(Description = "是否启用HTTP/2协议（需要服务器支持）")]
    public bool UseHttp2 { get; set; } = false;
}

/// <summary>
/// MQTT 配置
/// </summary>
[SwaggerSchema(Description = "MQTT 消息队列通信配置")]
public class MqttConfigDto
{
    /// <summary>
    /// MQTT Broker地址
    /// </summary>
    [SwaggerSchema(Description = "MQTT Broker地址，格式：mqtt://host:port，例如：mqtt://192.168.1.100:1883")]
    public string? MqttBroker { get; set; }

    /// <summary>
    /// MQTT主题
    /// </summary>
    [SwaggerSchema(Description = "订阅/发布的MQTT主题，例如：sorting/chute/assignment")]
    [StringLength(200, ErrorMessage = "MQTT主题长度不能超过200个字符")]
    public string MqttTopic { get; set; } = "sorting/chute/assignment";

    /// <summary>
    /// 服务质量等级 (0: 最多一次, 1: 至少一次, 2: 恰好一次)
    /// </summary>
    [SwaggerSchema(Description = "QoS等级：0=最多一次, 1=至少一次, 2=恰好一次")]
    [Range(0, 2, ErrorMessage = "QoS等级必须是0、1或2")]
    public int QualityOfServiceLevel { get; set; } = 1;

    /// <summary>
    /// 是否使用清除会话
    /// </summary>
    [SwaggerSchema(Description = "是否使用Clean Session（true=不保留会话状态）")]
    public bool CleanSession { get; set; } = true;

    /// <summary>
    /// 会话过期时间间隔（秒）
    /// </summary>
    [SwaggerSchema(Description = "会话过期时间，单位：秒")]
    public int SessionExpiryInterval { get; set; } = 3600;

    /// <summary>
    /// 消息过期时间间隔（秒，0表示不过期）
    /// </summary>
    [SwaggerSchema(Description = "消息过期时间，0表示永不过期，单位：秒")]
    public int MessageExpiryInterval { get; set; } = 0;

    /// <summary>
    /// 客户端ID前缀
    /// </summary>
    [SwaggerSchema(Description = "MQTT客户端ID的前缀，实际ID为：前缀+UUID")]
    public string ClientIdPrefix { get; set; } = "WheelDiverter";
}

/// <summary>
/// SignalR 配置
/// </summary>
[SwaggerSchema(Description = "SignalR 实时通信配置")]
public class SignalRConfigDto
{
    /// <summary>
    /// SignalR Hub URL
    /// </summary>
    [SwaggerSchema(Description = "SignalR Hub端点地址，例如：http://192.168.1.100:5000/sortingHub")]
    public string? SignalRHub { get; set; }

    /// <summary>
    /// 握手超时时间（秒）
    /// </summary>
    [SwaggerSchema(Description = "SignalR握手超时时间，单位：秒")]
    public int HandshakeTimeout { get; set; } = 15;

    /// <summary>
    /// 保持连接心跳间隔（秒）
    /// </summary>
    [SwaggerSchema(Description = "Keep-Alive心跳间隔，单位：秒")]
    public int KeepAliveInterval { get; set; } = 30;

    /// <summary>
    /// 服务器超时时间（秒）
    /// </summary>
    [SwaggerSchema(Description = "服务器响应超时时间，单位：秒")]
    public int ServerTimeout { get; set; } = 60;

    /// <summary>
    /// 是否跳过协议协商
    /// </summary>
    [SwaggerSchema(Description = "是否跳过传输协议协商（高级选项）")]
    public bool SkipNegotiation { get; set; } = false;
}
