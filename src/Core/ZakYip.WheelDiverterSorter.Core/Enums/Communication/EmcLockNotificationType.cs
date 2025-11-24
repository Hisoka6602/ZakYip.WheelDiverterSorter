using System.ComponentModel;

namespace ZakYip.WheelDiverterSorter.Core.Enums.Communication;

/// <summary>
/// EMC锁通知类型
/// </summary>
public enum EmcLockNotificationType
{
    /// <summary>
    /// 请求锁（准备执行重置操作）
    /// </summary>
    [Description("请求锁")]
    RequestLock = 0,
    
    /// <summary>
    /// 释放锁（重置操作完成）
    /// </summary>
    [Description("释放锁")]
    ReleaseLock = 1,
    
    /// <summary>
    /// 冷重置通知（需要硬件重启）
    /// </summary>
    [Description("冷重置")]
    ColdReset = 2,
    
    /// <summary>
    /// 热重置通知（软件重置，不需要硬件重启）
    /// </summary>
    [Description("热重置")]
    HotReset = 3,
    
    /// <summary>
    /// 确认收到通知
    /// </summary>
    [Description("确认")]
    Acknowledge = 4,
    
    /// <summary>
    /// 准备就绪（实例已停止使用EMC，可以执行重置）
    /// </summary>
    [Description("准备就绪")]
    Ready = 5,
    
    /// <summary>
    /// 重置完成通知
    /// </summary>
    [Description("重置完成")]
    ResetComplete = 6
}
