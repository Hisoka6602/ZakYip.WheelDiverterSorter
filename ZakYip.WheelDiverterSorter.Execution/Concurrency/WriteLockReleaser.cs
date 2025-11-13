namespace ZakYip.WheelDiverterSorter.Execution.Concurrency;

/// <summary>
/// 写锁释放器
/// </summary>
internal class WriteLockReleaser : IDisposable
{
    private readonly ReaderWriterLockSlim _lock;
    private bool _disposed;

    public WriteLockReleaser(ReaderWriterLockSlim lockSlim)
    {
        _lock = lockSlim;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _lock.ExitWriteLock();
        _disposed = true;
    }
}
