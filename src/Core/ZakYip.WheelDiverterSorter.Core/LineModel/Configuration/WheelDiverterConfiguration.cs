using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;

namespace ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;

/// <summary>
/// 摆轮配置（存储在LiteDB中，支持热更新）
/// </summary>
/// <remarks>
/// 摆轮设备通过TCP协议直接控制摆轮转向，与IO驱动器是不同的概念。
/// - 摆轮控制器：控制摆轮转向（左转、右转、回中）
/// - IO驱动器：操作IO端点（输入/输出位）
/// </remarks>
public class WheelDiverterConfiguration
{
    /// <summary>
    /// LiteDB自动生成的唯一标识
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 摆轮厂商类型
    /// </summary>
    /// <remarks>
    /// 可选值:
    /// - Mock: 模拟摆轮（用于测试）
    /// - ShuDiNiao: 数递鸟摆轮设备（默认）
    /// - Modi: 莫迪摆轮设备
    /// </remarks>
    public WheelDiverterVendorType VendorType { get; set; } = WheelDiverterVendorType.ShuDiNiao;

    /// <summary>
    /// 数递鸟摆轮设备配置
    /// </summary>
    public ShuDiNiaoWheelDiverterConfig? ShuDiNiao { get; set; }

    /// <summary>
    /// 莫迪摆轮设备配置
    /// </summary>
    public ModiWheelDiverterConfig? Modi { get; set; }

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
    public static WheelDiverterConfiguration GetDefault()
    {
        var now = ConfigurationDefaults.DefaultTimestamp;
        return new WheelDiverterConfiguration
        {
            VendorType = WheelDiverterVendorType.ShuDiNiao,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    /// <summary>
    /// 验证配置
    /// </summary>
    public (bool IsValid, string? ErrorMessage) Validate()
    {
        if (!Enum.IsDefined(typeof(WheelDiverterVendorType), VendorType))
        {
            return (false, "摆轮厂商类型无效");
        }

        if (VendorType == WheelDiverterVendorType.ShuDiNiao && ShuDiNiao == null)
        {
            return (false, "使用数递鸟摆轮时，必须配置数递鸟参数");
        }

        if (VendorType == WheelDiverterVendorType.Modi && Modi == null)
        {
            return (false, "使用莫迪摆轮时，必须配置莫迪参数");
        }

        if (ShuDiNiao != null)
        {
            if (ShuDiNiao.Devices == null || !ShuDiNiao.Devices.Any())
            {
                return (false, "数递鸟摆轮设备配置不能为空");
            }

            // 检查DeviceAddress不能重复
            var duplicateAddresses = ShuDiNiao.Devices
                .GroupBy(d => d.DeviceAddress)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicateAddresses.Any())
            {
                return (false, $"数递鸟设备地址重复: {string.Join(", ", duplicateAddresses.Select(a => $"0x{a:X2}"))}");
            }
        }

        if (Modi != null)
        {
            if (Modi.Devices == null || !Modi.Devices.Any())
            {
                return (false, "莫迪摆轮设备配置不能为空");
            }

            // 检查DiverterId不能重复
            var duplicateIds = Modi.Devices
                .GroupBy(d => d.DiverterId)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicateIds.Any())
            {
                return (false, $"莫迪摆轮ID重复: {string.Join(", ", duplicateIds)}");
            }
        }

        return (true, null);
    }
}

/// <summary>
/// 数递鸟摆轮设备配置
/// </summary>
public record class ShuDiNiaoWheelDiverterConfig
{
    /// <summary>
    /// 数递鸟摆轮设备列表
    /// </summary>
    public required List<ShuDiNiaoDeviceEntry> Devices { get; init; }

    /// <summary>
    /// 是否启用仿真模式
    /// </summary>
    public bool UseSimulation { get; init; } = false;
}

/// <summary>
/// 莫迪摆轮设备配置
/// </summary>
public record class ModiWheelDiverterConfig
{
    /// <summary>
    /// 莫迪摆轮设备列表
    /// </summary>
    public required List<ModiDeviceEntry> Devices { get; init; }

    /// <summary>
    /// 是否启用仿真模式
    /// </summary>
    public bool UseSimulation { get; init; } = false;
}
