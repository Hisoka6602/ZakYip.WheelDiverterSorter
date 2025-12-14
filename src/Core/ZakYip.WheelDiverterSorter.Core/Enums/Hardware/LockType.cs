namespace ZakYip.WheelDiverterSorter.Core.Enums.Hardware;

/// <summary>
/// EMC锁类型
/// </summary>
/// <remarks>
/// 用于 CoordinatedEmcController 区分不同的锁实现方式
/// </remarks>
public enum LockType
{
    /// <summary>
    /// 不使用锁
    /// </summary>
    None,
    
    /// <summary>
    /// 使用基于TCP的锁管理器 (IEmcResourceLockManager)
    /// </summary>
    TcpBased,
    
    /// <summary>
    /// 使用命名互斥锁 (IEmcResourceLock)
    /// </summary>
    NamedMutex
}
