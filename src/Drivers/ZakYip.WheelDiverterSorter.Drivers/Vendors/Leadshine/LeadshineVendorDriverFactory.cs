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
    private readonly IEmcController? _emcController;

    public VendorId VendorId => VendorId.Leadshine;

    public LeadshineVendorDriverFactory(
        ILoggerFactory loggerFactory,
        LeadshineOptions options)
    {
        _loggerFactory = loggerFactory;
        _options = options;

        // 初始化 EMC 控制器（传入 ControllerIp 和 PortNo）
        var emcLogger = _loggerFactory.CreateLogger<LeadshineEmcController>();
        _emcController = new LeadshineEmcController(
            emcLogger, 
            options.CardNo, 
            options.PortNo, 
            options.ControllerIp);
        
        // 同步初始化
        var initTask = _emcController.InitializeAsync();
        initTask.Wait();
        
        if (!initTask.Result)
        {
            emcLogger.LogWarning(
                "EMC 控制器初始化失败，IO 联动功能可能无法正常工作。CardNo: {CardNo}, PortNo: {PortNo}, ControllerIp: {ControllerIp}",
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

            // 直接创建摆轮驱动器（已移除 IDiverterController 中间层）
            var driverLogger = _loggerFactory.CreateLogger<LeadshineWheelDiverterDriver>();
            var driver = new LeadshineWheelDiverterDriver(driverLogger, _options.CardNo, config);
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

        // 从配置中查找对应的传送带段映射
        var mapping = _options.ConveyorSegmentMappings
            .FirstOrDefault(m => m.SegmentKey == segmentId);

        if (mapping == null)
        {
            // 没有找到对应的配置，记录日志并返回 null
            var logger = _loggerFactory.CreateLogger<LeadshineVendorDriverFactory>();
            logger.LogWarning(
                "未找到传送带段映射配置: SegmentId={SegmentId}，已配置的段: [{ConfiguredSegments}]",
                segmentId,
                string.Join(", ", _options.ConveyorSegmentMappings.Select(m => m.SegmentKey)));
            return null;
        }

        var driverLogger = _loggerFactory.CreateLogger<LeadshineConveyorSegmentDriver>();
        return new LeadshineConveyorSegmentDriver(mapping, _emcController, driverLogger);
    }

    public ISensorInputReader? CreateSensorInputReader()
    {
        if (_emcController == null)
        {
            return null;
        }

        var logger = _loggerFactory.CreateLogger<LeadshineSensorInputReader>();
        return new LeadshineSensorInputReader(_emcController, logger);
    }
}
