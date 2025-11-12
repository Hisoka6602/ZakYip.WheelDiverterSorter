namespace ZakYip.WheelDiverterSorter.Core;

/// <summary>
/// 表示一条分拣指令的只读模型
/// </summary>
/// <remarks>
/// 注意：此类代表旧的路由模型，使用简单的目标方向和角度。
/// 新的路径生成方案请使用 ISwitchingPathGenerator 和 SwitchingPath 模型。
/// 当项目完全迁移到新的路径模型后，此类可能被删除或重构。
/// </remarks>
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
