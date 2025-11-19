namespace ZakYip.WheelDiverterSorter.Tools.Reporting.Models;

/// <summary>
/// OverloadReason 分布统计
/// OverloadReason distribution statistics
/// </summary>
public record OverloadReasonStatistics
{
    /// <summary>
    /// 超载原因
    /// Overload reason
    /// </summary>
    public required string Reason { get; init; }

    /// <summary>
    /// 该原因出现次数
    /// Count of this reason
    /// </summary>
    public required int Count { get; init; }

    /// <summary>
    /// 占总超载事件的百分比（0-100）
    /// Percentage of total overload events (0-100)
    /// </summary>
    public double Percent { get; init; }
}
