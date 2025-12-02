using System.ComponentModel;

namespace ZakYip.WheelDiverterSorter.Core.Enums.Parcel;

/// <summary>
/// 包裹最终状态枚举
/// </summary>
public enum ParcelFinalStatus
{
    /// <summary>
    /// 成功分拣到目标格口
    /// </summary>
    [Description("成功")]
    Success,

    /// <summary>
    /// 超时
    /// </summary>
    [Description("超时")]
    Timeout,

    /// <summary>
    /// 包裹掉落
    /// </summary>
    [Description("掉落")]
    Dropped,

    /// <summary>
    /// 传感器故障
    /// </summary>
    [Description("传感器故障")]
    SensorFault,

    /// <summary>
    /// 路由到异常格口
    /// </summary>
    [Description("异常路由")]
    ExceptionRouted,

    /// <summary>
    /// 来源不明
    /// </summary>
    [Description("来源不明")]
    UnknownSource,

    /// <summary>
    /// 执行错误
    /// </summary>
    [Description("执行错误")]
    ExecutionError,

    /// <summary>
    /// 规则引擎超时
    /// </summary>
    [Description("规则引擎超时")]
    RuleEngineTimeout,

    /// <summary>
    /// 包裹丢失（超过最大存活时间仍未完成落格）
    /// </summary>
    /// <remarks>
    /// PR-NOSHADOW-ALL: 从首次检测时间起，若超过 MaxLifetimeBeforeLost 仍未获得落格确认，
    /// 也没有有效的位置状态，则判定为"包裹丢失"。
    /// 与 Timeout 的区别：Lost 表示包裹已不在输送线上，无法导向异常口，需从缓存清除。
    /// </remarks>
    [Description("包裹丢失")]
    Lost
}
