namespace ZakYip.Sorting.Core.Runtime;

/// <summary>
/// 拥堵检测快照：用于ICongestionDetector评估当前拥堵状况。
/// </summary>
public readonly record struct CongestionSnapshot
{
    /// <summary>
    /// 当前在途包裹数（已进入线体但未完成分拣）。
    /// </summary>
    public int InFlightParcels { get; init; }

    /// <summary>
    /// 最近N秒内平均分拣延迟（毫秒）。
    /// </summary>
    public double AverageLatencyMs { get; init; }

    /// <summary>
    /// 最近N秒内最大分拣延迟（毫秒）。
    /// </summary>
    public double MaxLatencyMs { get; init; }

    /// <summary>
    /// 最近N秒内分拣失败比例（0.0 ~ 1.0）。
    /// </summary>
    public double FailureRatio { get; init; }

    /// <summary>
    /// 采样时间窗口（秒）。
    /// </summary>
    public int TimeWindowSeconds { get; init; }

    /// <summary>
    /// 采样的包裹总数。
    /// </summary>
    public int TotalSampledParcels { get; init; }
}
