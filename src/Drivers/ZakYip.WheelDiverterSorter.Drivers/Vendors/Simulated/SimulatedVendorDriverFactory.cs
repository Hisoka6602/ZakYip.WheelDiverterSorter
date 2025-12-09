using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Hardware;
using ZakYip.WheelDiverterSorter.Core.Hardware.Devices;
using ZakYip.WheelDiverterSorter.Core.Hardware.IoLinkage;

namespace ZakYip.WheelDiverterSorter.Drivers.Vendors.Simulated;

/// <summary>
/// 模拟厂商驱动工厂
/// Simulated Vendor Driver Factory
/// </summary>
public class SimulatedVendorDriverFactory : IVendorDriverFactory
{
    private readonly ILoggerFactory _loggerFactory;

    public VendorId VendorId => VendorId.Simulated;

    public SimulatedVendorDriverFactory(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
    }

    public VendorCapabilities GetCapabilities()
    {
        return VendorCapabilities.Simulated;
    }

    public IReadOnlyList<IWheelDiverterDriver> CreateWheelDiverterDrivers()
    {
        // 模拟驱动器不需要创建具体的摆轮驱动器
        // 它们通常由 MockSwitchingPathExecutor 统一管理
        return Array.Empty<IWheelDiverterDriver>();
    }

    public IIoLinkageDriver CreateIoLinkageDriver()
    {
        var logger = _loggerFactory.CreateLogger<SimulatedIoLinkageDriver>();
        return new SimulatedIoLinkageDriver(logger);
    }

    public ISensorInputReader? CreateSensorInputReader()
    {
        var logger = _loggerFactory.CreateLogger<SimulatedSensorInputReader>();
        return new SimulatedSensorInputReader(logger);
    }
}
