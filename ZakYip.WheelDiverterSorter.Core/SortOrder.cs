namespace ZakYip.WheelDiverterSorter.Core;

/// <summary>
/// 表示一条分拣指令的只读模型
/// </summary>
public record class SortOrder
{
    /// <summary>
    /// 包裹标识
    /// </summary>
    public required long ParcelId { get; init; }

    /// <summary>
    /// 目标分拣方向
    /// </summary>
    public required DiverterSide TargetSide { get; init; }

    /// <summary>
    /// 指令生成时间
    /// </summary>
    public required DateTimeOffset GeneratedAt { get; init; }

    /// <summary>
    /// 可选的摆轮角度
    /// </summary>
    public DiverterAngle? OptionalAngle { get; init; }
}
