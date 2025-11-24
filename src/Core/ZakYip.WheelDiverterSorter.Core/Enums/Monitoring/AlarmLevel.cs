using System.ComponentModel;

namespace ZakYip.WheelDiverterSorter.Core.Enums.Monitoring;

/// <summary>
/// 告警级别 / Alarm Level
/// </summary>
public enum AlarmLevel
{
    /// <summary>
    /// P1告警 - 最高优先级，需要立即处理
    /// P1 Alarm - Highest priority, requires immediate attention
    /// </summary>
    [Description("P1告警")]
    P1 = 1,

    /// <summary>
    /// P2告警 - 中等优先级，需要尽快处理
    /// P2 Alarm - Medium priority, requires prompt attention
    /// </summary>
    [Description("P2告警")]
    P2 = 2,

    /// <summary>
    /// P3告警 - 低优先级，需要跟进
    /// P3 Alarm - Low priority, requires follow-up
    /// </summary>
    [Description("P3告警")]
    P3 = 3
}
