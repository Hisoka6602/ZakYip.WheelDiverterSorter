using ZakYip.WheelDiverterSorter.Core.LineModel;

namespace ZakYip.WheelDiverterSorter.Execution.Events;

/// <summary>
/// 路径段执行失败事件参数
/// </summary>
public readonly record struct PathSegmentExecutionFailedEventArgs
{
    /// <summary>
    /// 包裹标识
    /// </summary>
    public required long ParcelId { get; init; }

    /// <summary>
    /// 失败的路径段
    /// </summary>
    public required SwitchingPathSegment FailedSegment { get; init; }

    /// <summary>
    /// 原始目标格口ID
    /// </summary>
    public required int OriginalTargetChuteId { get; init; }

    /// <summary>
    /// 失败原因
    /// </summary>
    public required string FailureReason { get; init; }

    /// <summary>
    /// 失败时间
    /// </summary>
    public required DateTimeOffset FailureTime { get; init; }

    /// <summary>
    /// 失败位置（摆轮ID）
    /// </summary>
    public int FailurePosition => FailedSegment.DiverterId;
}
