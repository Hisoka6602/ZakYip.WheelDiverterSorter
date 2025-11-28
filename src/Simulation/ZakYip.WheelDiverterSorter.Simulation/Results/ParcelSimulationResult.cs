using ZakYip.WheelDiverterSorter.Core.Enums.Parcel;

namespace ZakYip.WheelDiverterSorter.Simulation.Results;

/// <summary>
/// 包裹仿真结果事件参数
/// </summary>
public record struct ParcelSimulationResultEventArgs
{
    /// <summary>
    /// 包裹ID
    /// </summary>
    public long ParcelId { get; init; }

    /// <summary>
    /// 目标格口ID（规则引擎返回的期望格口）
    /// </summary>
    public long? TargetChuteId { get; init; }

    /// <summary>
    /// 实际格口ID（实际检测到包裹通过的格口）
    /// </summary>
    public long? FinalChuteId { get; init; }

    /// <summary>
    /// 包裹仿真状态
    /// </summary>
    public ParcelSimulationStatus Status { get; init; }

    /// <summary>
    /// 行程时间（从入口传感器到最终格口的总时间）
    /// </summary>
    public TimeSpan? TravelTime { get; init; }

    /// <summary>
    /// 是否超时
    /// </summary>
    public bool IsTimeout { get; init; }

    /// <summary>
    /// 是否掉包
    /// </summary>
    public bool IsDropped { get; init; }

    /// <summary>
    /// 掉包位置（如果发生掉包）
    /// </summary>
    public string? DropoutLocation { get; init; }

    /// <summary>
    /// 失败原因（如果失败）
    /// </summary>
    public string? FailureReason { get; init; }

    /// <summary>
    /// 是否为高密度包裹（违反最小安全头距）
    /// </summary>
    public bool IsDenseParcel { get; init; }

    /// <summary>
    /// 与前一包裹的时间间隔（头距时间）
    /// </summary>
    public TimeSpan? HeadwayTime { get; init; }

    /// <summary>
    /// 与前一包裹的空间间隔（头距距离，单位：mm）
    /// </summary>
    public decimal? HeadwayMm { get; init; }
}
