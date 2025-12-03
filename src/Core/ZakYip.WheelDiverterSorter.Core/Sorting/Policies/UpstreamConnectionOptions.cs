using Microsoft.Extensions.Options;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Enums.Communication;

namespace ZakYip.WheelDiverterSorter.Core.Sorting.Policies;

/// <summary>
/// 上游连接配置选项（强类型）
/// </summary>
/// <remarks>
/// <para>统一管理与上游 RuleEngine 的 TCP/SignalR/MQTT 连接参数。</para>
/// <para>通过 IValidateOptions 实现启动时校验。</para>
/// <para>PR-UPSTREAM01: 移除 HTTP 协议支持，只支持 TCP/SignalR/MQTT。</para>
/// <para>PR-CONFIG-HOTRELOAD02: 合并 RuleEngineConnectionOptions，成为唯一权威配置。</para>
/// </remarks>
public record UpstreamConnectionOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "UpstreamConnection";

    /// <summary>
    /// 通信模式
    /// </summary>
    /// <remarks>
    /// PR-UPSTREAM01: HTTP 已移除，可选值：Tcp（默认，推荐生产环境）、SignalR、Mqtt
    /// </remarks>
    public CommunicationMode Mode { get; init; } = CommunicationMode.Tcp;

    /// <summary>
    /// 连接模式（客户端或服务端）
    /// </summary>
    /// <remarks>
    /// Client: 主动连接上游 RuleEngine；Server: 监听上游连接
    /// </remarks>
    public ConnectionMode ConnectionMode { get; init; } = ConnectionMode.Client;

    /// <summary>
    /// TCP服务器地址（格式：host:port）
    /// </summary>
    /// <remarks>
    /// 当 Mode 为 Tcp 时必须配置
    /// </remarks>
    public string? TcpServer { get; init; }

    /// <summary>
    /// SignalR Hub URL
    /// </summary>
    /// <remarks>
    /// 当 Mode 为 SignalR 时必须配置
    /// </remarks>
    public string? SignalRHub { get; init; }

    /// <summary>
    /// MQTT Broker地址
    /// </summary>
    /// <remarks>
    /// 当 Mode 为 Mqtt 时必须配置
    /// </remarks>
    public string? MqttBroker { get; init; }

    /// <summary>
    /// MQTT主题
    /// </summary>
    public string MqttTopic { get; init; } = "sorting/chute/assignment";

    /// <summary>
    /// 请求超时时间（毫秒）
    /// </summary>
    public int TimeoutMs { get; init; } = 5000;

    /// <summary>
    /// 是否启用自动重连
    /// </summary>
    public bool EnableAutoReconnect { get; init; } = true;

    /// <summary>
    /// 客户端模式下的初始退避延迟（毫秒）
    /// </summary>
    /// <remarks>
    /// 用于客户端模式的连接重试，起始延迟200ms，每次翻倍增长
    /// </remarks>
    public int InitialBackoffMs { get; init; } = 200;

    /// <summary>
    /// 客户端模式下的最大退避延迟（毫秒）
    /// </summary>
    /// <remarks>
    /// 硬编码上限为 2000ms (2秒)。即使配置更大值，实现上也会 cap 到 2000ms
    /// </remarks>
    public int MaxBackoffMs { get; init; } = 2000;

    /// <summary>
    /// 客户端模式下是否启用无限重试
    /// </summary>
    /// <remarks>
    /// 默认 true。客户端模式下连接失败会无限重试，不会自动停止
    /// </remarks>
    public bool EnableInfiniteRetry { get; init; } = true;

    /// <summary>
    /// 客户端模式下的最大退避时间（秒）
    /// </summary>
    /// <remarks>
    /// 默认5秒。用于客户端模式的无限重连机制
    /// </remarks>
    public int MaxBackoffSeconds { get; init; } = 5;

    /// <summary>
    /// 重试次数
    /// </summary>
    /// <remarks>
    /// 用于非无限重试模式下的最大重试次数。默认为 3 次。
    /// </remarks>
    public int RetryCount { get; init; } = 3;

    /// <summary>
    /// 重试延迟（毫秒）
    /// </summary>
    /// <remarks>
    /// 用于重试之间的基础延迟时间。默认为 1000ms (1秒)。
    /// </remarks>
    public int RetryDelayMs { get; init; } = 1000;

    /// <summary>
    /// 格口分配等待超时时间（毫秒）
    /// </summary>
    /// <remarks>
    /// 等待RuleEngine推送格口分配的最大时间。超时后将使用异常格口
    /// </remarks>
    public int ChuteAssignmentTimeoutMs { get; init; } = 10000;

    /// <summary>
    /// 格口分配超时时间（秒）
    /// </summary>
    /// <remarks>
    /// 当无法通过动态超时计算器获取超时时间时，使用此备用值。
    /// 默认为 5 秒。
    /// </remarks>
    public decimal FallbackTimeoutSeconds { get; init; } = 5m;

    /// <summary>
    /// TCP相关配置
    /// </summary>
    public TcpConnectionOptions Tcp { get; init; } = new();

    /// <summary>
    /// MQTT相关配置
    /// </summary>
    public MqttConnectionOptions Mqtt { get; init; } = new();

    /// <summary>
    /// SignalR相关配置
    /// </summary>
    public SignalRConnectionOptions SignalR { get; init; } = new();
}

