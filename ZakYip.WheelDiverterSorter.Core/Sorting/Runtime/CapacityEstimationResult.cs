namespace ZakYip.WheelDiverterSorter.Core.Sorting.Runtime;

/// <summary>
/// 产能估算结果：推荐的安全产能区间和危险区间。
/// </summary>
public readonly record struct CapacityEstimationResult
{
    /// <summary>
    /// 推荐安全区间：最小包裹/分钟。
    /// </summary>
    public double SafeMinParcelsPerMinute { get; init; }

    /// <summary>
    /// 推荐安全区间：最大包裹/分钟。
    /// </summary>
    public double SafeMaxParcelsPerMinute { get; init; }

    /// <summary>
    /// 危险区间：超过此值时，多数包裹变为异常。
    /// </summary>
    public double DangerousThresholdParcelsPerMinute { get; init; }

    /// <summary>
    /// 估算置信度（0.0 ~ 1.0）。
    /// </summary>
    public double Confidence { get; init; }

    /// <summary>
    /// 估算依据的测试数据点数量。
    /// </summary>
    public int DataPointCount { get; init; }
}
