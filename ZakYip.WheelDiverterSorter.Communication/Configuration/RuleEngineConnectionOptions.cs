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
}
