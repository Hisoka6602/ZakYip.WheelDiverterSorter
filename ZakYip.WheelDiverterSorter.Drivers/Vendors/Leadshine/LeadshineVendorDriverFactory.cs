using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core;
using ZakYip.WheelDiverterSorter.Core.Configuration;
using ZakYip.WheelDiverterSorter.Core.Hardware;
using ZakYip.WheelDiverterSorter.Drivers.Abstractions;
using ZakYip.WheelDiverterSorter.Drivers.Vendors.Leadshine;

namespace ZakYip.WheelDiverterSorter.Drivers.Vendors.Leadshine;

/// <summary>
/// 雷赛厂商驱动工厂
/// Leadshine Vendor Driver Factory
/// </summary>
public class LeadshineVendorDriverFactory : IVendorDriverFactory
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly LeadshineOptions _options;
    private readonly IEmcController? _emcController;

    public VendorId VendorId => VendorId.Leadshine;

    public LeadshineVendorDriverFactory(
        ILoggerFactory loggerFactory,
        LeadshineOptions options)
    {
        _loggerFactory = loggerFactory;
        _options = options;

        // 初始化 EMC 控制器
        var emcLogger = _loggerFactory.CreateLogger<LeadshineEmcController>();
        _emcController = new LeadshineEmcController(emcLogger, options.CardNo);
        
        // 同步初始化
        var initTask = _emcController.InitializeAsync();
        initTask.Wait();
        
        if (!initTask.Result)
        {
            emcLogger.LogWarning("EMC 控制器初始化失败，IO 联动功能可能无法正常工作");
        }
    }

    public VendorCapabilities GetCapabilities()
    {
        return VendorCapabilities.Leadshine;
    }

    public IReadOnlyList<IWheelDiverterDriver> CreateWheelDiverterDrivers()
    {
        var drivers = new List<IWheelDiverterDriver>();

        foreach (var configDto in _options.Diverters)
        {
            var config = new LeadshineDiverterConfig
            {
                DiverterId = configDto.DiverterId,
                DiverterName = configDto.DiverterName,
                ConnectedConveyorLengthMm = configDto.ConnectedConveyorLengthMm,
                ConnectedConveyorSpeedMmPerSec = configDto.ConnectedConveyorSpeedMmPerSec,
                DiverterSpeedMmPerSec = configDto.DiverterSpeedMmPerSec,
                OutputStartBit = configDto.OutputStartBit,
                FeedbackInputBit = configDto.FeedbackInputBit
            };

            // 创建底层控制器
            var controllerLogger = _loggerFactory.CreateLogger<LeadshineDiverterController>();
            var controller = new LeadshineDiverterController(controllerLogger, _options.CardNo, config);

            // 封装为高层驱动器
            var driverLogger = _loggerFactory.CreateLogger<RelayWheelDiverterDriver>();
            var driver = new RelayWheelDiverterDriver(driverLogger, controller);
            drivers.Add(driver);
        }

        return drivers;
    }

    public IIoLinkageDriver CreateIoLinkageDriver()
    {
        if (_emcController == null)
        {
            throw new InvalidOperationException("EMC 控制器未初始化");
        }

        var logger = _loggerFactory.CreateLogger<LeadshineIoLinkageDriver>();
        return new LeadshineIoLinkageDriver(logger, _emcController);
    }

    public IConveyorSegmentDriver? CreateConveyorSegmentDriver(string segmentId)
    {
        if (_emcController == null)
        {
            throw new InvalidOperationException("EMC 控制器未初始化");
        }

        // 这里需要一个 ConveyorIoMapping 配置，实际使用时应从配置中获取
        // 暂时返回 null，表示需要额外配置
        var logger = _loggerFactory.CreateLogger<LeadshineConveyorSegmentDriver>();
        return null;
    }

    public IReadOnlyList<IWheelDiverterActuator> CreateWheelDiverterActuators()
    {
        // Leadshine 实际硬件需要通过具体的 IWheelDiverterDriver 来操作
        // 硬件抽象层主要用于模拟和测试场景
        // 这里返回空列表，表示需要通过 CreateWheelDiverterDrivers() 获取驱动
        return Array.Empty<IWheelDiverterActuator>();
    }

    public ISensorInputReader? CreateSensorInputReader()
    {
        // Leadshine 传感器读取通过 EMC 控制器和 IO 联动驱动器实现
        // 硬件抽象层主要用于模拟和测试场景
        // 实际硬件使用时返回 null
        return null;
    }
}
