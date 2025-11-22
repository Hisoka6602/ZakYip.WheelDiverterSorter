using ZakYip.WheelDiverterSorter.Core.Enums.Communication;
using ZakYip.WheelDiverterSorter.Core.Enums.Conveyor;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Enums.Routing;
using ZakYip.WheelDiverterSorter.Core.Enums.Sensors;
using ZakYip.WheelDiverterSorter.Core.Enums.System;

namespace ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;

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
    /// 传感器厂商类型
    /// </summary>
    /// <remarks>
    /// 可选值:
    /// - Mock: 模拟传感器（用于测试）
    /// - Leadshine: 雷赛传感器
    /// - Siemens: 西门子传感器
    /// - Mitsubishi: 三菱传感器
    /// - Omron: 欧姆龙传感器
    /// </remarks>
    public SensorVendorType VendorType { get; set; } = SensorVendorType.Leadshine;

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
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 配置最后更新时间
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// 获取默认配置
    /// </summary>
    public static SensorConfiguration GetDefault()
    {
        return new SensorConfiguration
        {
            UseHardwareSensor = false,
            VendorType = SensorVendorType.Leadshine,
            Leadshine = new LeadshineSensorConfig
            {
                CardNo = 0,
                Sensors = new List<HardwareSensorEntry>
                {
                    new() { SensorId = 1, SensorName = "SENSOR_PE_01", Type = "Photoelectric", InputBit = 0, IsEnabled = true },
                    new() { SensorId = 2, SensorName = "SENSOR_LASER_01", Type = "Laser", InputBit = 1, IsEnabled = true }
                }
            },
            MockSensors = new List<MockSensorEntry>
            {
                new() { SensorId = 1, SensorName = "SENSOR_PE_01", Type = "Photoelectric", IsEnabled = true },
                new() { SensorId = 2, SensorName = "SENSOR_LASER_01", Type = "Laser", IsEnabled = true }
            }
        };
    }

    /// <summary>
    /// 验证配置
    /// </summary>
    public (bool IsValid, string? ErrorMessage) Validate()
    {
        if (!Enum.IsDefined(typeof(SensorVendorType), VendorType))
        {
            return (false, "传感器厂商类型无效");
        }

        if (UseHardwareSensor && VendorType == SensorVendorType.Leadshine && Leadshine == null)
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
    /// 传感器标识符（数字ID）
    /// </summary>
    public required int SensorId { get; set; }

    /// <summary>
    /// 传感器名称（可选）- Sensor Name (Optional)
    /// </summary>
    /// <remarks>
    /// 用于显示的友好名称，例如 "SENSOR_PE_01"、"入口传感器"
    /// </remarks>
    public string? SensorName { get; set; }

    /// <summary>
    /// 传感器类型 (Photoelectric/Laser)
    /// </summary>
    public required string Type { get; set; }

    /// <summary>
    /// 输入位
    /// </summary>
    public int InputBit { get; set; }

    /// <summary>
    /// IO触发电平配置（高电平有效/低电平有效）
    /// </summary>
    /// <remarks>
    /// 默认值：ActiveHigh（高电平有效）
    /// - ActiveHigh: 高电平有效（常开按键）
    /// - ActiveLow: 低电平有效（常闭按键）
    /// </remarks>
    public TriggerLevel TriggerLevel { get; set; } = TriggerLevel.ActiveHigh;

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
    /// 传感器标识符（数字ID）
    /// </summary>
    public required int SensorId { get; set; }

    /// <summary>
    /// 传感器名称（可选）- Sensor Name (Optional)
    /// </summary>
    /// <remarks>
    /// 用于显示的友好名称，例如 "SENSOR_PE_01"、"模拟入口传感器"
    /// </remarks>
    public string? SensorName { get; set; }

    /// <summary>
    /// 传感器类型 (Photoelectric/Laser)
    /// </summary>
    public required string Type { get; set; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; set; } = true;
}
