using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core;
using ZakYip.WheelDiverterSorter.Core.Hardware;
using ZakYip.WheelDiverterSorter.Drivers.Abstractions;
using ZakYip.WheelDiverterSorter.Drivers.Vendors.Simulated;

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

    public IConveyorSegmentDriver? CreateConveyorSegmentDriver(string segmentId)
    {
        // 创建模拟的传送带段驱动器
        var logger = _loggerFactory.CreateLogger<SimulatedConveyorSegmentDriver>();
        
        // 创建一个默认的 mapping 配置
        var mapping = new Core.Configuration.ConveyorIoMapping
        {
            SegmentKey = segmentId,
            DisplayName = $"Simulated-{segmentId}",
            StartOutputChannel = 0
        };
        
        return new SimulatedConveyorSegmentDriver(mapping, logger);
    }
}
