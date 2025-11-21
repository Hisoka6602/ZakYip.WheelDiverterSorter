using System.ComponentModel;

namespace ZakYip.WheelDiverterSorter.Core.Enums.Routing;

/// <summary>
/// 路径失败原因枚举
/// Path failure reason enumeration
/// </summary>
public enum PathFailureReason
{
    /// <summary>
    /// 传感器超时 - 传感器在预期时间内未检测到包裹
    /// Sensor timeout - Package not detected by sensor within expected time
    /// </summary>
    [Description("传感器超时")]
    SensorTimeout,

    /// <summary>
    /// 方向反馈不一致 - 摆轮实际方向与预期方向不符
    /// Unexpected direction - Diverter actual direction does not match expected
    /// </summary>
    [Description("方向反馈不一致")]
    UnexpectedDirection,

    /// <summary>
    /// 上游阻塞 - 上游节点被阻塞导致无法继续执行
    /// Upstream blocked - Upstream node blocked preventing execution
    /// </summary>
    [Description("上游阻塞")]
    UpstreamBlocked,

    /// <summary>
    /// 物理约束 - 拓扑结构或物理限制导致无法到达目标
    /// Physical constraint - Topology or physical limitation prevents reaching target
    /// </summary>
    [Description("物理约束")]
    PhysicalConstraint,

    /// <summary>
    /// TTL过期 - 路径段执行时间超过预期
    /// TTL expired - Path segment execution time exceeded
    /// </summary>
    [Description("TTL过期")]
    TtlExpired,

    /// <summary>
    /// 摆轮故障 - 摆轮设备故障或通信失败
    /// Diverter fault - Diverter device failure or communication error
    /// </summary>
    [Description("摆轮故障")]
    DiverterFault,

    /// <summary>
    /// 传感器故障 - 传感器设备故障
    /// Sensor fault - Sensor device failure
    /// </summary>
    [Description("传感器故障")]
    SensorFault,

    /// <summary>
    /// 包裹掉落 - 包裹在传输过程中丢失
    /// Parcel dropout - Package lost during transport
    /// </summary>
    [Description("包裹掉落")]
    ParcelDropout,

    /// <summary>
    /// 未知错误 - 其他未分类的错误
    /// Unknown error - Other unclassified errors
    /// </summary>
    [Description("未知错误")]
    Unknown
}
