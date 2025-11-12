namespace ZakYip.WheelDiverterSorter.Ingress.Models;

/// <summary>
/// 传感器事件
/// </summary>
public record SensorEvent
{
    /// <summary>
    /// 传感器ID
    /// </summary>
    public required string SensorId { get; init; }

    /// <summary>
    /// 传感器类型
    /// </summary>
    public required SensorType SensorType { get; init; }

    /// <summary>
    /// 事件触发时间
    /// </summary>
    public required DateTimeOffset TriggerTime { get; init; }

    /// <summary>
    /// 传感器状态（true表示有物体遮挡，false表示无遮挡）
    /// </summary>
    public required bool IsTriggered { get; init; }
}
