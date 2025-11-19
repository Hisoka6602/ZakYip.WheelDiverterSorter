namespace ZakYip.WheelDiverterSorter.Core.Sorting.Events;

/// <summary>
/// 上游分配事件载荷，当上游系统（RuleEngine）分配目标格口时触发
/// </summary>
public readonly record struct UpstreamAssignedEventArgs
{
    /// <summary>
    /// 包裹唯一标识符
    /// </summary>
    public required long ParcelId { get; init; }

    /// <summary>
    /// 分配的目标格口ID
    /// </summary>
    public required int ChuteId { get; init; }

    /// <summary>
    /// 分配时间（UTC）
    /// </summary>
    public required DateTimeOffset AssignedAt { get; init; }

    /// <summary>
    /// 分配耗时（毫秒）
    /// </summary>
    public double LatencyMs { get; init; }

    /// <summary>
    /// 分配状态（Success/Timeout/Error）
    /// </summary>
    public string Status { get; init; }

    /// <summary>
    /// 分配来源（Upstream/Fixed/RoundRobin）
    /// </summary>
    public string Source { get; init; }
}