/// <summary>
/// TCP协议配置选项
/// </summary>
public record TcpConnectionOptions
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
    public int ReceiveBufferSize { get; init; } = 8192;

    /// <summary>
    /// TCP发送缓冲区大小（字节）
    /// </summary>
    /// <remarks>
    /// 默认8KB
    /// </remarks>
    public int SendBufferSize { get; init; } = 8192;

    /// <summary>
    /// 是否启用Nagle算法
    /// </summary>
    /// <remarks>
    /// 默认false（禁用Nagle）以降低延迟
    /// 启用Nagle可以提高网络利用率，但增加延迟
    /// </remarks>
    public bool NoDelay { get; init; } = true;

    /// <summary>
    /// 是否启用TCP KeepAlive
    /// </summary>
    /// <remarks>
    /// 默认true。启用后可防止TCP连接空闲超时断线
    /// </remarks>
    public bool EnableKeepAlive { get; init; } = true;

    /// <summary>
    /// TCP KeepAlive时间间隔（秒）
    /// </summary>
    /// <remarks>
    /// 默认60秒。在连接空闲多久后开始发送KeepAlive探测包
    /// 仅在 EnableKeepAlive = true 时有效
    /// </remarks>
    public int KeepAliveTime { get; init; } = 60;

    /// <summary>
    /// TCP KeepAlive探测间隔（秒）
    /// </summary>
    /// <remarks>
    /// 默认10秒。每次KeepAlive探测包之间的间隔
    /// 仅在 EnableKeepAlive = true 时有效
    /// </remarks>
    public int KeepAliveInterval { get; init; } = 10;

    /// <summary>
    /// TCP KeepAlive重试次数
    /// </summary>
    /// <remarks>
    /// 默认3次。连续多少次探测失败后判定连接断开
    /// 仅在 EnableKeepAlive = true 时有效
    /// 注意：Windows系统可能不完全支持此参数
    /// </remarks>
    public int KeepAliveRetryCount { get; init; } = 3;
}

/// <summary>
/// SignalR协议配置选项
/// </summary>
public record SignalRConnectionOptions
{
    /// <summary>
    /// 握手超时时间（秒）
    /// </summary>
    /// <remarks>
    /// 默认15秒
    /// </remarks>
    public int HandshakeTimeout { get; init; } = 15;

    /// <summary>
    /// 保持连接超时时间（秒）
    /// </summary>
    /// <remarks>
    /// 默认30秒。服务端发送心跳包的间隔
    /// </remarks>
    public int KeepAliveInterval { get; init; } = 30;

    /// <summary>
    /// 服务端超时时间（秒）
    /// </summary>
    /// <remarks>
    /// 默认60秒。未收到服务端心跳的最大等待时间
    /// </remarks>
    public int ServerTimeout { get; init; } = 60;

    /// <summary>
    /// 重连间隔（毫秒）
    /// </summary>
    /// <remarks>
    /// 默认null（使用默认策略）。可设置固定重连间隔，如 [0, 2000, 5000, 10000]
    /// </remarks>
    public int[]? ReconnectIntervals { get; init; }

    /// <summary>
    /// 是否跳过协商（直接使用WebSocket）
    /// </summary>
    /// <remarks>
    /// 默认false。设为true可减少连接延迟，但需要服务端支持
    /// </remarks>
    public bool SkipNegotiation { get; init; } = false;
}

