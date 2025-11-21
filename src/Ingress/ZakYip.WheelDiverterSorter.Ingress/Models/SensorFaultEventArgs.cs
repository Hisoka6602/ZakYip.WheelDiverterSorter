using ZakYip.WheelDiverterSorter.Core.Enums.Communication;
using ZakYip.WheelDiverterSorter.Core.Enums.Conveyor;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Enums.Routing;
using ZakYip.WheelDiverterSorter.Core.Enums.Sensors;
using ZakYip.WheelDiverterSorter.Core.Enums.System;

namespace ZakYip.WheelDiverterSorter.Ingress.Models;

/// <summary>
/// 传感器故障事件参数
/// </summary>
public record SensorFaultEventArgs {
    /// <summary>
    /// 传感器ID
    /// </summary>
    public required string SensorId { get; init; }

    /// <summary>
    /// 传感器类型
    /// </summary>
    public required SensorType Type { get; init; }

    /// <summary>
    /// 故障类型
    /// </summary>
    public required SensorFaultType FaultType { get; init; }

    /// <summary>
    /// 故障描述
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// 故障发生时间
    /// </summary>
    public DateTimeOffset FaultTime { get; init; } = DateTimeOffset.UtcNow;
}