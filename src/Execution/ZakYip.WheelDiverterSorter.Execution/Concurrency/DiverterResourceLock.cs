namespace ZakYip.WheelDiverterSorter.Execution.Concurrency;

/// <summary>
/// 摆轮资源锁实现，基于SemaphoreSlim（支持异步操作，无线程亲和性要求）
/// 简化版：所有锁都是排他锁（写锁语义）
/// </summary>
public class DiverterResourceLock : IDiverterResourceLock, IDisposable
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private bool _disposed;

    /// <summary>
    /// 初始化摆轮资源锁
    /// </summary>
    /// <param name="diverterId">摆轮ID</param>
    public DiverterResourceLock(string diverterId)
    {
        DiverterId = diverterId ?? throw new ArgumentNullException(nameof(diverterId));
    }

    /// <inheritdoc/>
    public string DiverterId { get; }

    /// <inheritdoc/>
    public async Task<IDisposable> AcquireWriteLockAsync(CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new LockReleaser(_semaphore);
    }

    /// <inheritdoc/>
    public async Task<IDisposable> AcquireReadLockAsync(CancellationToken cancellationToken = default)
    {
        // 对于摆轮操作，读写锁都是排他的（简化实现）
        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new LockReleaser(_semaphore);
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        
        _semaphore?.Dispose();
        _disposed = true;
    }

    /// <summary>
    /// 锁释放器
    /// </summary>
    private readonly struct LockReleaser : IDisposable
    {
        private readonly SemaphoreSlim _semaphore;

        public LockReleaser(SemaphoreSlim semaphore)
        {
            _semaphore = semaphore;
        }

        public readonly void Dispose()
        {
            _semaphore.Release();
        }
    }
}
