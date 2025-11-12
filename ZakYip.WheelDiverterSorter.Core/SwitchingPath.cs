namespace ZakYip.WheelDiverterSorter.Core;

/// <summary>
/// 表示从入口到目标格口的完整摆轮路径
/// </summary>
public record class SwitchingPath
{
    /// <summary>
    /// 目标格口标识
    /// </summary>
    public required string TargetChuteId { get; init; }

    /// <summary>
    /// 路径中的所有摆轮段，按顺序排列
    /// </summary>
    public required IReadOnlyList<SwitchingPathSegment> Segments { get; init; }

    /// <summary>
    /// 路径生成时间
    /// </summary>
    public required DateTimeOffset GeneratedAt { get; init; }
}
