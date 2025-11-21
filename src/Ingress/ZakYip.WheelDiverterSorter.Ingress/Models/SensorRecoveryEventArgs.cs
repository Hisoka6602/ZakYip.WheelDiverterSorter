using ZakYip.WheelDiverterSorter.Core.Enums.Communication;
using ZakYip.WheelDiverterSorter.Core.Enums.Conveyor;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Enums.Routing;
using ZakYip.WheelDiverterSorter.Core.Enums.Sensors;
using ZakYip.WheelDiverterSorter.Core.Enums.System;

namespace ZakYip.WheelDiverterSorter.Ingress.Models;

/// <summary>
/// 传感器恢复事件参数
/// </summary>
public record SensorRecoveryEventArgs {
    /// <summary>
    /// 传感器ID
    /// </summary>
    public required string SensorId { get; init; }

    /// <summary>
    /// 传感器类型
    /// </summary>
    public required SensorType Type { get; init; }

    /// <summary>
    /// 恢复时间
    /// </summary>
    public DateTimeOffset RecoveryTime { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 故障持续时间（秒）
    /// </summary>
    public required double FaultDurationSeconds { get; init; }
}