using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;

namespace ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;

/// <summary>
/// 感应IO配置（存储在LiteDB中，支持热更新）
/// </summary>
/// <remarks>
/// 感应IO配置定义了系统中所有感应IO的逻辑配置。
/// 
/// **重要说明**：
/// - 厂商相关的硬件配置（如雷赛、西门子等）已移至 IO驱动器配置（/api/config/io-driver）
/// - 本配置仅包含感应IO的逻辑定义和业务类型配置
/// - 感应IO类型按业务功能分类：ParcelCreation（创建包裹）、WheelFront（摆轮前）、ChuteLock（锁格）
/// </remarks>
public class SensorConfiguration
{
    /// <summary>
    /// LiteDB自动生成的唯一标识
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 感应IO配置列表
    /// </summary>
    /// <remarks>
    /// 定义系统中所有感应IO的逻辑配置，包括业务类型和关联的IO点位。
    /// 注意：只能有一个 ParcelCreation 类型的感应IO处于激活状态。
    /// </remarks>
    public List<SensorIoEntry> Sensors { get; set; } = new();

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

    #region 向后兼容 - 废弃字段

    /// <summary>
    /// [废弃] 是否使用硬件传感器 - 已移至IO驱动器配置
    /// </summary>
    /// <remarks>
    /// 此字段已废弃，请使用 /api/config/io-driver 中的 UseHardwareDriver 配置。
    /// 保留此字段仅为向后兼容，在未来版本中将被移除。
    /// </remarks>
    [Obsolete("请使用 /api/config/io-driver 中的 UseHardwareDriver 配置")]
    public bool UseHardwareSensor { get; set; } = false;

    /// <summary>
    /// [废弃] 传感器厂商类型 - 已移至IO驱动器配置
    /// </summary>
    /// <remarks>
    /// 此字段已废弃，请使用 /api/config/io-driver 中的 VendorType 配置。
    /// 保留此字段仅为向后兼容，在未来版本中将被移除。
    /// </remarks>
    [Obsolete("请使用 /api/config/io-driver 中的 VendorType 配置")]
    public SensorVendorType VendorType { get; set; } = SensorVendorType.Leadshine;

    /// <summary>
    /// [废弃] 雷赛传感器配置 - 已移至IO驱动器配置
    /// </summary>
    /// <remarks>
    /// 此字段已废弃，请使用 /api/config/io-driver 中的 Leadshine 配置。
    /// 保留此字段仅为向后兼容，在未来版本中将被移除。
    /// </remarks>
    [Obsolete("请使用 /api/config/io-driver 中的 Leadshine 配置")]
    public LeadshineSensorConfig? Leadshine { get; set; }

    /// <summary>
    /// [废弃] 模拟传感器配置列表 - 已迁移到 Sensors 列表
    /// </summary>
    /// <remarks>
    /// 此字段已废弃，请使用 Sensors 列表。
    /// 保留此字段仅为向后兼容，在未来版本中将被移除。
    /// </remarks>
    [Obsolete("请使用 Sensors 列表")]
    public List<MockSensorEntry> MockSensors { get; set; } = new();

    #endregion

    /// <summary>
    /// 获取默认配置
    /// </summary>
    public static SensorConfiguration GetDefault()
    {
        return new SensorConfiguration
        {
            Sensors = new List<SensorIoEntry>
            {
                new() 
                { 
                    SensorId = 1, 
                    SensorName = "创建包裹感应IO", 
                    IoType = SensorIoType.ParcelCreation, 
                    IoPointId = 0, 
                    IsEnabled = true 
                },
                new() 
                { 
                    SensorId = 2, 
                    SensorName = "摆轮1前感应IO", 
                    IoType = SensorIoType.WheelFront, 
                    IoPointId = 1, 
                    BoundWheelNodeId = "WHEEL-1",
                    IsEnabled = true 
                },
                new() 
                { 
                    SensorId = 3, 
                    SensorName = "格口1锁格感应IO", 
                    IoType = SensorIoType.ChuteLock, 
                    IoPointId = 2, 
                    BoundChuteId = "CHUTE-001",
                    IsEnabled = true 
                }
            },
            // 向后兼容的废弃字段
#pragma warning disable CS0618 // Type or member is obsolete
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
#pragma warning restore CS0618 // Type or member is obsolete
        };
    }

