using ZakYip.WheelDiverterSorter.Core.Enums.Communication;

namespace ZakYip.WheelDiverterSorter.Core.Events.Communication;

/// <summary>
/// EMC锁事件参数
/// PR-PERF-EVENTS01: 转换为 sealed record class 以优化性能
/// </summary>
/// <remarks>
/// 包含EMC锁相关事件的完整信息，用于分布式环境中的EMC设备协调。
/// </remarks>
public sealed record class EmcLockEventArgs
{
    /// <summary>
    /// 事件ID（唯一标识）
    /// </summary>
    public required string EventId { get; init; }
    
    /// <summary>
    /// 发送者实例ID
    /// </summary>
    public required string InstanceId { get; init; }
    
    /// <summary>
    /// 通知类型
    /// </summary>
    public required EmcLockNotificationType NotificationType { get; init; }
    
    /// <summary>
    /// EMC卡号
    /// </summary>
    public required ushort CardNo { get; init; }
    
    /// <summary>
    /// 时间戳（本地时间）
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }
    
    /// <summary>
    /// 额外消息
    /// </summary>
    public string? Message { get; init; }
    
    /// <summary>
    /// 超时时间（毫秒）- 其他实例需要在此时间内响应
    /// </summary>
    public int TimeoutMs { get; init; } = 5000;
}
