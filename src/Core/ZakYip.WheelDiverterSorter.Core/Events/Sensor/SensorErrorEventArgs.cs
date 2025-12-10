using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;

namespace ZakYip.WheelDiverterSorter.Core.Events.Sensor;

/// <summary>
/// 传感器错误事件参数
/// PR-PERF-EVENTS01: 转换为 sealed record class 以优化性能（包含 string 和 Exception 引用类型）
/// </summary>
public sealed record class SensorErrorEventArgs
{
    /// <summary>
    /// 传感器ID
    /// </summary>
    public required long SensorId { get; init; }

    /// <summary>
    /// 传感器类型
    /// </summary>
    public required SensorType Type { get; init; }

    /// <summary>
    /// 错误消息
    /// </summary>
    public required string ErrorMessage { get; init; }

    /// <summary>
    /// 异常对象
    /// </summary>
    public Exception? Exception { get; init; }

    /// <summary>
    /// 错误时间
    /// </summary>
    public required DateTimeOffset ErrorTime { get; init; }
}