    /// <summary>
    /// 验证配置
    /// </summary>
    public (bool IsValid, string? ErrorMessage) Validate()
    {
        if (Sensors != null)
        {
            // 检查SensorId不能重复
            var duplicateIds = Sensors
                .GroupBy(s => s.SensorId)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicateIds.Any())
            {
                return (false, $"感应IO ID重复: {string.Join(", ", duplicateIds)}");
            }

            // 检查只能有一个激活的 ParcelCreation 类型感应IO
            var activeParcelCreationSensors = Sensors
                .Where(s => s.IoType == SensorIoType.ParcelCreation && s.IsEnabled)
                .ToList();

            if (activeParcelCreationSensors.Count > 1)
            {
                return (false, $"只能有一个激活的创建包裹感应IO，当前有 {activeParcelCreationSensors.Count} 个: {string.Join(", ", activeParcelCreationSensors.Select(s => s.SensorName ?? s.SensorId.ToString()))}");
            }

            // 检查 WheelFront 类型必须绑定摆轮节点
            var wheelFrontWithoutBinding = Sensors
                .Where(s => s.IoType == SensorIoType.WheelFront && s.IsEnabled && string.IsNullOrEmpty(s.BoundWheelNodeId))
                .ToList();

            if (wheelFrontWithoutBinding.Any())
            {
                return (false, $"摆轮前感应IO必须绑定摆轮节点: {string.Join(", ", wheelFrontWithoutBinding.Select(s => s.SensorName ?? s.SensorId.ToString()))}");
            }

            // 检查 ChuteLock 类型必须绑定格口
            var chuteLockWithoutBinding = Sensors
                .Where(s => s.IoType == SensorIoType.ChuteLock && s.IsEnabled && string.IsNullOrEmpty(s.BoundChuteId))
                .ToList();

            if (chuteLockWithoutBinding.Any())
            {
                return (false, $"锁格感应IO必须绑定格口: {string.Join(", ", chuteLockWithoutBinding.Select(s => s.SensorName ?? s.SensorId.ToString()))}");
            }
        }

#pragma warning disable CS0618 // Type or member is obsolete
        // 向后兼容验证 - 仅当使用旧配置时验证
        if (!Enum.IsDefined(typeof(SensorVendorType), VendorType))
        {
            return (false, "传感器厂商类型无效");
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
#pragma warning restore CS0618 // Type or member is obsolete

        return (true, null);
    }
}

/// <summary>
/// 感应IO配置条目
/// </summary>
/// <remarks>
/// 定义单个感应IO的逻辑配置，包括业务类型和关联的IO点位。
/// IO点位的具体硬件映射由 IO驱动器配置（/api/config/io-driver）定义。
/// </remarks>
public class SensorIoEntry
{
    /// <summary>
    /// 感应IO标识符（数字ID）
    /// </summary>
    public required long SensorId { get; set; }

    /// <summary>
    /// 感应IO名称（可选）
    /// </summary>
    /// <remarks>
    /// 用于显示的友好名称，例如 "创建包裹感应IO"、"摆轮1前感应IO"
    /// </remarks>
    public string? SensorName { get; set; }

    /// <summary>
    /// 感应IO类型（业务功能分类）
    /// </summary>
    /// <remarks>
    /// 按业务功能分为三种类型：
    /// - ParcelCreation: 创建包裹感应IO（只能同时存在一个激活的）
    /// - WheelFront: 摆轮前感应IO（与摆轮 frontIoId 关联）
    /// - ChuteLock: 锁格感应IO
    /// </remarks>
    public required SensorIoType IoType { get; set; }

    /// <summary>
    /// 关联的IO点位ID（引用IO驱动器配置中的IO点位）
    /// </summary>
    /// <remarks>
    /// 此ID对应 IO驱动器配置中定义的输入IO点位。
    /// 具体的硬件映射（如雷赛控制卡的 InputBit）由 IO驱动器配置管理。
    /// </remarks>
    public required int IoPointId { get; set; }

    /// <summary>
    /// 绑定的摆轮节点ID（仅当 IoType 为 WheelFront 时使用）
    /// </summary>
    /// <remarks>
    /// WheelFront 类型的感应IO必须绑定一个摆轮节点，
    /// 用于在包裹到达摆轮前触发摆轮提前动作。
    /// </remarks>
    public string? BoundWheelNodeId { get; set; }

    /// <summary>
    /// 绑定的格口ID（仅当 IoType 为 ChuteLock 时使用）
    /// </summary>
    /// <remarks>
    /// ChuteLock 类型的感应IO必须绑定一个格口，
    /// 用于确认包裹已成功落入目标格口。
    /// </remarks>
    public string? BoundChuteId { get; set; }

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
    public required long SensorId { get; set; }

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
    public required long SensorId { get; set; }

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
