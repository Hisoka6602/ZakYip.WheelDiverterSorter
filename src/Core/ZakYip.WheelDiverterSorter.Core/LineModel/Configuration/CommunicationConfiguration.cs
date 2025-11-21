using System.ComponentModel.DataAnnotations;
using ZakYip.WheelDiverterSorter.Core.LineModel.Enums;

namespace ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;

/// <summary>
/// 通信配置（存储在LiteDB中，支持热更新）
/// </summary>
public class CommunicationConfiguration
{
    /// <summary>
    /// LiteDB自动生成的唯一标识
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 通信模式
    /// </summary>
    /// <remarks>
    /// 可选值:
    /// - Http: HTTP REST API（仅用于测试，生产环境禁用）
    /// - Tcp: TCP Socket（推荐生产环境）
    /// - SignalR: SignalR（推荐生产环境）
    /// - Mqtt: MQTT（推荐生产环境）
    /// </remarks>
    [Required(ErrorMessage = "通信模式不能为空")]
    public CommunicationMode Mode { get; set; } = CommunicationMode.Http;

    /// <summary>
    /// 连接模式（客户端或服务端）
    /// </summary>
    /// <remarks>
    /// - Client: 本程序作为客户端，主动连接 RuleEngine
    /// - Server: 本程序作为服务端，监听上游连接
    /// </remarks>
    [Required(ErrorMessage = "连接模式不能为空")]
    public ConnectionMode ConnectionMode { get; set; } = ConnectionMode.Client;

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
    [Required(ErrorMessage = "MQTT主题不能为空")]
    [StringLength(200, ErrorMessage = "MQTT主题长度不能超过200个字符")]
    public string MqttTopic { get; set; } = "sorting/chute/assignment";

    /// <summary>
    /// HTTP API URL
    /// </summary>
    public string? HttpApi { get; set; }

    /// <summary>
    /// 请求超时时间（毫秒）
    /// </summary>
    [Range(1000, 60000, ErrorMessage = "请求超时时间必须在1000-60000毫秒之间")]
    public int TimeoutMs { get; set; } = 5000;

    /// <summary>
    /// 重试次数
    /// </summary>
    [Range(0, 10, ErrorMessage = "重试次数必须在0-10之间")]
    public int RetryCount { get; set; } = 3;

    /// <summary>
    /// 重试延迟（毫秒）
    /// </summary>
    [Range(100, 10000, ErrorMessage = "重试延迟必须在100-10000毫秒之间")]
    public int RetryDelayMs { get; set; } = 1000;

    /// <summary>
    /// 是否启用自动重连
    /// </summary>
    public bool EnableAutoReconnect { get; set; } = true;

    /// <summary>
    /// 客户端模式下的初始退避延迟（毫秒）
    /// </summary>
    /// <remarks>
    /// 用于客户端模式的连接重试，起始延迟200ms，每次翻倍增长
    /// </remarks>
    [Range(100, 5000, ErrorMessage = "初始退避延迟必须在100-5000毫秒之间")]
    public int InitialBackoffMs { get; set; } = 200;

    /// <summary>
    /// 客户端模式下的最大退避延迟（毫秒）
    /// </summary>
    /// <remarks>
    /// 硬编码上限为 2000ms (2秒)。即使配置更大值，实现上也会 cap 到 2000ms
    /// </remarks>
    [Range(1000, 10000, ErrorMessage = "最大退避延迟必须在1000-10000毫秒之间（实现上限制为2000ms）")]
    public int MaxBackoffMs { get; set; } = 2000;

    /// <summary>
    /// 客户端模式下是否启用无限重试
    /// </summary>
    /// <remarks>
    /// 默认 true。客户端模式下连接失败会无限重试，不会自动停止
    /// </remarks>
    public bool EnableInfiniteRetry { get; set; } = true;

    /// <summary>
    /// TCP相关配置
    /// </summary>
    public TcpConfig Tcp { get; set; } = new();

    /// <summary>
    /// HTTP相关配置
    /// </summary>
    public HttpConfig Http { get; set; } = new();

    /// <summary>
    /// MQTT相关配置
    /// </summary>
    public MqttConfig Mqtt { get; set; } = new();

    /// <summary>
    /// SignalR相关配置
    /// </summary>
    public SignalRConfig SignalR { get; set; } = new();

    /// <summary>
    /// 配置版本号
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// 配置创建时间（本地时间）
    /// </summary>
    /// <remarks>
    /// 由仓储在创建时通过 ISystemClock.LocalNow 设置
    /// </remarks>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 配置最后更新时间（本地时间）
    /// </summary>
    /// <remarks>
    /// 由仓储在更新时通过 ISystemClock.LocalNow 设置
    /// </remarks>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// 获取默认配置
    /// </summary>
    public static CommunicationConfiguration GetDefault()
    {
        return new CommunicationConfiguration
        {
            Mode = CommunicationMode.Http,
            ConnectionMode = ConnectionMode.Client,
            TcpServer = "192.168.1.100:8000",
            SignalRHub = "http://192.168.1.100:5000/sortingHub",
            MqttBroker = "mqtt://192.168.1.100:1883",
            MqttTopic = "sorting/chute/assignment",
            HttpApi = "http://localhost:5000/api/sorting/chute",
            TimeoutMs = 5000,
            RetryCount = 3,
            RetryDelayMs = 1000,
            EnableAutoReconnect = true,
            InitialBackoffMs = 200,
            MaxBackoffMs = 2000,
            EnableInfiniteRetry = true,
            Tcp = new TcpConfig
            {
                ReceiveBufferSize = 8192,
                SendBufferSize = 8192,
                NoDelay = true,
                KeepAliveInterval = 60
            },
            Http = new HttpConfig
            {
                MaxConnectionsPerServer = 10,
                PooledConnectionIdleTimeout = 60,
                PooledConnectionLifetime = 0,
                UseHttp2 = false
            },
            Mqtt = new MqttConfig
            {
                QualityOfServiceLevel = 1,
                CleanSession = true,
                SessionExpiryInterval = 3600,
                MessageExpiryInterval = 0,
                ClientIdPrefix = "WheelDiverter"
            },
            SignalR = new SignalRConfig
            {
                HandshakeTimeout = 15,
                KeepAliveInterval = 30,
                ServerTimeout = 60,
                SkipNegotiation = false
            }
        };
    }

