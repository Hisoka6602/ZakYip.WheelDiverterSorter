using ZakYip.WheelDiverterSorter.Core.Enums.Communication;

namespace ZakYip.WheelDiverterSorter.Core.Events.Communication;

/// <summary>
/// EMC锁事件
/// </summary>
/// <remarks>
/// 用于在多实例部署环境中传递 EMC 资源锁的协调信息。
/// 配合 <see cref="IEmcResourceLockManager"/> 使用。
/// </remarks>
public class EmcLockEvent
{
    /// <summary>
    /// 事件ID（唯一标识）
    /// </summary>
    public string EventId { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// 发送者实例ID
    /// </summary>
    public string InstanceId { get; set; } = string.Empty;
    
    /// <summary>
    /// 通知类型
    /// </summary>
    public EmcLockNotificationType NotificationType { get; set; }
    
    /// <summary>
    /// EMC卡号
    /// </summary>
    public ushort CardNo { get; set; }
    
    /// <summary>
    /// 时间戳（本地时间，由创建者通过ISystemClock设置）
    /// </summary>
    public DateTime Timestamp { get; set; }
    
    /// <summary>
    /// 额外消息
    /// </summary>
    public string? Message { get; set; }
    
    /// <summary>
    /// 超时时间（毫秒）- 其他实例需要在此时间内响应
    /// </summary>
    public int TimeoutMs { get; set; } = 5000;
}
