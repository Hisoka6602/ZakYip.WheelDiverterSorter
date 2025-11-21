using ZakYip.WheelDiverterSorter.Core.Enums.Communication;
using ZakYip.WheelDiverterSorter.Core.Enums.Conveyor;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Enums.Routing;
using ZakYip.WheelDiverterSorter.Core.Enums.Sensors;
using ZakYip.WheelDiverterSorter.Core.Enums.System;

namespace ZakYip.WheelDiverterSorter.Ingress.Configuration;

/// <summary>
/// 模拟传感器配置DTO
/// </summary>
public class MockSensorConfigDto {

    /// <summary>
    /// 传感器ID
    /// </summary>
    public required string SensorId { get; set; }

    /// <summary>
    /// 传感器类型
    /// </summary>
    public SensorType Type { get; set; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 模拟触发最小间隔（毫秒）
    /// </summary>
    public int MinTriggerIntervalMs { get; set; } = 5000;

    /// <summary>
    /// 模拟触发最大间隔（毫秒）
    /// </summary>
    public int MaxTriggerIntervalMs { get; set; } = 15000;

    /// <summary>
    /// 模拟包裹通过最小时间（毫秒）
    /// </summary>
    public int MinParcelPassTimeMs { get; set; } = 200;

    /// <summary>
    /// 模拟包裹通过最大时间（毫秒）
    /// </summary>
    public int MaxParcelPassTimeMs { get; set; } = 500;
}