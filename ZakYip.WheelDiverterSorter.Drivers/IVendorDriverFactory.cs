using ZakYip.WheelDiverterSorter.Core;
using ZakYip.WheelDiverterSorter.Core.Hardware;
using ZakYip.WheelDiverterSorter.Drivers.Abstractions;

namespace ZakYip.WheelDiverterSorter.Drivers;

/// <summary>
/// 厂商驱动工厂接口
/// Vendor Driver Factory Interface - Creates vendor-specific driver implementations
/// </summary>
public interface IVendorDriverFactory
{
    /// <summary>
    /// 获取当前配置的厂商ID
    /// </summary>
    VendorId VendorId { get; }

    /// <summary>
    /// 获取厂商能力声明
    /// </summary>
    VendorCapabilities GetCapabilities();

    /// <summary>
    /// 创建摆轮驱动器列表
    /// </summary>
    /// <returns>摆轮驱动器列表</returns>
    IReadOnlyList<IWheelDiverterDriver> CreateWheelDiverterDrivers();

    /// <summary>
    /// 创建IO联动驱动器
    /// </summary>
    /// <returns>IO联动驱动器实例</returns>
    IIoLinkageDriver CreateIoLinkageDriver();

    /// <summary>
    /// 创建传送带段驱动器
    /// </summary>
    /// <param name="segmentId">传送带段ID</param>
    /// <returns>传送带段驱动器实例，如果不支持则返回null</returns>
    IConveyorSegmentDriver? CreateConveyorSegmentDriver(string segmentId);
}
