using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Drivers.Vendors.Leadshine.Configuration;

namespace ZakYip.WheelDiverterSorter.Drivers;

/// <summary>
/// 驱动器配置选项
/// Driver Configuration Options
/// </summary>
/// <remarks>
/// <para>
/// 此配置类用于从 appsettings.json 中绑定驱动器配置。
/// VendorType 和 VendorId 属性已弃用，推荐使用 DI 扩展方法直接注册厂商实现。
/// </para>
/// <para>
/// This configuration class is used to bind driver configuration from appsettings.json.
/// VendorType and VendorId properties are deprecated. 
/// Use DI extension methods to register vendor implementations directly.
/// </para>
/// </remarks>
public class DriverOptions
{
    /// <summary>
    /// 是否使用硬件驱动器（false则使用模拟驱动器）
    /// </summary>
    /// <remarks>
    /// 默认为 true（使用硬件驱动，不使用仿真）
    /// </remarks>
    public bool UseHardwareDriver { get; set; } = true;

    /// <summary>
    /// 驱动器厂商类型（已弃用）
    /// </summary>
    /// <remarks>
    /// 此属性已弃用，请使用 DI 扩展方法（如 AddLeadshineIo）来选择厂商。
    /// This property is deprecated. Use DI extension methods (e.g., AddLeadshineIo) to select vendor.
    /// </remarks>
    [Obsolete("此属性已弃用，请使用 DI 扩展方法（如 AddLeadshineIo）来选择厂商。")]
    public DriverVendorType VendorType { get; set; } = DriverVendorType.Leadshine;

    /// <summary>
    /// 厂商标识符（已弃用）
    /// </summary>
    /// <remarks>
    /// 此属性已弃用，请使用 DI 扩展方法（如 AddLeadshineIo）来选择厂商。
    /// This property is deprecated. Use DI extension methods (e.g., AddLeadshineIo) to select vendor.
    /// </remarks>
    [Obsolete("此属性已弃用，请使用 DI 扩展方法（如 AddLeadshineIo）来选择厂商。")]
    public VendorId? VendorId { get; set; }

    /// <summary>
    /// 雷赛控制器配置
    /// </summary>
    public Vendors.Leadshine.Configuration.LeadshineOptions Leadshine { get; set; } = new();
}
