using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Hardware;
using ZakYip.WheelDiverterSorter.Core.Hardware.Devices;
using ZakYip.WheelDiverterSorter.Core.Hardware.IoLinkage;
using ZakYip.WheelDiverterSorter.Core.LineModel.Segments;

namespace ZakYip.WheelDiverterSorter.Drivers;

/// <summary>
/// 厂商驱动工厂接口
/// Vendor Driver Factory Interface - Creates vendor-specific driver implementations
/// </summary>
/// <remarks>
/// PR-TD9: 摆轮控制统一通过 <see cref="IWheelDiverterDriver"/> 接口暴露，
/// 不再允许多套同义抽象（IWheelDiverterActuator）并行。
/// </remarks>
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
    /// <remarks>
    /// 所有摆轮控制统一通过此方法创建 <see cref="IWheelDiverterDriver"/> 实例。
    /// PR-TD9: 已移除重复的 CreateWheelDiverterActuators() 方法。
    /// </remarks>
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

    /// <summary>
    /// 创建传感器输入读取器
    /// </summary>
    /// <returns>传感器输入读取器实例，如果不支持则返回null</returns>
    ISensorInputReader? CreateSensorInputReader();
}
