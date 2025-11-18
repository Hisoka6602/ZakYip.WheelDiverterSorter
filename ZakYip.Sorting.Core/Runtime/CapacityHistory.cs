namespace ZakYip.Sorting.Core.Runtime;

/// <summary>
/// 产能历史数据：用于ICapacityEstimator根据历史测试数据估算安全产能区间。
/// </summary>
public readonly record struct CapacityHistory
{
    /// <summary>
    /// 不同放包间隔下的测试结果列表。
    /// </summary>
    public IReadOnlyList<CapacityTestResult> TestResults { get; init; }
}

/// <summary>
/// 单次产能测试结果。
/// </summary>
public readonly record struct CapacityTestResult
{
    /// <summary>
    /// 放包间隔（毫秒）。
    /// </summary>
    public int IntervalMs { get; init; }

    /// <summary>
    /// 测试包裹数量。
    /// </summary>
    public int ParcelCount { get; init; }

    /// <summary>
    /// 成功率（0.0 ~ 1.0）。
    /// </summary>
    public double SuccessRate { get; init; }

    /// <summary>
    /// 平均延迟（毫秒）。
    /// </summary>
    public double AverageLatencyMs { get; init; }

    /// <summary>
    /// 最大延迟（毫秒）。
    /// </summary>
    public double MaxLatencyMs { get; init; }

    /// <summary>
    /// 异常口比例（0.0 ~ 1.0）。
    /// </summary>
    public double ExceptionRate { get; init; }

    /// <summary>
    /// 超载处置触发次数。
    /// </summary>
    public int OverloadTriggerCount { get; init; }
}
