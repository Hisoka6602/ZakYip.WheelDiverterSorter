using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;

namespace ZakYip.WheelDiverterSorter.Drivers.Vendors.Leadshine.Configuration;

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

    /// <summary>
    /// 传感器轮询间隔（毫秒）
    /// </summary>
    /// <remarks>
    /// 如果为 null，则使用全局默认值 (SensorOptions.PollingIntervalMs = 10ms)。
    /// 建议范围：5ms - 50ms。
    /// </remarks>
    public int? PollingIntervalMs { get; init; }
}