    /// <summary>
    /// 验证配置
    /// </summary>
    public (bool IsValid, string? ErrorMessage) Validate()
    {
        if (!Enum.IsDefined(typeof(CommunicationMode), Mode))
        {
            return (false, "通信模式无效");
        }

        // 根据模式验证对应的配置
        switch (Mode)
        {
            case CommunicationMode.Tcp:
                if (string.IsNullOrWhiteSpace(TcpServer))
                {
                    return (false, "TCP模式下，TcpServer不能为空");
                }
                break;

            case CommunicationMode.SignalR:
                if (string.IsNullOrWhiteSpace(SignalRHub))
                {
                    return (false, "SignalR模式下，SignalRHub不能为空");
                }
                break;

            case CommunicationMode.Mqtt:
                if (string.IsNullOrWhiteSpace(MqttBroker))
                {
                    return (false, "MQTT模式下，MqttBroker不能为空");
                }
                if (string.IsNullOrWhiteSpace(MqttTopic))
                {
                    return (false, "MQTT模式下，MqttTopic不能为空");
                }
                break;

            case CommunicationMode.Http:
                if (string.IsNullOrWhiteSpace(HttpApi))
                {
                    return (false, "HTTP模式下，HttpApi不能为空");
                }
                break;
        }

        if (TimeoutMs <= 0)
        {
            return (false, "超时时间必须大于0");
        }

        if (RetryCount < 0)
        {
            return (false, "重试次数不能为负数");
        }

        if (RetryDelayMs < 0)
        {
            return (false, "重试延迟不能为负数");
        }

        return (true, null);
    }
}

/// <summary>
/// TCP配置
/// </summary>
public class TcpConfig
{
    /// <summary>
    /// 接收缓冲区大小（字节）
    /// </summary>
    public int ReceiveBufferSize { get; set; } = 8192;
    
    /// <summary>
    /// 发送缓冲区大小（字节）
    /// </summary>
    public int SendBufferSize { get; set; } = 8192;
    
    /// <summary>
    /// 禁用Nagle算法以减少延迟
    /// </summary>
    public bool NoDelay { get; set; } = true;
    
    /// <summary>
    /// 保持连接心跳间隔（秒）
    /// </summary>
    public int KeepAliveInterval { get; set; } = 60;
}

/// <summary>
/// HTTP配置
/// </summary>
public class HttpConfig
{
    /// <summary>
    /// 每个服务器的最大连接数
    /// </summary>
    public int MaxConnectionsPerServer { get; set; } = 10;
    
    /// <summary>
    /// 连接池中空闲连接的超时时间（秒）
    /// </summary>
    public int PooledConnectionIdleTimeout { get; set; } = 60;
    
    /// <summary>
    /// 连接池中连接的生命周期（秒，0表示无限制）
    /// </summary>
    public int PooledConnectionLifetime { get; set; } = 0;
    
    /// <summary>
    /// 是否使用HTTP/2协议
    /// </summary>
    public bool UseHttp2 { get; set; } = false;
}

/// <summary>
/// MQTT配置
/// </summary>
public class MqttConfig
{
    /// <summary>
    /// 服务质量等级 (0: 最多一次, 1: 至少一次, 2: 恰好一次)
    /// </summary>
    public int QualityOfServiceLevel { get; set; } = 1;
    
    /// <summary>
    /// 是否使用清除会话
    /// </summary>
    public bool CleanSession { get; set; } = true;
    
    /// <summary>
    /// 会话过期时间间隔（秒）
    /// </summary>
    public int SessionExpiryInterval { get; set; } = 3600;
    
    /// <summary>
    /// 消息过期时间间隔（秒，0表示不过期）
    /// </summary>
    public int MessageExpiryInterval { get; set; } = 0;
    
    /// <summary>
    /// 客户端ID前缀
    /// </summary>
    public string ClientIdPrefix { get; set; } = "WheelDiverter";
}

/// <summary>
/// SignalR配置
/// </summary>
public class SignalRConfig
{
    /// <summary>
    /// 握手超时时间（秒）
    /// </summary>
    public int HandshakeTimeout { get; set; } = 15;
    
    /// <summary>
    /// 保持连接心跳间隔（秒）
    /// </summary>
    public int KeepAliveInterval { get; set; } = 30;
    
    /// <summary>
    /// 服务器超时时间（秒）
    /// </summary>
    public int ServerTimeout { get; set; } = 60;
    
    /// <summary>
    /// 是否跳过协议协商
    /// </summary>
    public bool SkipNegotiation { get; set; } = false;
}
