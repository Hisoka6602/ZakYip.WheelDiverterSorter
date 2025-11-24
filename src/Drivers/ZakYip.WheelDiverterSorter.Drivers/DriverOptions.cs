using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Hardware;

namespace ZakYip.WheelDiverterSorter.Drivers;

/// <summary>
/// 驱动器配置选项
/// </summary>
public class DriverOptions
{
    /// <summary>
    /// 是否使用硬件驱动器（false则使用模拟驱动器）
    /// </summary>
    public bool UseHardwareDriver { get; set; } = false;

    /// <summary>
    /// 驱动器厂商类型（旧版，保留向后兼容）
    /// </summary>
    public DriverVendorType VendorType { get; set; } = DriverVendorType.Leadshine;

    /// <summary>
    /// 厂商标识符（新版，推荐使用）
    /// </summary>
    public VendorId? VendorId { get; set; }

    /// <summary>
    /// 雷赛控制器配置
    /// </summary>
    public LeadshineOptions Leadshine { get; set; } = new();
}
