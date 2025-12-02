using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;

namespace ZakYip.WheelDiverterSorter.Core.Events.Sensor;

/// <summary>
/// 重复触发异常事件参数
/// PR-PERF-EVENTS01: 转换为 sealed record class 以优化性能（包含 string 引用类型）
/// </summary>
public sealed record class DuplicateTriggerEventArgs
{
    /// <summary>
    /// 包裹ID
    /// </summary>
    public required long ParcelId { get; init; }

    /// <summary>
    /// 检测时间
    /// </summary>
    public required DateTimeOffset DetectedAt { get; init; }

    /// <summary>
    /// 触发检测的传感器ID
    /// </summary>
    public required string SensorId { get; init; }

    /// <summary>
    /// 传感器类型
    /// </summary>
    public required SensorType SensorType { get; init; }

    /// <summary>
    /// 距离上次触发的时间间隔（毫秒）
    /// </summary>
    public required double TimeSinceLastTriggerMs { get; init; }

    /// <summary>
    /// 异常原因
    /// </summary>
    public required string Reason { get; init; }
}