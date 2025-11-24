using ZakYip.WheelDiverterSorter.Core.Enums;

namespace ZakYip.WheelDiverterSorter.Ingress.Configuration;

/// <summary>
/// 雷赛传感器配置DTO
/// </summary>
public record LeadshineSensorConfigDto
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
    /// 输入位索引
    /// </summary>
    public required int InputBit { get; init; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; init; } = true;
}