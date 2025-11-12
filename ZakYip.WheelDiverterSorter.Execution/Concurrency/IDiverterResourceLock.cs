namespace ZakYip.WheelDiverterSorter.Execution.Concurrency;

/// <summary>
/// 摆轮资源锁接口，用于控制对单个摆轮的并发访问
/// </summary>
/// <remarks>
/// 使用读写锁机制，支持多个读操作同时进行，但写操作是互斥的。
/// 在摆轮分拣场景中：
/// - 读锁：查询摆轮状态
/// - 写锁：设置摆轮角度（实际控制摆轮动作）
/// </remarks>
public interface IDiverterResourceLock
{
    /// <summary>
    /// 获取摆轮ID
    /// </summary>
    string DiverterId { get; }

    /// <summary>
    /// 异步获取写锁（独占锁）
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>释放锁的句柄，使用完毕后应该释放</returns>
    Task<IDisposable> AcquireWriteLockAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步获取读锁（共享锁）
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>释放锁的句柄，使用完毕后应该释放</returns>
    Task<IDisposable> AcquireReadLockAsync(CancellationToken cancellationToken = default);
}
