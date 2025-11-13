using ZakYip.WheelDiverterSorter.Core.Enums;

namespace ZakYip.WheelDiverterSorter.Ingress.Models;

/// <summary>
/// 传感器错误事件参数
/// </summary>
public record SensorErrorEventArgs {
    /// <summary>
    /// 传感器ID
    /// </summary>
    public required string SensorId { get; init; }

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
    public DateTimeOffset ErrorTime { get; init; } = DateTimeOffset.UtcNow;
}