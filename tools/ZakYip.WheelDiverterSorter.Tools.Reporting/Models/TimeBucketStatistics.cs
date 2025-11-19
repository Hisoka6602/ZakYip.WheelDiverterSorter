namespace ZakYip.WheelDiverterSorter.Tools.Reporting.Models;

/// <summary>
/// 时间片维度统计结果
/// Time bucket dimension statistics result
/// </summary>
public record TimeBucketStatistics
{
    /// <summary>
    /// 时间片起始时间
    /// Bucket start time
    /// </summary>
    public required DateTimeOffset BucketStart { get; init; }

    /// <summary>
    /// 时间片结束时间
    /// Bucket end time
    /// </summary>
    public required DateTimeOffset BucketEnd { get; init; }

    /// <summary>
    /// 总包裹数（包含正常 + 异常）
    /// Total parcels (including normal + exception)
    /// </summary>
    public required int TotalParcels { get; init; }

    /// <summary>
    /// 异常口数量（Stage = ExceptionDiverted）
    /// Exception parcels count (Stage = ExceptionDiverted)
    /// </summary>
    public required int ExceptionParcels { get; init; }

    /// <summary>
    /// Overload 触发数量（Stage = OverloadDecision）
    /// Overload events count (Stage = OverloadDecision)
    /// </summary>
    public required int OverloadEvents { get; init; }

    /// <summary>
    /// 异常比例（0-1）
    /// Exception ratio (0-1)
    /// </summary>
    public double ExceptionRatio => TotalParcels > 0 ? (double)ExceptionParcels / TotalParcels : 0;

    /// <summary>
    /// Overload 比例（0-1）
    /// Overload ratio (0-1)
    /// </summary>
    public double OverloadRatio => TotalParcels > 0 ? (double)OverloadEvents / TotalParcels : 0;
}
