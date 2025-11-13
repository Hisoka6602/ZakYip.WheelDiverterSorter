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
    /// 驱动器厂商类型
    /// </summary>
    public DriverVendorType VendorType { get; set; } = DriverVendorType.Leadshine;

    /// <summary>
    /// 雷赛控制器配置
    /// </summary>
    public LeadshineOptions Leadshine { get; set; } = new();
}
