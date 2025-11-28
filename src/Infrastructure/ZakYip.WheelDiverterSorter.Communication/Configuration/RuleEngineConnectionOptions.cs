using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Enums.Communication;

namespace ZakYip.WheelDiverterSorter.Communication.Configuration;

/// <summary>
/// RuleEngine连接配置选项
/// </summary>
public class RuleEngineConnectionOptions
{
    /// <summary>
    /// 通信模式
    /// </summary>
    /// <remarks>
    /// 默认使用TCP模式，因为HTTP仅用于测试场景，生产环境不使用HTTP
    /// </remarks>
    public CommunicationMode Mode { get; set; } = CommunicationMode.Tcp;

    /// <summary>
    /// 连接模式（客户端或服务端）
    /// </summary>
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
    /// 重试次数（已废弃，不再使用）
    /// </summary>
    /// <remarks>
    /// ⚠️ 此参数已废弃，不再使用。
    /// 
    /// 原因：
    /// 1. 发送操作：根据系统规则，发送失败只记录日志，不进行重试
    /// 2. 连接操作：客户端模式使用无限重试机制（EnableInfiniteRetry=true）
    /// 
    /// 保留此字段仅为向后兼容，实际代码中不应使用此参数。
    /// 新代码请使用无限重试机制（EnableInfiniteRetry）。
    /// </remarks>
    public int RetryCount { get; set; } = 0;

    /// <summary>
    /// 重试延迟（已废弃，不再使用）
    /// </summary>
    /// <remarks>
    /// ⚠️ 此参数已废弃，因为发送操作不再重试。
    /// 保留此字段仅为向后兼容。
    /// </remarks>
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
    public int InitialBackoffMs { get; set; } = 200;

    /// <summary>
    /// 客户端模式下的最大退避延迟（毫秒）
    /// </summary>
    /// <remarks>
    /// 硬编码上限为 2000ms (2秒)。即使配置更大值，实现上也会 cap 到 2000ms
    /// </remarks>
    public int MaxBackoffMs { get; set; } = 2000;

    /// <summary>
    /// 客户端模式下是否启用无限重试
    /// </summary>
    /// <remarks>
    /// 默认 true。客户端模式下连接失败会无限重试，不会自动停止
    /// </remarks>
    public bool EnableInfiniteRetry { get; set; } = true;

    /// <summary>
    /// 客户端模式下的最大退避时间（秒）
    /// </summary>
    /// <remarks>
    /// 默认5秒。用于客户端模式的无限重连机制
    /// </remarks>
    public int MaxBackoffSeconds { get; set; } = 5;

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
