namespace ZakYip.WheelDiverterSorter.Communication.Configuration;

/// <summary>
/// 通信模式
/// </summary>
public enum CommunicationMode
{
    /// <summary>
    /// HTTP REST API（仅用于测试，生产环境禁用）
    /// </summary>
    Http,

    /// <summary>
    /// TCP Socket（推荐生产环境）
    /// </summary>
    Tcp,

    /// <summary>
    /// SignalR（推荐生产环境）
    /// </summary>
    SignalR,

    /// <summary>
    /// MQTT（推荐生产环境）
    /// </summary>
    Mqtt
}

/// <summary>
/// RuleEngine连接配置选项
/// </summary>
public class RuleEngineConnectionOptions
{
    /// <summary>
    /// 通信模式
    /// </summary>
    public CommunicationMode Mode { get; set; } = CommunicationMode.Http;

    /// <summary>
    /// TCP服务器地址（格式：host:port）
    /// </summary>
    public string? TcpServer { get; set; }

    /// <summary>
    /// SignalR Hub URL
    /// </summary>
    public string? SignalRHub { get; set; }

    /// <summary>
    /// MQTT Broker地址
    /// </summary>
    public string? MqttBroker { get; set; }

    /// <summary>
    /// MQTT主题
    /// </summary>
    public string MqttTopic { get; set; } = "sorting/chute/assignment";

    /// <summary>
    /// HTTP API URL
    /// </summary>
    public string? HttpApi { get; set; }

    /// <summary>
    /// 请求超时时间（毫秒）
    /// </summary>
    public int TimeoutMs { get; set; } = 5000;

    /// <summary>
    /// 重试次数
    /// </summary>
    public int RetryCount { get; set; } = 3;

    /// <summary>
    /// 重试延迟（毫秒）
    /// </summary>
    public int RetryDelayMs { get; set; } = 1000;

    /// <summary>
    /// 是否启用自动重连
    /// </summary>
    public bool EnableAutoReconnect { get; set; } = true;

    /// <summary>
    /// 格口分配等待超时时间（毫秒）
    /// </summary>
    /// <remarks>
    /// 等待RuleEngine推送格口分配的最大时间。超时后将使用异常格口
    /// </remarks>
    public int ChuteAssignmentTimeoutMs { get; set; } = 10000;

    /// <summary>
    /// TCP相关配置
    /// </summary>
    public TcpOptions Tcp { get; set; } = new();

    /// <summary>
    /// HTTP相关配置
    /// </summary>
    public HttpOptions Http { get; set; } = new();

    /// <summary>
    /// MQTT相关配置
    /// </summary>
    public MqttOptions Mqtt { get; set; } = new();

    /// <summary>
    /// SignalR相关配置
    /// </summary>
    public SignalROptions SignalR { get; set; } = new();
}

/// <summary>
/// TCP协议配置选项
/// </summary>
public class TcpOptions
{
    /// <summary>
    /// TCP接收缓冲区大小（字节）
    /// </summary>
    /// <remarks>
    /// 默认8KB。根据消息大小调整：
    /// - 小消息（<1KB）：4096
    /// - 中等消息（1-4KB）：8192
    /// - 大消息（>4KB）：16384或更大
    /// </remarks>
    public int ReceiveBufferSize { get; set; } = 8192;

    /// <summary>
    /// TCP发送缓冲区大小（字节）
    /// </summary>
    /// <remarks>
    /// 默认8KB
    /// </remarks>
    public int SendBufferSize { get; set; } = 8192;

    /// <summary>
    /// 是否启用Nagle算法
    /// </summary>
    /// <remarks>
    /// 默认false（禁用Nagle）以降低延迟
    /// 启用Nagle可以提高网络利用率，但增加延迟
    /// </remarks>
    public bool NoDelay { get; set; } = true;

    /// <summary>
    /// TCP KeepAlive时间间隔（秒）
    /// </summary>
    /// <remarks>
    /// 默认60秒。0表示禁用KeepAlive
    /// </remarks>
    public int KeepAliveInterval { get; set; } = 60;
}

