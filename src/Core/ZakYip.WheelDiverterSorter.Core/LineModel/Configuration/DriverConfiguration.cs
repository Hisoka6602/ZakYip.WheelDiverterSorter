using ZakYip.WheelDiverterSorter.Core.Enums;

namespace ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;

/// <summary>
/// 驱动器配置（存储在LiteDB中，支持热更新）
/// </summary>
public class DriverConfiguration
{
    /// <summary>
    /// LiteDB自动生成的唯一标识
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 是否使用硬件驱动器（false则使用模拟驱动器）
    /// </summary>
    public bool UseHardwareDriver { get; set; } = false;

    /// <summary>
    /// 驱动器厂商类型
    /// </summary>
    /// <remarks>
    /// 可选值:
    /// - Mock: 模拟驱动器（用于测试）
    /// - Leadshine: 雷赛控制器
    /// - Siemens: 西门子PLC
    /// - Mitsubishi: 三菱PLC
    /// - Omron: 欧姆龙PLC
    /// - ShuDiNiao: 数递鸟摆轮设备
    /// - Modi: 莫迪摆轮设备
    /// </remarks>
    public DriverVendorType VendorType { get; set; } = DriverVendorType.Leadshine;

    /// <summary>
    /// 雷赛控制器配置
    /// </summary>
    public LeadshineDriverConfig? Leadshine { get; set; }

    /// <summary>
    /// 数递鸟摆轮设备配置
    /// </summary>
    public ShuDiNiaoDriverConfig? ShuDiNiao { get; set; }

    /// <summary>
    /// 莫迪摆轮设备配置
    /// </summary>
    public ModiDriverConfig? Modi { get; set; }

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
    public static DriverConfiguration GetDefault()
    {
        return new DriverConfiguration
        {
            UseHardwareDriver = false,
            VendorType = DriverVendorType.Leadshine,
            Leadshine = new LeadshineDriverConfig
            {
                CardNo = 0,
                Diverters = new List<DiverterDriverEntry>
                {
                    new() { DiverterId = 1, DiverterName = "D1", OutputStartBit = 0, FeedbackInputBit = 10 },
                    new() { DiverterId = 2, DiverterName = "D2", OutputStartBit = 2, FeedbackInputBit = 11 },
                    new() { DiverterId = 3, DiverterName = "D3", OutputStartBit = 4, FeedbackInputBit = 12 }
                }
            }
        };
    }

    /// <summary>
    /// 验证配置
    /// </summary>
    public (bool IsValid, string? ErrorMessage) Validate()
    {
        if (!Enum.IsDefined(typeof(DriverVendorType), VendorType))
        {
            return (false, "驱动器厂商类型无效");
        }

        if (UseHardwareDriver && VendorType == DriverVendorType.Leadshine && Leadshine == null)
        {
            return (false, "使用雷赛硬件驱动时，必须配置雷赛参数");
        }

        if (UseHardwareDriver && VendorType == DriverVendorType.ShuDiNiao && ShuDiNiao == null)
        {
            return (false, "使用数递鸟硬件驱动时，必须配置数递鸟参数");
        }

        if (UseHardwareDriver && VendorType == DriverVendorType.Modi && Modi == null)
        {
            return (false, "使用莫迪硬件驱动时，必须配置莫迪参数");
        }

        if (Leadshine != null)
        {
            if (Leadshine.Diverters == null || !Leadshine.Diverters.Any())
            {
                return (false, "雷赛摆轮配置不能为空");
            }

            // 检查DiverterId不能重复
            var duplicateIds = Leadshine.Diverters
                .GroupBy(d => d.DiverterId)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicateIds.Any())
            {
                return (false, $"摆轮ID重复: {string.Join(", ", duplicateIds)}");
            }
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
/// 雷赛控制器配置
/// </summary>
public class LeadshineDriverConfig
{
    /// <summary>
    /// 控制器卡号
    /// </summary>
    public ushort CardNo { get; set; } = 0;

    /// <summary>
    /// 摆轮配置列表
    /// </summary>
    public List<DiverterDriverEntry> Diverters { get; set; } = new();
}

/// <summary>
/// 摆轮驱动器配置条目
/// </summary>
public class DiverterDriverEntry
{
    /// <summary>
    /// 摆轮标识符（数字ID）
    /// </summary>
    public required long DiverterId { get; set; }

    /// <summary>
    /// 摆轮名称（可选）- Diverter Name (Optional)
    /// </summary>
    /// <remarks>
    /// 用于显示的友好名称，例如 "D1"、"1号摆轮"
    /// </remarks>
    public string? DiverterName { get; set; }

    /// <summary>
    /// 输出起始位
    /// </summary>
    public int OutputStartBit { get; set; }

    /// <summary>
    /// 反馈输入位
    /// </summary>
    public int FeedbackInputBit { get; set; }
}

/// <summary>
/// 数递鸟摆轮设备配置
/// </summary>
public record class ShuDiNiaoDriverConfig
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
/// 数递鸟摆轮设备条目
/// </summary>
public record class ShuDiNiaoDeviceEntry
{
    /// <summary>
    /// 摆轮标识符（字符串ID）
    /// </summary>
    public required string DiverterId { get; init; }

    /// <summary>
    /// TCP连接主机地址
    /// </summary>
    /// <example>192.168.0.100</example>
    public required string Host { get; init; }

    /// <summary>
    /// TCP连接端口
    /// </summary>
    /// <example>2000</example>
    public required int Port { get; init; }

    /// <summary>
    /// 设备地址（协议中的设备地址字节）
    /// </summary>
    /// <remarks>
    /// 0x51 表示 1 号设备，0x52 表示 2 号设备，依次类推
    /// </remarks>
    /// <example>0x51</example>
    public required byte DeviceAddress { get; init; }

    /// <summary>
    /// 是否启用该设备
    /// </summary>
    public bool IsEnabled { get; init; } = true;
}

/// <summary>
/// 莫迪摆轮设备配置
/// </summary>
public record class ModiDriverConfig
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

/// <summary>
/// 莫迪摆轮设备条目
/// </summary>
public record class ModiDeviceEntry
{
    /// <summary>
    /// 摆轮标识符（字符串ID）
    /// </summary>
    /// <example>D1</example>
    public required string DiverterId { get; init; }

    /// <summary>
    /// TCP连接主机地址
    /// </summary>
    /// <example>192.168.1.100</example>
    public required string Host { get; init; }

    /// <summary>
    /// TCP连接端口
    /// </summary>
    /// <example>8000</example>
    public required int Port { get; init; }

    /// <summary>
    /// 设备编号
    /// </summary>
    /// <remarks>
    /// 莫迪设备的内部编号，用于协议通信
    /// </remarks>
    /// <example>1</example>
    public required int DeviceId { get; init; }

    /// <summary>
    /// 是否启用该设备
    /// </summary>
    public bool IsEnabled { get; init; } = true;
}
