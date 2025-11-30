using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Enums.Monitoring;

namespace ZakYip.WheelDiverterSorter.Core.Events.Monitoring;

/// <summary>
/// 告警触发事件参数 / Alert Raised Event Arguments
/// </summary>
public record struct AlertRaisedEventArgs : IEquatable<AlertRaisedEventArgs>
{
    /// <summary>
    /// 告警代码 / Alert code (e.g., "EXCEPTION_CHUTE_RATIO_HIGH", "OVERLOAD_SPIKE")
    /// </summary>
    public required string AlertCode { get; init; }

    /// <summary>
    /// 告警严重程度 / Alert severity
    /// </summary>
    public required AlertSeverity Severity { get; init; }

    /// <summary>
    /// 告警消息 / Alert message
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// 告警触发时间 / Alert raised timestamp
    /// </summary>
    public required DateTimeOffset RaisedAt { get; init; }

    /// <summary>
    /// 线体ID（可选）/ Line ID (optional)
    /// </summary>
    public string? LineId { get; init; }

    /// <summary>
    /// 格口ID（可选）/ Chute ID (optional)
    /// </summary>
    public string? ChuteId { get; init; }

    /// <summary>
    /// 节点ID（可选）/ Node ID (optional)
    /// </summary>
    public long? NodeId { get; init; }

    /// <summary>
    /// 附加详情（可选）/ Additional details (optional)
    /// </summary>
    public Dictionary<string, object>? Details { get; init; }
}
