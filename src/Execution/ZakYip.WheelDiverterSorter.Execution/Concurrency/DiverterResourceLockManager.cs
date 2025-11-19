using System.Collections.Concurrent;

namespace ZakYip.WheelDiverterSorter.Execution.Concurrency;

/// <summary>
/// 摆轮资源锁管理器接口
/// </summary>
/// <remarks>
/// 负责为每个摆轮创建和管理独立的锁实例
/// </remarks>
public interface IDiverterResourceLockManager
{
    /// <summary>
    /// 获取指定摆轮的资源锁
    /// </summary>
    /// <param name="diverterId">摆轮ID（数字ID）</param>
    /// <returns>摆轮资源锁</returns>
    IDiverterResourceLock GetLock(int diverterId);
}

/// <summary>
/// 摆轮资源锁管理器实现
/// </summary>
public class DiverterResourceLockManager : IDiverterResourceLockManager, IDisposable
{
    private readonly ConcurrentDictionary<int, DiverterResourceLock> _locks;
    private bool _disposed;

    /// <summary>
    /// 初始化摆轮资源锁管理器
    /// </summary>
    public DiverterResourceLockManager()
    {
        _locks = new ConcurrentDictionary<int, DiverterResourceLock>();
    }

    /// <inheritdoc/>
    public IDiverterResourceLock GetLock(int diverterId)
    {
        if (diverterId <= 0)
        {
            throw new ArgumentException("摆轮ID必须大于0", nameof(diverterId));
        }

        return _locks.GetOrAdd(diverterId, id => new DiverterResourceLock(id.ToString()));
    }

    /// <summary>
    /// 释放所有资源锁
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        foreach (var lockInstance in _locks.Values)
        {
            lockInstance?.Dispose();
        }

        _locks.Clear();
        _disposed = true;
    }
}
