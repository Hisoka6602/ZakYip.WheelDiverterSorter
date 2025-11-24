namespace ZakYip.WheelDiverterSorter.Communication.Models;

/// <summary>
/// 包裹检测通知
/// </summary>
/// <remarks>
/// 当系统检测到包裹时，发送此通知给RuleEngine
/// </remarks>
public record ParcelDetectionNotification
{
    /// <summary>
    /// 包裹ID (毫秒时间戳)
    /// </summary>
    public required long ParcelId { get; init; }

    /// <summary>
    /// 检测时间
    /// </summary>
    public required DateTimeOffset DetectionTime { get; init; }

    /// <summary>
    /// 额外的元数据（可选）
    /// </summary>
    public Dictionary<string, string>? Metadata { get; init; }
}
