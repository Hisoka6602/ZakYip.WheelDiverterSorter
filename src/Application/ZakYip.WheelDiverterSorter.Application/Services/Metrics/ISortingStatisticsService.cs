namespace ZakYip.WheelDiverterSorter.Application.Services.Metrics;

/// <summary>
/// 分拣统计服务接口
/// </summary>
/// <remarks>
/// 提供分拣系统的实时统计数据，包括成功、超时、丢失和受影响的包裹数量
/// </remarks>
public interface ISortingStatisticsService
{
    /// <summary>
    /// 分拣成功数量
    /// </summary>
    long SuccessCount { get; }
    
    /// <summary>
    /// 分拣超时数量（包裹延迟但仍导向异常口）
    /// </summary>
    long TimeoutCount { get; }
    
    /// <summary>
    /// 包裹丢失数量（包裹物理丢失，从队列删除）
    /// </summary>
    long LostCount { get; }
    
    /// <summary>
    /// 受影响包裹数量（因其他包裹丢失而被重路由到异常口）
    /// </summary>
    long AffectedCount { get; }
    
    /// <summary>
    /// 增加成功计数
    /// </summary>
    void IncrementSuccess();
    
    /// <summary>
    /// 增加超时计数
    /// </summary>
    void IncrementTimeout();
    
    /// <summary>
    /// 增加丢失计数
    /// </summary>
    void IncrementLost();
    
    /// <summary>
    /// 增加受影响计数
    /// </summary>
    /// <param name="count">受影响的包裹数量</param>
    void IncrementAffected(int count = 1);
    
    /// <summary>
    /// 重置所有计数器
    /// </summary>
    void Reset();
}
