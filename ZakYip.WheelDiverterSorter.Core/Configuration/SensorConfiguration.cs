namespace ZakYip.WheelDiverterSorter.Core.Configuration;

/// <summary>
/// 传感器配置（存储在LiteDB中，支持热更新）
/// </summary>
public class SensorConfiguration
{
    /// <summary>
    /// LiteDB自动生成的唯一标识
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 是否使用硬件传感器（false则使用模拟传感器）
    /// </summary>
    public bool UseHardwareSensor { get; set; } = false;

    /// <summary>
    /// 传感器厂商类型（枚举值：Mock=0, Leadshine=1, Siemens=2, Mitsubishi=3, Omron=4）
    /// </summary>
    public int VendorType { get; set; } = 1; // Leadshine

    /// <summary>
    /// 雷赛传感器配置
    /// </summary>
    public LeadshineSensorConfig? Leadshine { get; set; }

    /// <summary>
    /// 模拟传感器配置列表
    /// </summary>
    public List<MockSensorEntry> MockSensors { get; set; } = new();

    /// <summary>
    /// 配置版本号
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// 配置创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 配置最后更新时间
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 获取默认配置
    /// </summary>
    public static SensorConfiguration GetDefault()
    {
        return new SensorConfiguration
        {
            UseHardwareSensor = false,
            VendorType = 1, // Leadshine
            Leadshine = new LeadshineSensorConfig
            {
                CardNo = 0,
                Sensors = new List<HardwareSensorEntry>
                {
                    new() { SensorId = "SENSOR_PE_01", Type = "Photoelectric", InputBit = 0, IsEnabled = true },
                    new() { SensorId = "SENSOR_LASER_01", Type = "Laser", InputBit = 1, IsEnabled = true }
                }
            },
            MockSensors = new List<MockSensorEntry>
            {
                new() { SensorId = "SENSOR_PE_01", Type = "Photoelectric", IsEnabled = true },
                new() { SensorId = "SENSOR_LASER_01", Type = "Laser", IsEnabled = true }
            }
        };
    }

    /// <summary>
    /// 验证配置
    /// </summary>
    public (bool IsValid, string? ErrorMessage) Validate()
    {
        if (VendorType < 0 || VendorType > 4)
        {
            return (false, "传感器厂商类型无效");
        }

        if (UseHardwareSensor && VendorType == 1 && Leadshine == null)
        {
            return (false, "使用雷赛硬件传感器时，必须配置雷赛参数");
        }

        if (Leadshine != null && Leadshine.Sensors != null)
        {
            // 检查SensorId不能重复
            var duplicateIds = Leadshine.Sensors
                .GroupBy(s => s.SensorId)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicateIds.Any())
            {
                return (false, $"传感器ID重复: {string.Join(", ", duplicateIds)}");
            }
        }

        if (MockSensors != null)
        {
            // 检查模拟传感器ID不能重复
            var duplicateIds = MockSensors
                .GroupBy(s => s.SensorId)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicateIds.Any())
            {
                return (false, $"模拟传感器ID重复: {string.Join(", ", duplicateIds)}");
            }
        }

        return (true, null);
    }
}

/// <summary>
/// 雷赛传感器配置
/// </summary>
public class LeadshineSensorConfig
{
    /// <summary>
    /// 控制器卡号
    /// </summary>
    public ushort CardNo { get; set; } = 0;

    /// <summary>
    /// 传感器配置列表
    /// </summary>
    public List<HardwareSensorEntry> Sensors { get; set; } = new();
}

/// <summary>
/// 硬件传感器配置条目
/// </summary>
public class HardwareSensorEntry
{
    /// <summary>
    /// 传感器标识符
    /// </summary>
    public required string SensorId { get; set; }

    /// <summary>
    /// 传感器类型 (Photoelectric/Laser)
    /// </summary>
    public required string Type { get; set; }

    /// <summary>
    /// 输入位
    /// </summary>
    public int InputBit { get; set; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; set; } = true;
}

/// <summary>
/// 模拟传感器配置条目
/// </summary>
public class MockSensorEntry
{
    /// <summary>
    /// 传感器标识符
    /// </summary>
    public required string SensorId { get; set; }

    /// <summary>
    /// 传感器类型 (Photoelectric/Laser)
    /// </summary>
    public required string Type { get; set; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; set; } = true;
}
