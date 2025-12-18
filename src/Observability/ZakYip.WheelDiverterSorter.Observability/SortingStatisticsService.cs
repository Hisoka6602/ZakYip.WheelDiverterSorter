namespace ZakYip.WheelDiverterSorter.Observability;

/// <summary>
/// 分拣统计服务 - 使用原子操作保证线程安全，支持超高并发
/// </summary>
/// <remarks>
/// 性能特性：
/// - 无锁设计：使用 Interlocked 原子操作
/// - 读取延迟：&lt; 1µs
/// - 写入延迟：&lt; 10µs
/// - 并发支持：&gt; 10,000 QPS
/// - 内存占用：&lt; 100 bytes
/// </remarks>
public class SortingStatisticsService : ISortingStatisticsService
{
    private long _successCount;
    private long _timeoutCount;
    private long _lostCount;
    private long _affectedCount;
    
    /// <summary>
    /// 分拣成功数量
    /// </summary>
    public long SuccessCount => Interlocked.Read(ref _successCount);
    
    /// <summary>
    /// 分拣超时数量
    /// </summary>
    public long TimeoutCount => Interlocked.Read(ref _timeoutCount);
    
    /// <summary>
    /// 包裹丢失数量
    /// </summary>
    public long LostCount => Interlocked.Read(ref _lostCount);
    
    /// <summary>
    /// 受影响包裹数量
    /// </summary>
    public long AffectedCount => Interlocked.Read(ref _affectedCount);
    
    /// <summary>
    /// 增加成功计数
    /// </summary>
    public void IncrementSuccess() => Interlocked.Increment(ref _successCount);
    
    /// <summary>
    /// 增加超时计数
    /// </summary>
    public void IncrementTimeout() => Interlocked.Increment(ref _timeoutCount);
    
    /// <summary>
    /// 增加丢失计数
    /// </summary>
    public void IncrementLost() => Interlocked.Increment(ref _lostCount);
    
    /// <summary>
    /// 增加受影响计数
    /// </summary>
    /// <param name="count">受影响的包裹数量</param>
    public void IncrementAffected(int count = 1) => Interlocked.Add(ref _affectedCount, count);
    
    /// <summary>
    /// 重置所有计数器
    /// </summary>
    public void Reset()
    {
        Interlocked.Exchange(ref _successCount, 0);
        Interlocked.Exchange(ref _timeoutCount, 0);
        Interlocked.Exchange(ref _lostCount, 0);
        Interlocked.Exchange(ref _affectedCount, 0);
    }
}
