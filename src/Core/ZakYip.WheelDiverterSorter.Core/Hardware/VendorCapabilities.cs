using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;

namespace ZakYip.WheelDiverterSorter.Core.Hardware;

/// <summary>
/// 厂商驱动能力声明
/// Vendor Capabilities - Declares what hardware capabilities a vendor driver supports
/// </summary>
/// <remarks>
/// 用于声明某个厂商驱动实现支持哪些硬件能力。
/// 在运行时可检查驱动是否支持特定功能，便于灵活配置和降级处理。
/// </remarks>
public record VendorCapabilities
{
    /// <summary>
    /// 厂商标识
    /// </summary>
    public required VendorId VendorId { get; init; }

    /// <summary>
    /// 厂商名称（友好显示名称）
    /// </summary>
    public required string VendorName { get; init; }

    /// <summary>
    /// 是否支持摆轮控制
    /// </summary>
    public bool SupportsWheelDiverter { get; init; }

    /// <summary>
    /// 是否支持传送带驱动控制
    /// </summary>
    public bool SupportsConveyorDrive { get; init; }

    /// <summary>
    /// 是否支持传感器输入读取
    /// </summary>
    public bool SupportsSensorInput { get; init; }

    /// <summary>
    /// 是否支持报警输出控制（三色灯/蜂鸣器）
    /// </summary>
    public bool SupportsAlarmOutput { get; init; }

    /// <summary>
    /// 是否支持 IO 联动
    /// </summary>
    public bool SupportsIoLinkage { get; init; }

    /// <summary>
    /// 是否支持 EMC 分布式锁
    /// </summary>
    public bool SupportsDistributedLock { get; init; }

    /// <summary>
    /// 支持的通信协议列表（如 TCP, UDP, Serial, Modbus 等）
    /// </summary>
    public IReadOnlyList<string> SupportedProtocols { get; init; } = Array.Empty<string>();

    /// <summary>
    /// 最大摆轮数量（0表示无限制）
    /// </summary>
    public int MaxDiverterCount { get; init; }

    /// <summary>
    /// 最大传送带段数量（0表示无限制）
    /// </summary>
    public int MaxConveyorSegmentCount { get; init; }

    /// <summary>
    /// 附加能力描述（扩展字段）
    /// </summary>
    public IDictionary<string, object>? ExtendedCapabilities { get; init; }

    /// <summary>
    /// 获取模拟驱动的能力声明
    /// </summary>
    public static VendorCapabilities Simulated => new()
    {
        VendorId = VendorId.Simulated,
        VendorName = "模拟驱动器",
        SupportsWheelDiverter = true,
        SupportsConveyorDrive = true,
        SupportsSensorInput = true,
        SupportsAlarmOutput = true,
        SupportsIoLinkage = true,
        SupportsDistributedLock = false,
        SupportedProtocols = new[] { "InMemory" },
        MaxDiverterCount = 0,
        MaxConveyorSegmentCount = 0
    };

    /// <summary>
    /// 获取雷赛驱动的能力声明
    /// </summary>
    public static VendorCapabilities Leadshine => new()
    {
        VendorId = VendorId.Leadshine,
        VendorName = "雷赛智能",
        SupportsWheelDiverter = true,
        SupportsConveyorDrive = true,
        SupportsSensorInput = true,
        SupportsAlarmOutput = true,
        SupportsIoLinkage = true,
        SupportsDistributedLock = true,
        SupportedProtocols = new[] { "EMC-DLL", "Native" },
        MaxDiverterCount = 16,
        MaxConveyorSegmentCount = 8
    };

    /// <summary>
    /// 获取西门子驱动的能力声明
    /// </summary>
    public static VendorCapabilities Siemens => new()
    {
        VendorId = VendorId.Siemens,
        VendorName = "西门子",
        SupportsWheelDiverter = true,
        SupportsConveyorDrive = true,
        SupportsSensorInput = true,
        SupportsAlarmOutput = true,
        SupportsIoLinkage = true,
        SupportsDistributedLock = false,
        SupportedProtocols = new[] { "S7", "TCP", "Profinet" },
        MaxDiverterCount = 0,
        MaxConveyorSegmentCount = 0
    };

    /// <summary>
    /// 获取欧姆龙驱动的能力声明
    /// </summary>
    public static VendorCapabilities Omron => new()
    {
        VendorId = VendorId.Omron,
        VendorName = "欧姆龙",
        SupportsWheelDiverter = true,
        SupportsConveyorDrive = true,
        SupportsSensorInput = true,
        SupportsAlarmOutput = true,
        SupportsIoLinkage = true,
        SupportsDistributedLock = false,
        SupportedProtocols = new[] { "FINS", "TCP", "UDP", "Serial" },
        MaxDiverterCount = 0,
        MaxConveyorSegmentCount = 0
    };
}