/// <summary>
/// HTTP协议配置选项
/// </summary>
public class HttpOptions
{
    /// <summary>
    /// HTTP连接池最大连接数
    /// </summary>
    /// <remarks>
    /// 默认10。根据并发需求调整
    /// </remarks>
    public int MaxConnectionsPerServer { get; set; } = 10;

    /// <summary>
    /// 连接空闲超时时间（秒）
    /// </summary>
    /// <remarks>
    /// 默认60秒。连接空闲超过此时间会被回收
    /// </remarks>
    public int PooledConnectionIdleTimeout { get; set; } = 60;

    /// <summary>
    /// 连接生存时间（秒）
    /// </summary>
    /// <remarks>
    /// 默认0（无限制）。强制回收长期连接以应对DNS变化
    /// </remarks>
    public int PooledConnectionLifetime { get; set; } = 0;

    /// <summary>
    /// 是否使用HTTP/2协议
    /// </summary>
    /// <remarks>
    /// 默认false。仅当服务端支持时启用
    /// </remarks>
    public bool UseHttp2 { get; set; } = false;
}

/// <summary>
/// MQTT协议配置选项
/// </summary>
public class MqttOptions
{
    /// <summary>
    /// MQTT服务质量等级
    /// </summary>
    /// <remarks>
    /// 0 = At most once (最多一次，可能丢失)
    /// 1 = At least once (至少一次，可能重复) - 默认
    /// 2 = Exactly once (恰好一次，最可靠但最慢)
    /// </remarks>
    public int QualityOfServiceLevel { get; set; } = 1;

    /// <summary>
    /// 是否使用Clean Session
    /// </summary>
    /// <remarks>
    /// true = 不保留会话状态（默认）
    /// false = 保留会话状态和订阅
    /// </remarks>
    public bool CleanSession { get; set; } = true;

    /// <summary>
    /// 会话保持时间（秒）
    /// </summary>
    /// <remarks>
    /// 默认3600秒（1小时）。0表示连接断开后立即清理会话
    /// </remarks>
    public int SessionExpiryInterval { get; set; } = 3600;

    /// <summary>
    /// 消息保留时间（秒）
    /// </summary>
    /// <remarks>
    /// 默认0（不保留）。用于保留最后一条消息给新订阅者
    /// </remarks>
    public int MessageExpiryInterval { get; set; } = 0;

    /// <summary>
    /// MQTT客户端ID前缀
    /// </summary>
    /// <remarks>
    /// 默认"WheelDiverter"。完整ID为：前缀_GUID
    /// </remarks>
    public string ClientIdPrefix { get; set; } = "WheelDiverter";
}

/// <summary>
/// SignalR协议配置选项
/// </summary>
public class SignalROptions
{
    /// <summary>
    /// 握手超时时间（秒）
    /// </summary>
    /// <remarks>
    /// 默认15秒
    /// </remarks>
    public int HandshakeTimeout { get; set; } = 15;

    /// <summary>
    /// 保持连接超时时间（秒）
    /// </summary>
    /// <remarks>
    /// 默认30秒。服务端发送心跳包的间隔
    /// </remarks>
    public int KeepAliveInterval { get; set; } = 30;

    /// <summary>
    /// 服务端超时时间（秒）
    /// </summary>
    /// <remarks>
    /// 默认60秒。未收到服务端心跳的最大等待时间
    /// </remarks>
    public int ServerTimeout { get; set; } = 60;

    /// <summary>
    /// 重连间隔（毫秒）
    /// </summary>
    /// <remarks>
    /// 默认null（使用默认策略）。可设置固定重连间隔，如 [0, 2000, 5000, 10000]
    /// </remarks>
    public int[]? ReconnectIntervals { get; set; }

    /// <summary>
    /// 是否跳过协商（直接使用WebSocket）
    /// </summary>
    /// <remarks>
    /// 默认false。设为true可减少连接延迟，但需要服务端支持
    /// </remarks>
    public bool SkipNegotiation { get; set; } = false;
}
