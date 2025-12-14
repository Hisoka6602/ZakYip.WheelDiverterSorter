namespace ZakYip.WheelDiverterSorter.Host.Models;

/// <summary>
/// 分拣统计数据DTO
/// </summary>
public record SortingStatisticsDto
{
    /// <summary>
    /// 分拣成功数量
    /// </summary>
    /// <example>12345</example>
    public required long SuccessCount { get; init; }
    
    /// <summary>
    /// 分拣超时数量（包裹延迟但仍导向异常口）
    /// </summary>
    /// <example>23</example>
    public required long TimeoutCount { get; init; }
    
    /// <summary>
    /// 包裹丢失数量（包裹物理丢失，从队列删除）
    /// </summary>
    /// <example>5</example>
    public required long LostCount { get; init; }
    
    /// <summary>
    /// 受影响包裹数量（因其他包裹丢失而被重路由到异常口）
    /// </summary>
    /// <example>8</example>
    public required long AffectedCount { get; init; }
    
    /// <summary>
    /// 统计时间戳
    /// </summary>
    /// <example>2025-12-14T12:00:00Z</example>
    public required DateTime Timestamp { get; init; }
}
