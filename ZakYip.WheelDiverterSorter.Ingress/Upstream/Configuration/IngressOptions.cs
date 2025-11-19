namespace ZakYip.WheelDiverterSorter.Ingress.Upstream.Configuration;

/// <summary>
/// 上游通道配置
/// </summary>
public class UpstreamChannelConfig
{
    /// <summary>
    /// 通道名称
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// 通道类型（HTTP, SignalR, MQTT等）
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// 优先级（数值越小优先级越高）
    /// </summary>
    public int Priority { get; init; } = 100;

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// 端点地址
    /// </summary>
    public string? Endpoint { get; init; }

    /// <summary>
    /// 超时时间（毫秒）
    /// </summary>
    public int TimeoutMs { get; init; } = 5000;

    /// <summary>
    /// 重试次数
    /// </summary>
    public int RetryCount { get; init; } = 3;

    /// <summary>
    /// 重试延迟（毫秒）
    /// </summary>
    public int RetryDelayMs { get; init; } = 1000;

    /// <summary>
    /// 附加配置
    /// </summary>
    public Dictionary<string, string>? AdditionalConfig { get; init; }
}

/// <summary>
/// Ingress 配置选项
/// </summary>
public class IngressOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "Ingress";

    /// <summary>
    /// 上游通道配置列表
    /// </summary>
    public List<UpstreamChannelConfig> UpstreamChannels { get; init; } = new();

    /// <summary>
    /// 默认超时时间（毫秒）
    /// </summary>
    public int DefaultTimeoutMs { get; init; } = 5000;

    /// <summary>
    /// 是否启用降级策略
    /// </summary>
    public bool EnableFallback { get; init; } = true;

    /// <summary>
    /// 降级格口ID
    /// </summary>
    public int FallbackChuteId { get; init; } = 999;

    /// <summary>
    /// 是否启用熔断器
    /// </summary>
    public bool EnableCircuitBreaker { get; init; } = true;

    /// <summary>
    /// 熔断器失败阈值
    /// </summary>
    public int CircuitBreakerFailureThreshold { get; init; } = 5;

    /// <summary>
    /// 熔断器超时时间（秒）
    /// </summary>
    public int CircuitBreakerTimeoutSeconds { get; init; } = 30;
}
