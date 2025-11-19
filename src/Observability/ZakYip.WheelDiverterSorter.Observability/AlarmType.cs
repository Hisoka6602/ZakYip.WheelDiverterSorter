namespace ZakYip.WheelDiverterSorter.Observability;

/// <summary>
/// 告警类型 / Alarm Type
/// </summary>
public enum AlarmType
{
    /// <summary>
    /// 分拣失败率过高 / High sorting failure rate
    /// </summary>
    SortingFailureRateHigh,

    /// <summary>
    /// 队列积压 / Queue backlog
    /// </summary>
    QueueBacklog,

    /// <summary>
    /// 摆轮故障 / Diverter fault
    /// </summary>
    DiverterFault,

    /// <summary>
    /// 传感器故障 / Sensor fault
    /// </summary>
    SensorFault,

    /// <summary>
    /// RuleEngine断线 / RuleEngine disconnection
    /// </summary>
    RuleEngineDisconnected,

    /// <summary>
    /// 系统重启 / System restart
    /// </summary>
    SystemRestart
}
