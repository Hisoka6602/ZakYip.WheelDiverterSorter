using ZakYip.WheelDiverterSorter.Core.LineModel.Enums;

namespace ZakYip.WheelDiverterSorter.Ingress.Configuration;

/// <summary>
/// 传感器配置选项
/// </summary>
public class SensorOptions {

    /// <summary>
    /// 是否使用硬件传感器（false则使用模拟传感器）
    /// </summary>
    public bool UseHardwareSensor { get; set; } = false;

    /// <summary>
    /// 传感器厂商类型
    /// </summary>
    public SensorVendorType VendorType { get; set; } = SensorVendorType.Leadshine;

    /// <summary>
    /// 雷赛传感器配置
    /// </summary>
    public LeadshineSensorOptions? Leadshine { get; set; }

    /// <summary>
    /// 模拟传感器配置列表
    /// </summary>
    public List<MockSensorConfigDto> MockSensors { get; set; } = new();
}