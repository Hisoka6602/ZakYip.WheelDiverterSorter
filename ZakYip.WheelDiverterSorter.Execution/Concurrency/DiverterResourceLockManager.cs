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
    /// <param name="diverterId">摆轮ID</param>
    /// <returns>摆轮资源锁</returns>
    IDiverterResourceLock GetLock(string diverterId);
}

/// <summary>
/// 摆轮资源锁管理器实现
/// </summary>
public class DiverterResourceLockManager : IDiverterResourceLockManager, IDisposable
{
    private readonly ConcurrentDictionary<string, DiverterResourceLock> _locks;
    private bool _disposed;

    /// <summary>
    /// 初始化摆轮资源锁管理器
    /// </summary>
    public DiverterResourceLockManager()
    {
        _locks = new ConcurrentDictionary<string, DiverterResourceLock>();
    }

    /// <inheritdoc/>
    public IDiverterResourceLock GetLock(string diverterId)
    {
        if (string.IsNullOrWhiteSpace(diverterId))
        {
            throw new ArgumentException("摆轮ID不能为空", nameof(diverterId));
        }

        return _locks.GetOrAdd(diverterId, id => new DiverterResourceLock(id));
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
