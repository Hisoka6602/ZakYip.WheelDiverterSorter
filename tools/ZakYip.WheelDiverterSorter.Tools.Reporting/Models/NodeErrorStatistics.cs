namespace ZakYip.WheelDiverterSorter.Tools.Reporting.Models;

/// <summary>
/// 按节点的异常热点统计
/// Exception hotspot statistics by node
/// </summary>
public record NodeErrorStatistics
{
    /// <summary>
    /// 节点 ID
    /// Node ID
    /// </summary>
    public required string NodeId { get; init; }

    /// <summary>
    /// 该节点相关的异常/Overload 事件次数
    /// Related exception/overload event count for this node
    /// </summary>
    public required int EventCount { get; init; }

    /// <summary>
    /// 占全部事件的百分比（0-100）
    /// Percentage of total events (0-100)
    /// </summary>
    public double Percent { get; init; }
}
