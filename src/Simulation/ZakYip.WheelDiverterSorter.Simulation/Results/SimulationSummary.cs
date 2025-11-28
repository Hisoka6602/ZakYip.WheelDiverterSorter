using ZakYip.WheelDiverterSorter.Core.Enums.Parcel;

namespace ZakYip.WheelDiverterSorter.Simulation.Results;

/// <summary>
/// 仿真汇总统计
/// </summary>
public class SimulationSummary
{
    /// <summary>
    /// 总包裹数
    /// </summary>
    public int TotalParcels { get; set; }

    /// <summary>
    /// 成功分拣到目标格口的数量
    /// </summary>
    public int SortedToTargetChuteCount { get; set; }

    /// <summary>
    /// 超时的数量
    /// </summary>
    public int TimeoutCount { get; set; }

    /// <summary>
    /// 掉包的数量
    /// </summary>
    public int DroppedCount { get; set; }

    /// <summary>
    /// 执行错误的数量
    /// </summary>
    public int ExecutionErrorCount { get; set; }

    /// <summary>
    /// 规则引擎超时的数量
    /// </summary>
    public int RuleEngineTimeoutCount { get; set; }

    /// <summary>
    /// 分拣到错误格口的数量（此值必须始终为 0）
    /// </summary>
    public int SortedToWrongChuteCount { get; set; }

    /// <summary>
    /// 成功率
    /// </summary>
    public double SuccessRate => TotalParcels > 0 
        ? (double)SortedToTargetChuteCount / TotalParcels 
        : 0.0;

    /// <summary>
    /// 总耗时
    /// </summary>
    public TimeSpan TotalDuration { get; set; }

    /// <summary>
    /// 平均每包耗时
    /// </summary>
    public TimeSpan AverageTimePerParcel => TotalParcels > 0 
        ? TimeSpan.FromTicks(TotalDuration.Ticks / TotalParcels) 
        : TimeSpan.Zero;

    /// <summary>
    /// 平均行程时间（从入口到格口）
    /// </summary>
    public TimeSpan? AverageTravelTime { get; set; }

    /// <summary>
    /// 最小行程时间
    /// </summary>
    public TimeSpan? MinTravelTime { get; set; }

    /// <summary>
    /// 最大行程时间
    /// </summary>
    public TimeSpan? MaxTravelTime { get; set; }

    /// <summary>
    /// 格口统计（格口ID -> 分拣数量）
    /// </summary>
    public Dictionary<long, int> ChuteStatistics { get; set; } = new();

    /// <summary>
    /// 状态统计字典（用于调试和详细分析）
    /// </summary>
    public Dictionary<ParcelSimulationStatus, int> StatusStatistics { get; set; } = new();

    /// <summary>
    /// 高密度包裹总数（违反最小安全头距的包裹数量）
    /// </summary>
    public int DenseParcelCount { get; set; }

    /// <summary>
    /// 包裹结果列表（用于详细分析和测试断言）
    /// </summary>
    public List<ParcelSimulationResultEventArgs> Parcels { get; set; } = new();
}
