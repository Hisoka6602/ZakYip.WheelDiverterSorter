namespace ZakYip.WheelDiverterSorter.Execution.Concurrency;

/// <summary>
/// 读锁释放器
/// </summary>
internal class ReadLockReleaser : IDisposable
{
    private readonly ReaderWriterLockSlim _lock;
    private bool _disposed;

    public ReadLockReleaser(ReaderWriterLockSlim lockSlim)
    {
        _lock = lockSlim;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _lock.ExitReadLock();
        _disposed = true;
    }
}
