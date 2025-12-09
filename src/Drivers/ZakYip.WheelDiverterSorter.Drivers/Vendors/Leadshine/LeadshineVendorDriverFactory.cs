using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.LineModel;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Hardware;
using ZakYip.WheelDiverterSorter.Core.Hardware.Devices;
using ZakYip.WheelDiverterSorter.Core.Hardware.IoLinkage;
using ZakYip.WheelDiverterSorter.Core.Hardware.Mappings;
using ZakYip.WheelDiverterSorter.Core.Hardware.Ports;
using ZakYip.WheelDiverterSorter.Core.Hardware.Providers;
using ZakYip.WheelDiverterSorter.Drivers.Vendors.Leadshine.Configuration;
using ZakYip.WheelDiverterSorter.Core.LineModel.Segments;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;

namespace ZakYip.WheelDiverterSorter.Drivers.Vendors.Leadshine;

/// <summary>
/// 雷赛厂商驱动工厂
/// Leadshine Vendor Driver Factory
/// </summary>
/// <remarks>
/// 基于 ZakYip.Singulation 项目的 LeadshineLtdmcBusAdapter 实现。
/// 支持以太网模式（需要 ControllerIp）和本地 PCI 模式（ControllerIp 为空）。
/// </remarks>
public class LeadshineVendorDriverFactory : IVendorDriverFactory
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly LeadshineOptions _options;
    private readonly IEmcController _emcController;

    public VendorId VendorId => VendorId.Leadshine;

    public LeadshineVendorDriverFactory(
        ILoggerFactory loggerFactory,
        LeadshineOptions options,
        IEmcController emcController)
    {
        _loggerFactory = loggerFactory;
        _options = options;
        _emcController = emcController ?? throw new ArgumentNullException(nameof(emcController));
        
        var emcLogger = _loggerFactory.CreateLogger<LeadshineEmcController>();
        
        // EMC 控制器已经由 DI 容器初始化，这里只需要记录状态
        if (!_emcController.IsAvailable())
        {
            emcLogger.LogWarning(
                "EMC 控制器处于不可用状态。CardNo: {CardNo}, PortNo: {PortNo}, ControllerIp: {ControllerIp}。" +
                "所有 IO 操作将返回失败。",
                options.CardNo,
                options.PortNo,
                options.ControllerIp ?? "N/A (PCI Mode)");
        }
        else
        {
            emcLogger.LogInformation(
                "使用已初始化的 EMC 控制器。CardNo: {CardNo}, PortNo: {PortNo}, ControllerIp: {ControllerIp}",
                options.CardNo,
                options.PortNo,
                options.ControllerIp ?? "N/A (PCI Mode)");
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

            // 创建摆轮驱动器，传入 EMC 控制器实例以检查初始化状态
            var driverLogger = _loggerFactory.CreateLogger<LeadshineWheelDiverterDriver>();
            var driver = new LeadshineWheelDiverterDriver(driverLogger, _options.CardNo, config, _emcController);
            drivers.Add(driver);
        }

        return drivers;
    }

    public IIoLinkageDriver CreateIoLinkageDriver()
    {
        var logger = _loggerFactory.CreateLogger<LeadshineIoLinkageDriver>();
        return new LeadshineIoLinkageDriver(logger, _emcController);
    }

    public ISensorInputReader? CreateSensorInputReader()
    {
        var logger = _loggerFactory.CreateLogger<LeadshineSensorInputReader>();
        return new LeadshineSensorInputReader(_emcController, logger);
    }
}
