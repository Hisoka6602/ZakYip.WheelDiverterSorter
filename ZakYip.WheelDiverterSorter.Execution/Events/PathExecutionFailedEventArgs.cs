using ZakYip.WheelDiverterSorter.Core.LineModel;

namespace ZakYip.WheelDiverterSorter.Execution.Events;

/// <summary>
/// 路径执行失败事件参数
/// </summary>
public readonly record struct PathExecutionFailedEventArgs
{
    /// <summary>
    /// 包裹标识
    /// </summary>
    public required long ParcelId { get; init; }

    /// <summary>
    /// 原始路径
    /// </summary>
    public required SwitchingPath OriginalPath { get; init; }

    /// <summary>
    /// 失败的路径段（如果适用）
    /// </summary>
    public SwitchingPathSegment? FailedSegment { get; init; }

    /// <summary>
    /// 失败原因
    /// </summary>
    public required string FailureReason { get; init; }

    /// <summary>
    /// 失败时间
    /// </summary>
    public required DateTimeOffset FailureTime { get; init; }

    /// <summary>
    /// 实际到达的格口ID（通常是异常格口）
    /// </summary>
    public required int ActualChuteId { get; init; }
}
