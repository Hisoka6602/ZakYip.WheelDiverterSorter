using System.ComponentModel;

namespace ZakYip.WheelDiverterSorter.Core.Enums.Sorting;

/// <summary>
/// 异常类型枚举
/// Exception type enumeration for routing failures
/// </summary>
/// <remarks>
/// 定义包裹路由到异常格口的各种原因类型
/// Defines various reason types for routing parcels to exception chute
/// </remarks>
public enum ExceptionType
{
    /// <summary>
    /// 未知异常
    /// Unknown exception
    /// </summary>
    [Description("未知异常")]
    Unknown = 0,

    /// <summary>
    /// 上游超时 - RuleEngine 未在 TTL 时间内响应
    /// Upstream timeout - RuleEngine did not respond within TTL
    /// </summary>
    [Description("上游超时")]
    UpstreamTimeout = 1,

    /// <summary>
    /// 路径失败 - 执行路径时发生错误
    /// Path failure - Error occurred during path execution
    /// </summary>
    [Description("路径失败")]
    PathFailure = 2,

    /// <summary>
    /// 节点降级 - 路径经过不健康节点
    /// Node degraded - Path requires unhealthy node
    /// </summary>
    [Description("节点降级")]
    NodeDegraded = 3,

    /// <summary>
    /// 系统过载 - 系统拥堵超出处理能力
    /// System overload - System congestion over capacity
    /// </summary>
    [Description("系统过载")]
    Overload = 4,

    /// <summary>
    /// 拓扑不可达 - 无可用路径到达目标格口
    /// Topology unreachable - No available path to target chute
    /// </summary>
    [Description("拓扑不可达")]
    TopologyUnreachable = 5,

    /// <summary>
    /// TTL 失败 - 包裹在系统中停留时间过长
    /// TTL failure - Parcel stayed in system too long
    /// </summary>
    [Description("TTL失败")]
    TtlFailure = 6,

    /// <summary>
    /// 传感器故障 - 传感器检测异常
    /// Sensor fault - Sensor detection error
    /// </summary>
    [Description("传感器故障")]
    SensorFault = 7,

    /// <summary>
    /// 配置错误 - 配置无效或缺失
    /// Configuration error - Invalid or missing configuration
    /// </summary>
    [Description("配置错误")]
    ConfigurationError = 8
}
