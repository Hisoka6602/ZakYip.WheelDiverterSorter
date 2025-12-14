namespace ZakYip.WheelDiverterSorter.Core.Events.Queue;

/// <summary>
/// 包裹丢失事件参数
/// </summary>
/// <remarks>
/// 当包裹在传输过程中物理丢失（不在分拣设备上）时触发。
/// 与超时不同，丢失的包裹无法导向异常口，需要从队列中删除以避免影响后续包裹分拣。
/// PR-CODING-STANDARDS: 转换为 sealed record class（事件载荷统一使用 record）
/// </remarks>
public sealed record class ParcelLostEventArgs
{
    /// <summary>
    /// 丢失的包裹ID
    /// </summary>
    public required long LostParcelId { get; init; }
    
    /// <summary>
    /// 检测到丢失的位置索引
    /// </summary>
    public required int DetectedAtPositionIndex { get; init; }
    
    /// <summary>
    /// 检测到丢失的时间
    /// </summary>
    public required DateTime DetectedAt { get; init; }
    
    /// <summary>
    /// 包裹创建时间
    /// </summary>
    public DateTime? ParcelCreatedAt { get; init; }
    
    /// <summary>
    /// 预期到达时间
    /// </summary>
    public DateTime? ExpectedArrivalTime { get; init; }
    
    /// <summary>
    /// 延迟时间（毫秒）
    /// </summary>
    /// <remarks>
    /// DelayMs = DetectedAt - ExpectedArrivalTime
    /// </remarks>
    public double DelayMs { get; init; }
    
    /// <summary>
    /// 总存活时间（毫秒）
    /// </summary>
    /// <remarks>
    /// TotalLifetimeMs = DetectedAt - ParcelCreatedAt
    /// </remarks>
    public double? TotalLifetimeMs { get; init; }
    
    /// <summary>
    /// 从所有队列中移除的任务数量
    /// </summary>
    public int TotalTasksRemoved { get; init; }
    
    /// <summary>
    /// 使用的丢失判定阈值（毫秒）
    /// </summary>
    public double LostThresholdMs { get; init; }
    
    /// <summary>
    /// 使用的中位数间隔（毫秒）
    /// </summary>
    /// <remarks>
    /// 来自 PositionIntervalTracker 的统计数据
    /// </remarks>
    public double? MedianIntervalMs { get; init; }
    
    /// <summary>
    /// 使用的丢失判定系数
    /// </summary>
    /// <remarks>
    /// LostThresholdMs = MedianIntervalMs * LostDetectionFactor
    /// </remarks>
    public decimal LostDetectionFactor { get; init; }
    
    /// <summary>
    /// 受丢失包裹影响的其他包裹ID列表
    /// </summary>
    /// <remarks>
    /// 这些包裹在丢失包裹创建之后、丢失检测之前创建，其任务方向已被改为直行以导向异常格口
    /// </remarks>
    public IReadOnlyList<long> AffectedParcelIds { get; init; } = Array.Empty<long>();
}
