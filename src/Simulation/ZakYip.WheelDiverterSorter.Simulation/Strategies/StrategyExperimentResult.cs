using ZakYip.WheelDiverterSorter.Core.Sorting.Overload;
using ZakYip.WheelDiverterSorter.Core.Enums.Sorting;

namespace ZakYip.WheelDiverterSorter.Simulation.Strategies;

/// <summary>
/// 策略实验结果
/// Strategy experiment result
/// </summary>
public sealed record StrategyExperimentResult
{
    /// <summary>
    /// 策略 Profile
    /// Strategy profile
    /// </summary>
    public required StrategyProfile Profile { get; init; }

    /// <summary>
    /// 总包裹数
    /// Total number of parcels
    /// </summary>
    public required int TotalParcels { get; init; }

    /// <summary>
    /// 成功落格数
    /// Number of successfully sorted parcels
    /// </summary>
    public required int SuccessParcels { get; init; }

    /// <summary>
    /// 异常口数
    /// Number of exception chute parcels
    /// </summary>
    public required int ExceptionParcels { get; init; }

    /// <summary>
    /// Overload 事件次数
    /// Number of overload events
    /// </summary>
    public required int OverloadEvents { get; init; }

    /// <summary>
    /// 成功率
    /// Success ratio
    /// </summary>
    public required decimal SuccessRatio { get; init; }

    /// <summary>
    /// 异常率
    /// Exception ratio
    /// </summary>
    public required decimal ExceptionRatio { get; init; }

    /// <summary>
    /// 平均分拣延迟
    /// Average latency
    /// </summary>
    public required TimeSpan AverageLatency { get; init; }

    /// <summary>
    /// 最大分拣延迟
    /// Maximum latency
    /// </summary>
    public required TimeSpan MaxLatency { get; init; }

    /// <summary>
    /// 可选：Overload 原因分布
    /// Optional: Distribution of overload reasons
    /// </summary>
    public Dictionary<OverloadReason, int>? OverloadReasonDistribution { get; init; }
}
