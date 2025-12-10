using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;

namespace ZakYip.WheelDiverterSorter.Core.Events.Sensor;

/// <summary>
/// 包裹检测事件参数
/// PR-PERF-EVENTS01: 转换为 sealed record class 以优化性能（包含 string 引用类型）
/// </summary>
public sealed record class ParcelDetectedEventArgs
{
    /// <summary>
    /// 生成的唯一包裹ID (毫秒时间戳)
    /// </summary>
    public required long ParcelId { get; init; }

    /// <summary>
    /// 检测时间
    /// </summary>
    public required DateTimeOffset DetectedAt { get; init; }

    /// <summary>
    /// 触发检测的传感器ID
    /// </summary>
    public required long SensorId { get; init; }

    /// <summary>
    /// 传感器类型
    /// </summary>
    public required SensorType SensorType { get; init; }

    /// <summary>
    /// 检测位置（传感器ID的别名，用于更明确表达位置信息）
    /// </summary>
    public string Position => SensorId.ToString();
}