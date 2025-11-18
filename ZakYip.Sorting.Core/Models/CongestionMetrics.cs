namespace ZakYip.Sorting.Core.Models;

/// <summary>
/// 拥堵指标
/// Metrics used for congestion detection
/// </summary>
public class CongestionMetrics
{
    /// <summary>
    /// 当前在途包裹数（已进入线体但未落格）
    /// Current number of in-flight parcels (entered but not yet sorted)
    /// </summary>
    public int InFlightParcels { get; set; }

    /// <summary>
    /// 最近时间窗口内的成功率（0.0 到 1.0）
    /// Success rate in recent time window (0.0 to 1.0)
    /// </summary>
    public double SuccessRate { get; set; }

    /// <summary>
    /// 平均延迟（毫秒）
    /// Average latency in milliseconds
    /// </summary>
    public double AverageLatencyMs { get; set; }

    /// <summary>
    /// 最大延迟（毫秒）
    /// Maximum latency in milliseconds
    /// </summary>
    public double MaxLatencyMs { get; set; }

    /// <summary>
    /// 采样时间窗口（秒）
    /// Sampling time window in seconds
    /// </summary>
    public int TimeWindowSeconds { get; set; }

    /// <summary>
    /// 采样的包裹总数
    /// Total number of parcels sampled
    /// </summary>
    public int TotalParcels { get; set; }

    /// <summary>
    /// 失败的包裹数
    /// Number of failed parcels
    /// </summary>
    public int FailedParcels { get; set; }
}
