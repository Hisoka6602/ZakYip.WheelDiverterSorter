using ZakYip.WheelDiverterSorter.Core.Enums;

namespace ZakYip.WheelDiverterSorter.Ingress.Configuration;

/// <summary>
/// 模拟传感器配置DTO
/// </summary>
public record MockSensorConfigDto
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
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; init; } = true;

    /// <summary>
    /// 模拟触发最小间隔（毫秒）
    /// </summary>
    public int MinTriggerIntervalMs { get; init; } = 5000;

    /// <summary>
    /// 模拟触发最大间隔（毫秒）
    /// </summary>
    public int MaxTriggerIntervalMs { get; init; } = 15000;

    /// <summary>
    /// 模拟包裹通过最小时间（毫秒）
    /// </summary>
    public int MinParcelPassTimeMs { get; init; } = 200;

    /// <summary>
    /// 模拟包裹通过最大时间（毫秒）
    /// </summary>
    public int MaxParcelPassTimeMs { get; init; } = 500;
}