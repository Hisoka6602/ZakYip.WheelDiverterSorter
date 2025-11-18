namespace ZakYip.Sorting.Core.Models;

/// <summary>
/// 放包节流配置
/// Release throttle configuration
/// </summary>
public class ReleaseThrottleConfiguration
{
    /// <summary>
    /// 警告级别延迟阈值（毫秒）
    /// Warning level latency threshold in milliseconds
    /// </summary>
    public int WarningThresholdLatencyMs { get; set; } = 5000;

    /// <summary>
    /// 严重级别延迟阈值（毫秒）
    /// Severe level latency threshold in milliseconds
    /// </summary>
    public int SevereThresholdLatencyMs { get; set; } = 10000;

    /// <summary>
    /// 警告级别成功率阈值（0.0-1.0）
    /// Warning level success rate threshold (0.0-1.0)
    /// </summary>
    public double WarningThresholdSuccessRate { get; set; } = 0.9;

    /// <summary>
    /// 严重级别成功率阈值（0.0-1.0）
    /// Severe level success rate threshold (0.0-1.0)
    /// </summary>
    public double SevereThresholdSuccessRate { get; set; } = 0.7;

    /// <summary>
    /// 警告级别在途包裹数阈值
    /// Warning level in-flight parcels threshold
    /// </summary>
    public int WarningThresholdInFlightParcels { get; set; } = 50;

    /// <summary>
    /// 严重级别在途包裹数阈值
    /// Severe level in-flight parcels threshold
    /// </summary>
    public int SevereThresholdInFlightParcels { get; set; } = 100;

    /// <summary>
    /// 正常状态下的放包间隔（毫秒）
    /// Normal state release interval in milliseconds
    /// </summary>
    public int NormalReleaseIntervalMs { get; set; } = 300;

    /// <summary>
    /// 警告状态下的放包间隔（毫秒）
    /// Warning state release interval in milliseconds
    /// </summary>
    public int WarningReleaseIntervalMs { get; set; } = 500;

    /// <summary>
    /// 严重状态下的放包间隔（毫秒）
    /// Severe state release interval in milliseconds
    /// </summary>
    public int SevereReleaseIntervalMs { get; set; } = 1000;

    /// <summary>
    /// 是否在严重拥堵时暂停放包
    /// Whether to pause release on severe congestion
    /// </summary>
    public bool ShouldPauseOnSevere { get; set; } = false;

    /// <summary>
    /// 启用节流功能
    /// Enable throttling feature
    /// </summary>
    public bool EnableThrottling { get; set; } = true;

    /// <summary>
    /// 指标采样时间窗口（秒）
    /// Metrics sampling time window in seconds
    /// </summary>
    public int MetricsTimeWindowSeconds { get; set; } = 60;
}
