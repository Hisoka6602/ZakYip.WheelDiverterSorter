namespace ZakYip.WheelDiverterSorter.Ingress.Configuration;

/// <summary>
/// 传感器配置选项
/// </summary>
public class SensorOptions
{
    /// <summary>
    /// 是否使用硬件传感器（false则使用模拟传感器）
    /// </summary>
    public bool UseHardwareSensor { get; set; } = false;

    /// <summary>
    /// 传感器厂商类型
    /// </summary>
    public string VendorType { get; set; } = "Leadshine";

    /// <summary>
    /// 雷赛传感器配置
    /// </summary>
    public LeadshineSensorOptions? Leadshine { get; set; }

    /// <summary>
    /// 模拟传感器配置列表
    /// </summary>
    public List<MockSensorConfigDto> MockSensors { get; set; } = new();
}

/// <summary>
/// 雷赛传感器配置选项
/// </summary>
public class LeadshineSensorOptions
{
    /// <summary>
    /// 控制器卡号
    /// </summary>
    public ushort CardNo { get; set; } = 0;

    /// <summary>
    /// 传感器配置列表
    /// </summary>
    public List<LeadshineSensorConfigDto> Sensors { get; set; } = new();
}

/// <summary>
/// 雷赛传感器配置DTO
/// </summary>
public class LeadshineSensorConfigDto
{
    /// <summary>
    /// 传感器ID
    /// </summary>
    public required string SensorId { get; set; }

    /// <summary>
    /// 传感器类型
    /// </summary>
    public required SensorType Type { get; set; }

    /// <summary>
    /// 输入位索引
    /// </summary>
    public required int InputBit { get; set; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; set; } = true;
}

/// <summary>
/// 模拟传感器配置DTO
/// </summary>
public class MockSensorConfigDto
{
    /// <summary>
    /// 传感器ID
    /// </summary>
    public required string SensorId { get; set; }

    /// <summary>
    /// 传感器类型
    /// </summary>
    public required SensorType Type { get; set; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; set; } = true;
}
