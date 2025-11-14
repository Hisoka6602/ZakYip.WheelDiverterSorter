using ZakYip.WheelDiverterSorter.Core;

namespace ZakYip.WheelDiverterSorter.Execution.Events;

/// <summary>
/// 路径执行失败事件参数
/// </summary>
public class PathExecutionFailedEventArgs : EventArgs
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
    /// 预期到达的格口ID（通常是异常格口/FallbackChuteId）
    /// </summary>
    /// <remarks>
    /// 注意：此字段表示路径执行失败后包裹预期到达的格口（通常是FallbackChuteId），
    /// 而不是包裹实际物理到达的位置。在没有自动故障恢复的情况下，
    /// 包裹实际会停留在失败点或流向输送带末端的默认异常格口。
    /// </remarks>
    public required int ActualChuteId { get; init; }
}
