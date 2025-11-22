namespace ZakYip.WheelDiverterSorter.Core.Sorting.Events;

/// <summary>
/// 路径规划完成事件载荷，当系统为包裹生成摆轮路径时触发
/// </summary>
public readonly record struct RoutePlannedEventArgs
{
    /// <summary>
    /// 包裹唯一标识符
    /// </summary>
    public required long ParcelId { get; init; }

    /// <summary>
    /// 目标格口ID
    /// </summary>
    public required long TargetChuteId { get; init; }

    /// <summary>
    /// 路径规划时间（UTC）
    /// </summary>
    public required DateTimeOffset PlannedAt { get; init; }

    /// <summary>
    /// 路径段数量
    /// </summary>
    public int SegmentCount { get; init; }

    /// <summary>
    /// 预估执行时间（毫秒）
    /// </summary>
    public double EstimatedTimeMs { get; init; }

    /// <summary>
    /// 路径是否健康（是否经过降级节点）
    /// </summary>
    public bool IsHealthy { get; init; }

    /// <summary>
    /// 不健康的节点列表（如果有）
    /// </summary>
    public string? UnhealthyNodes { get; init; }
}