/// <summary>
/// MQTT协议配置选项
/// </summary>
/// <remarks>
/// PR-CONFIG-HOTRELOAD02: 用于运行时Options模式的不可变配置。
/// 与 MqttConfig（持久化配置）在结构上相似但用途不同。
/// </remarks>
public record MqttConnectionOptions
{
    /// <summary>
    /// 配置类型标识（用于区分结构相似的类型）
    /// </summary>
    /// <remarks>
    /// PR-CONFIG-HOTRELOAD02: 此属性用于区分 MqttConnectionOptions 和 MqttConfig。
    /// 两者是不同层次的配置模型：
    /// - MqttConfig: 持久化配置（LiteDB），可变（class with set）
    /// - MqttConnectionOptions: 运行时Options（内存），不可变（record with init）
    /// </remarks>
    public string ConfigTypeMarker { get; init; } = "MqttConnectionOptions";
    
    /// <summary>
    /// MQTT服务质量等级
    /// </summary>
    /// <remarks>
    /// 0 = At most once (最多一次，可能丢失)
    /// 1 = At least once (至少一次，可能重复) - 默认
    /// 2 = Exactly once (恰好一次，最可靠但最慢)
    /// </remarks>
    public int QualityOfServiceLevel { get; init; } = 1;

    /// <summary>
    /// 是否使用Clean Session
    /// </summary>
    /// <remarks>
    /// true = 不保留会话状态（默认）
    /// false = 保留会话状态和订阅
    /// </remarks>
    public bool CleanSession { get; init; } = true;

    /// <summary>
    /// 会话保持时间（秒）
    /// </summary>
    /// <remarks>
    /// 默认3600秒（1小时）。0表示连接断开后立即清理会话
    /// </remarks>
    public int SessionExpiryInterval { get; init; } = 3600;

    /// <summary>
    /// 消息保留时间（秒）
    /// </summary>
    /// <remarks>
    /// 默认0（不保留）。用于保留最后一条消息给新订阅者
    /// </remarks>
    public int MessageExpiryInterval { get; init; } = 0;

    /// <summary>
    /// MQTT客户端ID前缀
    /// </summary>
    /// <remarks>
    /// 默认"WheelDiverter"。完整ID为：前缀_GUID
    /// </remarks>
    public string ClientIdPrefix { get; init; } = "WheelDiverter";
}

/// <summary>
/// UpstreamConnectionOptions 校验器
/// </summary>
/// <remarks>
/// <para>实现 IValidateOptions，在应用启动时校验配置合法性。</para>
/// <para>根据不同通信模式校验对应的必填配置项。</para>
/// <para>PR-UPSTREAM01: 移除 HTTP 模式校验。</para>
/// </remarks>
public class UpstreamConnectionOptionsValidator : IValidateOptions<UpstreamConnectionOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, UpstreamConnectionOptions options)
    {
        var errors = new List<string>();

        // 根据通信模式校验必填配置
        switch (options.Mode)
        {
            case CommunicationMode.Tcp:
                if (string.IsNullOrWhiteSpace(options.TcpServer))
                {
                    errors.Add("TCP模式下，TcpServer 不能为空");
                }
                break;

            case CommunicationMode.SignalR:
                if (string.IsNullOrWhiteSpace(options.SignalRHub))
                {
                    errors.Add("SignalR模式下，SignalRHub 不能为空");
                }
                break;

            case CommunicationMode.Mqtt:
                if (string.IsNullOrWhiteSpace(options.MqttBroker))
                {
                    errors.Add("MQTT模式下，MqttBroker 不能为空");
                }
                if (string.IsNullOrWhiteSpace(options.MqttTopic))
                {
                    errors.Add("MQTT模式下，MqttTopic 不能为空");
                }
                break;
        }

        // 校验超时配置
        if (options.TimeoutMs <= 0)
        {
            errors.Add("请求超时时间（TimeoutMs）必须大于0");
        }

        // 校验退避配置
        if (options.InitialBackoffMs <= 0)
        {
            errors.Add("初始退避延迟（InitialBackoffMs）必须大于0");
        }

        if (options.MaxBackoffMs <= 0)
        {
            errors.Add("最大退避延迟（MaxBackoffMs）必须大于0");
        }

        if (options.MaxBackoffMs < options.InitialBackoffMs)
        {
            errors.Add("最大退避延迟（MaxBackoffMs）不能小于初始退避延迟（InitialBackoffMs）");
        }

        if (errors.Count > 0)
        {
            return ValidateOptionsResult.Fail(errors);
        }

        return ValidateOptionsResult.Success;
    }
}
