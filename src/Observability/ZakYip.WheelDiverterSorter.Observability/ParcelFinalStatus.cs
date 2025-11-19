namespace ZakYip.WheelDiverterSorter.Observability;

/// <summary>
/// 包裹最终状态枚举
/// </summary>
public enum ParcelFinalStatus
{
    /// <summary>
    /// 成功分拣到目标格口
    /// </summary>
    Success,

    /// <summary>
    /// 超时
    /// </summary>
    Timeout,

    /// <summary>
    /// 包裹掉落
    /// </summary>
    Dropped,

    /// <summary>
    /// 传感器故障
    /// </summary>
    SensorFault,

    /// <summary>
    /// 路由到异常格口
    /// </summary>
    ExceptionRouted,

    /// <summary>
    /// 来源不明
    /// </summary>
    UnknownSource,

    /// <summary>
    /// 执行错误
    /// </summary>
    ExecutionError,

    /// <summary>
    /// 规则引擎超时
    /// </summary>
    RuleEngineTimeout
}
