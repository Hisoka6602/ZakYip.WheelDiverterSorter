using ZakYip.WheelDiverterSorter.Core.Enums;

namespace ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;

/// <summary>
/// IO驱动器配置（存储在LiteDB中，支持热更新）
/// </summary>
/// <remarks>
/// 此配置用于【IO驱动器】，而非【摆轮驱动器】。两者是不同的概念：
/// 
/// - **IO驱动器**（本配置）: 控制IO端点（输入/输出位），用于传感器信号读取和继电器控制
///   - 雷赛（Leadshine）: 雷赛运动控制卡的IO接口
///   - 西门子（Siemens）: S7系列PLC的IO模块
///   - 三菱（Mitsubishi）: 三菱PLC的IO模块
///   - 欧姆龙（Omron）: 欧姆龙PLC的IO模块
/// 
/// - **摆轮驱动器**（WheelDiverterConfiguration）: 控制摆轮转向（左转、右转、回中）
///   - 数递鸟（ShuDiNiao）: 通过TCP协议直接控制
///   - 莫迪（Modi）: 通过TCP协议直接控制
/// </remarks>
public class DriverConfiguration
{
    /// <summary>
    /// LiteDB自动生成的唯一标识
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 是否使用硬件驱动器（false则使用模拟驱动器）
    /// </summary>
    /// <remarks>
    /// 默认为 true（使用硬件驱动，不使用仿真）
    /// </remarks>
    public bool UseHardwareDriver { get; set; } = true;

    /// <summary>
    /// IO驱动器厂商类型
    /// </summary>
    /// <remarks>
    /// 可选值:
    /// - Mock: 模拟驱动器（用于测试）
    /// - Leadshine: 雷赛运动控制卡
    /// - Siemens: 西门子PLC
    /// - Mitsubishi: 三菱PLC
    /// - Omron: 欧姆龙PLC
    /// 
    /// 注意：摆轮设备（如数递鸟、莫迪）的配置已分离到 WheelDiverterConfiguration
    /// </remarks>
    public DriverVendorType VendorType { get; set; } = DriverVendorType.Leadshine;

    /// <summary>
    /// 雷赛运动控制卡配置
    /// </summary>
    public LeadshineDriverConfig? Leadshine { get; set; }

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
    /// <remarks>
    /// 默认配置：
    /// - UseHardwareDriver = true（使用硬件驱动，不使用仿真）
    /// - VendorType = Leadshine（默认雷赛控制器）
    /// - ControllerIp = "192.168.5.11"
    /// - CardNo = 8
    /// - PortNo = 2
    /// </remarks>
    public static DriverConfiguration GetDefault()
    {
        var now = ConfigurationDefaults.DefaultTimestamp;
        return new DriverConfiguration
        {
            UseHardwareDriver = true,
            VendorType = DriverVendorType.Leadshine,
            Leadshine = new LeadshineDriverConfig
            {
                ControllerIp = "192.168.5.11",
                CardNo = 8,
                PortNo = 2,
                Diverters = new List<DiverterDriverEntry>
                {
                    new() { DiverterId = 1, DiverterName = "D1", OutputStartBit = 0, FeedbackInputBit = 10 },
                    new() { DiverterId = 2, DiverterName = "D2", OutputStartBit = 2, FeedbackInputBit = 11 },
                    new() { DiverterId = 3, DiverterName = "D3", OutputStartBit = 4, FeedbackInputBit = 12 }
                }
            },
            CreatedAt = now,
            UpdatedAt = now
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

        return (true, null);
    }
}

/// <summary>
/// 雷赛控制器配置
/// </summary>
/// <remarks>
/// 基于 ZakYip.Singulation 项目的 LeadshineLtdmcBusAdapter 实现。
/// 支持以太网模式（需要 ControllerIp）和本地 PCI 模式（ControllerIp 为空）。
/// </remarks>
public class LeadshineDriverConfig
{
    /// <summary>
    /// 控制器IP地址（以太网模式）
    /// </summary>
    /// <remarks>
    /// 以太网模式需要配置控制器的IP地址。
    /// 如果为空或null，则使用本地PCI模式（dmc_board_init）。
    /// 如果配置了IP，则使用以太网模式（dmc_board_init_eth）。
    /// 默认值为 "192.168.5.11"。
    /// </remarks>
    /// <example>192.168.5.11</example>
    public string? ControllerIp { get; set; } = "192.168.5.11";

    /// <summary>
    /// 控制器卡号
    /// </summary>
    /// <remarks>
    /// 默认值为 8。
    /// </remarks>
    /// <example>8</example>
    public ushort CardNo { get; set; } = 8;

    /// <summary>
    /// 端口号（CAN/EtherCAT端口编号）
    /// </summary>
    /// <remarks>
    /// 默认值为 2。
    /// </remarks>
    /// <example>2</example>
    public ushort PortNo { get; set; } = 2;

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
/// 数递鸟摆轮设备条目
/// </summary>
/// <remarks>
/// 摆轮的路由配置（左/右/直行格口、前感应IO）已在格口路径拓扑配置中定义，
/// 此处仅包含设备通信相关配置。
/// </remarks>
public record class ShuDiNiaoDeviceEntry
{
    /// <summary>
    /// 摆轮标识符（数字ID）
    /// </summary>
    public required long DiverterId { get; init; }

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
/// 莫迪摆轮设备条目
/// </summary>
/// <remarks>
/// 摆轮的路由配置（左/右/直行格口、前感应IO）已在格口路径拓扑配置中定义，
/// 此处仅包含设备通信相关配置。
/// </remarks>
public record class ModiDeviceEntry
{
    /// <summary>
    /// 摆轮标识符（数字ID）
    /// </summary>
    /// <example>1</example>
    public required long DiverterId { get; init; }

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
