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
    /// 格口分配超时时间（秒）
    /// </summary>
    /// <remarks>
    /// 当无法通过动态超时计算器获取超时时间时，使用此备用值。
    /// 默认为 5 秒。
    /// </remarks>
    public decimal FallbackTimeoutSeconds { get; init; } = 5m;
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
