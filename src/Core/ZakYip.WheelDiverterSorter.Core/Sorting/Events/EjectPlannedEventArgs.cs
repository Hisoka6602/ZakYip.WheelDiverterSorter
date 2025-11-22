namespace ZakYip.WheelDiverterSorter.Core.Sorting.Events;

/// <summary>
/// 吐件计划事件载荷，当系统决定在某个节点吐件时触发
/// </summary>
public readonly record struct EjectPlannedEventArgs
{
    /// <summary>
    /// 包裹唯一标识符
    /// </summary>
    public required long ParcelId { get; init; }

    /// <summary>
    /// 计划吐件时间（UTC）
    /// </summary>
    public required DateTimeOffset PlannedAt { get; init; }

    /// <summary>
    /// 计划吐件的摆轮节点ID
    /// </summary>
    public required string NodeId { get; init; }

    /// <summary>
    /// 吐件方向（Left/Right）
    /// </summary>
    public required string Direction { get; init; }

    /// <summary>
    /// 目标格口ID
    /// </summary>
    public long TargetChuteId { get; init; }
}
