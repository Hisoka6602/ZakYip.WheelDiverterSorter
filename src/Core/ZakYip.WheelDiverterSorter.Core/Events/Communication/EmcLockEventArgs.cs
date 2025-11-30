using ZakYip.WheelDiverterSorter.Core.Enums.Communication;

namespace ZakYip.WheelDiverterSorter.Core.Events.Communication;

/// <summary>
/// EMC锁事件参数
/// </summary>
/// <remarks>
/// 包含EMC锁相关事件的完整信息，用于分布式环境中的EMC设备协调。
/// </remarks>
public class EmcLockEventArgs : EventArgs
{
    /// <summary>
    /// 事件ID（唯一标识）
    /// </summary>
    public string EventId { get; init; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// 发送者实例ID
    /// </summary>
    public string InstanceId { get; init; } = string.Empty;
    
    /// <summary>
    /// 通知类型
    /// </summary>
    public EmcLockNotificationType NotificationType { get; init; }
    
    /// <summary>
    /// EMC卡号
    /// </summary>
    public ushort CardNo { get; init; }
    
    /// <summary>
    /// 时间戳（本地时间）
    /// </summary>
    public DateTime Timestamp { get; init; }
    
    /// <summary>
    /// 额外消息
    /// </summary>
    public string? Message { get; init; }
    
    /// <summary>
    /// 超时时间（毫秒）- 其他实例需要在此时间内响应
    /// </summary>
    public int TimeoutMs { get; init; } = 5000;
}
