namespace ZakYip.WheelDiverterSorter.Core.LineModel.Segments;

/// <summary>
/// 表示一条输送段的只读模型
/// </summary>
public record class ConveyorSection
{
    /// <summary>
    /// 输送段标识
    /// </summary>
    public required string SectionId { get; init; }

    /// <summary>
    /// 输送段长度（毫米）
    /// </summary>
    public required double LengthMm { get; init; }

    /// <summary>
    /// 最大速度（毫米每秒）
    /// </summary>
    public required double MaxSpeedMmps { get; init; }
}
