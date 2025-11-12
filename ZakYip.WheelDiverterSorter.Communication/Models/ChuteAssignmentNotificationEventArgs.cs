namespace ZakYip.WheelDiverterSorter.Communication;

/// <summary>
/// 格口分配通知事件参数
/// </summary>
/// <remarks>
/// 当RuleEngine推送格口分配时触发此事件
/// </remarks>
public class ChuteAssignmentNotificationEventArgs : EventArgs
{
    /// <summary>
    /// 包裹ID
    /// </summary>
    public required string ParcelId { get; init; }

    /// <summary>
    /// 目标格口号
    /// </summary>
    public required string ChuteNumber { get; init; }

    /// <summary>
    /// 通知时间
    /// </summary>
    public DateTimeOffset NotificationTime { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 额外的元数据（可选）
    /// </summary>
    public Dictionary<string, string>? Metadata { get; init; }
}
