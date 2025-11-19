namespace ZakYip.WheelDiverterSorter.Communication.Models;

/// <summary>
/// 格口分配通知事件参数
/// </summary>
/// <remarks>
/// 当RuleEngine推送格口分配时触发此事件
/// </remarks>
public record ChuteAssignmentNotificationEventArgs
{
    /// <summary>
    /// 包裹ID (毫秒时间戳)
    /// </summary>
    public required long ParcelId { get; init; }

    /// <summary>
    /// 目标格口ID（数字ID）
    /// </summary>
    public required int ChuteId { get; init; }

    /// <summary>
    /// 通知时间
    /// </summary>
    public DateTimeOffset NotificationTime { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 额外的元数据（可选）
    /// </summary>
    public Dictionary<string, string>? Metadata { get; init; }
}
