using ZakYip.WheelDiverterSorter.Core.Enums.Communication;
using ZakYip.WheelDiverterSorter.Core.Enums.Conveyor;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Enums.Routing;
using ZakYip.WheelDiverterSorter.Core.Enums.Sensors;
using ZakYip.WheelDiverterSorter.Core.Enums.System;

namespace ZakYip.WheelDiverterSorter.Core.LineModel.Events;

/// <summary>
/// 改口被忽略/拒绝事件参数。
/// </summary>
public record struct ChuteChangeIgnoredEventArgs
{
    /// <summary>
    /// 包裹ID
    /// </summary>
    public required long ParcelId { get; init; }

    /// <summary>
    /// 请求的格口ID
    /// </summary>
    public required int RequestedChuteId { get; init; }

    /// <summary>
    /// 被忽略/拒绝的原因
    /// </summary>
    public required ChuteChangeOutcome Outcome { get; init; }

    /// <summary>
    /// 发生时间
    /// </summary>
    public required DateTimeOffset OccurredAt { get; init; }

    /// <summary>
    /// 可选的详细原因说明
    /// </summary>
    public string? Reason { get; init; }
}
