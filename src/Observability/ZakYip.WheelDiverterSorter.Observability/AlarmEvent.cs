using ZakYip.WheelDiverterSorter.Core.Enums.Monitoring;

namespace ZakYip.WheelDiverterSorter.Observability;

/// <summary>
/// 告警事件 / Alarm Event
/// </summary>
public class AlarmEvent
{
    /// <summary>
    /// 告警类型 / Alarm type
    /// </summary>
    public AlarmType Type { get; set; }

    /// <summary>
    /// 告警级别 / Alarm level
    /// </summary>
    public AlarmLevel Level { get; set; }

    /// <summary>
    /// 告警消息 / Alarm message
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 告警详情 / Alarm details
    /// </summary>
    public Dictionary<string, object> Details { get; set; } = new();

    /// <summary>
    /// 触发时间 / Trigger time (本地时间，由创建者通过ISystemClock设置)
    /// </summary>
    public DateTime TriggeredAt { get; set; }

    /// <summary>
    /// 是否已确认 / Whether acknowledged
    /// </summary>
    public bool IsAcknowledged { get; set; }

    /// <summary>
    /// 确认时间 / Acknowledgment time
    /// </summary>
    public DateTime? AcknowledgedAt { get; set; }
}
