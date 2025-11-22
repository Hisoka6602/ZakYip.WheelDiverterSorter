using System.ComponentModel;

namespace ZakYip.WheelDiverterSorter.Core.Enums.Observability;

/// <summary>
/// 告警类型 / Alarm Type
/// </summary>
public enum AlarmType
{
    /// <summary>
    /// 分拣失败率过高 / High sorting failure rate
    /// </summary>
    [Description("分拣失败率过高")]
    SortingFailureRateHigh,

    /// <summary>
    /// 队列积压 / Queue backlog
    /// </summary>
    [Description("队列积压")]
    QueueBacklog,

    /// <summary>
    /// 摆轮故障 / Diverter fault
    /// </summary>
    [Description("摆轮故障")]
    DiverterFault,

    /// <summary>
    /// 传感器故障 / Sensor fault
    /// </summary>
    [Description("传感器故障")]
    SensorFault,

    /// <summary>
    /// RuleEngine断线 / RuleEngine disconnection
    /// </summary>
    [Description("RuleEngine断线")]
    RuleEngineDisconnected,

    /// <summary>
    /// 系统重启 / System restart
    /// </summary>
    [Description("系统重启")]
    SystemRestart
}
