using System.ComponentModel;

namespace ZakYip.WheelDiverterSorter.Core.Enums;

/// <summary>
/// 超载原因枚举
/// Reasons why a parcel is routed to exception chute
/// </summary>
public enum OverloadReason
{
    /// <summary>
    /// 正常分拣
    /// Normal sorting, not overloaded
    /// </summary>
    [Description("正常")]
    None = 0,

    /// <summary>
    /// 超时：RuleEngine响应超时
    /// Timeout: RuleEngine did not respond in time
    /// </summary>
    [Description("超时")]
    Timeout = 1,

    /// <summary>
    /// 窗口错失：包裹错过了分拣时间窗口
    /// Window Miss: Parcel missed sorting time window
    /// </summary>
    [Description("窗口错失")]
    WindowMiss = 2,

    /// <summary>
    /// 产能超限：系统拥堵，超出处理能力
    /// Capacity Exceeded: System congestion, over capacity
    /// </summary>
    [Description("产能超限")]
    CapacityExceeded = 3,

    /// <summary>
    /// 节点降级：路径经过不健康节点，自动降级到异常口
    /// Node Degraded: Path requires unhealthy node, auto-routed to exception
    /// </summary>
    [Description("节点降级")]
    NodeDegraded = 4,

    /// <summary>
    /// 拓扑不可达：无可用路径到达目标格口
    /// Topology Unreachable: No available path to target chute
    /// </summary>
    [Description("拓扑不可达")]
    TopologyUnreachable = 5,

    /// <summary>
    /// 传感器故障：传感器检测异常
    /// Sensor Fault: Sensor detection error
    /// </summary>
    [Description("传感器故障")]
    SensorFault = 6,

    /// <summary>
    /// 其他原因
    /// Other reasons
    /// </summary>
    [Description("其他")]
    Other = 99
}
