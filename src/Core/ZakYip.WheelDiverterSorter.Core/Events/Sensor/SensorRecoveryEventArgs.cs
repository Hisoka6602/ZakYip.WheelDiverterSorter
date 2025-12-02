using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;

namespace ZakYip.WheelDiverterSorter.Core.Events.Sensor;

/// <summary>
/// 传感器恢复事件参数
/// PR-PERF-EVENTS01: 转换为 sealed record class 以优化性能（包含 string 引用类型）
/// </summary>
public sealed record class SensorRecoveryEventArgs
{
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
    public required DateTimeOffset RecoveryTime { get; init; }

    /// <summary>
    /// 故障持续时间（秒）
    /// </summary>
    public required double FaultDurationSeconds { get; init; }
}