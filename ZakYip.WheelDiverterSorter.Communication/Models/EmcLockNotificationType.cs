namespace ZakYip.WheelDiverterSorter.Communication.Models;

/// <summary>
/// EMC锁通知类型
/// </summary>
public enum EmcLockNotificationType
{
    /// <summary>
    /// 请求锁（准备执行重置操作）
    /// </summary>
    RequestLock = 0,
    
    /// <summary>
    /// 释放锁（重置操作完成）
    /// </summary>
    ReleaseLock = 1,
    
    /// <summary>
    /// 冷重置通知（需要硬件重启）
    /// </summary>
    ColdReset = 2,
    
    /// <summary>
    /// 热重置通知（软件重置，不需要硬件重启）
    /// </summary>
    HotReset = 3,
    
    /// <summary>
    /// 确认收到通知
    /// </summary>
    Acknowledge = 4,
    
    /// <summary>
    /// 准备就绪（实例已停止使用EMC，可以执行重置）
    /// </summary>
    Ready = 5,
    
    /// <summary>
    /// 重置完成通知
    /// </summary>
    ResetComplete = 6
}
