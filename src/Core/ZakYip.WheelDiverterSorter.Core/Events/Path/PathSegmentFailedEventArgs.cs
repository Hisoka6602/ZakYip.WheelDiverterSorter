using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Enums.Sorting;

namespace ZakYip.WheelDiverterSorter.Core.Events.Path;

/// <summary>
/// 路径段失败事件参数
/// Path segment failed event arguments
/// </summary>
public record class PathSegmentFailedEventArgs
{
    /// <summary>
    /// 包裹标识
    /// Parcel ID
    /// </summary>
    public required long ParcelId { get; init; }

    /// <summary>
    /// 当前节点ID（失败的摆轮ID）
    /// Current node ID (failed diverter ID)
    /// </summary>
    public required long CurrentNodeId { get; init; }

    /// <summary>
    /// 失败原因
    /// Failure reason
    /// </summary>
    public required PathFailureReason FailureReason { get; init; }

    /// <summary>
    /// 发生时间
    /// Occurred at
    /// </summary>
    public required DateTimeOffset OccurredAt { get; init; }

    /// <summary>
    /// 路径段序号（可选）
    /// Segment sequence number (optional)
    /// </summary>
    public int? SegmentSequence { get; init; }

    /// <summary>
    /// 原始目标格口ID（可选）
    /// Original target chute ID (optional)
    /// </summary>
    public long? OriginalTargetChuteId { get; init; }

    /// <summary>
    /// 附加详情（可选）
    /// Additional details (optional)
    /// </summary>
    public string? Details { get; init; }
}
