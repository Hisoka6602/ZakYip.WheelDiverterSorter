using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;

namespace ZakYip.WheelDiverterSorter.Core.Events.Sensor;

/// <summary>
/// 落格传感器检测事件参数
/// </summary>
/// <remarks>
/// 当包裹落入格口时由落格传感器(ChuteDropoff)触发此事件。
/// 用于 OnSensorTrigger 模式下的落格完成通知。
/// </remarks>
public sealed record class ChuteDropoffDetectedEventArgs
{
    /// <summary>
    /// 包裹ID（如果能识别）
    /// </summary>
    /// <remarks>
    /// 在某些场景下可能无法直接识别包裹ID，需要通过格口映射推断
    /// </remarks>
    public long? ParcelId { get; init; }

    /// <summary>
    /// 格口ID
    /// </summary>
    public required long ChuteId { get; init; }

    /// <summary>
    /// 检测时间
    /// </summary>
    public required DateTimeOffset DetectedAt { get; init; }

    /// <summary>
    /// 触发检测的传感器ID
    /// </summary>
    public required long SensorId { get; init; }

    /// <summary>
    /// 传感器类型（应为 ChuteDropoff）
    /// </summary>
    public required SensorType SensorType { get; init; }
}
