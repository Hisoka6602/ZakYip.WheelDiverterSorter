namespace ZakYip.WheelDiverterSorter.Tools.Reporting.Models;

/// <summary>
/// 按格口的异常热点统计
/// Exception hotspot statistics by chute
/// </summary>
public record ChuteErrorStatistics
{
    /// <summary>
    /// 格口 ID
    /// Chute ID
    /// </summary>
    public required string ChuteId { get; init; }

    /// <summary>
    /// 该格口异常次数
    /// Exception count for this chute
    /// </summary>
    public required int ExceptionCount { get; init; }

    /// <summary>
    /// 占全部异常的百分比（0-100）
    /// Percentage of total exceptions (0-100)
    /// </summary>
    public double Percent { get; init; }
}
