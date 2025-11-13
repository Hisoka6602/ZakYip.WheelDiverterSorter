namespace ZakYip.WheelDiverterSorter.Execution.Concurrency;

/// <summary>
/// 摆轮资源锁实现，基于ReaderWriterLockSlim
/// </summary>
public class DiverterResourceLock : IDiverterResourceLock, IDisposable
{
    private readonly ReaderWriterLockSlim _lock;
    private bool _disposed;

    /// <summary>
    /// 初始化摆轮资源锁
    /// </summary>
    /// <param name="diverterId">摆轮ID</param>
    public DiverterResourceLock(string diverterId)
    {
        DiverterId = diverterId ?? throw new ArgumentNullException(nameof(diverterId));
        _lock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
    }

    /// <inheritdoc/>
    public string DiverterId { get; }

    /// <inheritdoc/>
    public async Task<IDisposable> AcquireWriteLockAsync(CancellationToken cancellationToken = default)
    {
        // 在异步上下文中获取锁
        await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            _lock.EnterWriteLock();
        }, cancellationToken).ConfigureAwait(false);

        return new WriteLockReleaser(_lock);
    }

    /// <inheritdoc/>
    public async Task<IDisposable> AcquireReadLockAsync(CancellationToken cancellationToken = default)
    {
        // 在异步上下文中获取锁
        await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            _lock.EnterReadLock();
        }, cancellationToken).ConfigureAwait(false);

        return new ReadLockReleaser(_lock);
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        
        _lock?.Dispose();
        _disposed = true;
    }
}

/// <summary>
/// 写锁释放器
/// </summary>
file readonly struct WriteLockReleaser : IDisposable
{
    private readonly ReaderWriterLockSlim _lock;

    public WriteLockReleaser(ReaderWriterLockSlim lockSlim)
    {
        _lock = lockSlim;
    }

    public readonly void Dispose()
    {
        _lock.ExitWriteLock();
    }
}

/// <summary>
/// 读锁释放器
/// </summary>
file readonly struct ReadLockReleaser : IDisposable
{
    private readonly ReaderWriterLockSlim _lock;

    public ReadLockReleaser(ReaderWriterLockSlim lockSlim)
    {
        _lock = lockSlim;
    }

    public readonly void Dispose()
    {
        _lock.ExitReadLock();
    }
}
