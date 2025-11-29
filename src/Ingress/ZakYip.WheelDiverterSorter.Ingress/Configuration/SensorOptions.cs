using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;

namespace ZakYip.WheelDiverterSorter.Ingress.Configuration;

/// <summary>
/// 传感器配置选项
/// </summary>
/// <remarks>
/// 此类是用于配置绑定的 DTO，不直接引用具体厂商类型。
/// 具体的厂商配置通过 ISensorVendorConfigProvider 抽象获取。
/// </remarks>
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
    /// 模拟传感器配置列表
    /// </summary>
    public List<MockSensorConfigDto> MockSensors { get; set; } = new();
}