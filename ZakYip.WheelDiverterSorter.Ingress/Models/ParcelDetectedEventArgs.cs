namespace ZakYip.WheelDiverterSorter.Ingress.Models;

/// <summary>
/// 包裹检测事件参数
/// </summary>
public record ParcelDetectedEventArgs
{
    /// <summary>
    /// 生成的唯一包裹ID
    /// </summary>
    public required string ParcelId { get; init; }

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
}
