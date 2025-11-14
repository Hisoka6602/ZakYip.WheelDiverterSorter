namespace ZakYip.WheelDiverterSorter.Communication.Models;

/// <summary>
/// EMC锁事件
/// </summary>
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
    /// 时间戳
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// 额外消息
    /// </summary>
    public string? Message { get; set; }
    
    /// <summary>
    /// 超时时间（毫秒）- 其他实例需要在此时间内响应
    /// </summary>
    public int TimeoutMs { get; set; } = 5000;
}
