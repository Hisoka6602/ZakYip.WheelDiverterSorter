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
            var errorMessage = 
                $"EMC 控制器初始化失败。CardNo: {options.CardNo}, PortNo: {options.PortNo}, " +
                $"ControllerIp: {options.ControllerIp ?? "N/A (PCI Mode)"}。\n" +
                $"可能原因：\n" +
                $"1) 控制卡未连接或未通电\n" +
                $"2) IP地址配置错误（以太网模式）\n" +
                $"3) LTDMC.dll 未正确安装\n" +
                $"参考雷赛示例代码，dmc_board_init_eth 或 dmc_board_init 必须返回 0 才能进行后续 IO 操作。\n" +
                $"ErrorCode=9 表示控制卡未初始化，请确保在调用 dmc_write_outbit 前控制卡已成功初始化。";
            
            emcLogger.LogError(errorMessage);
            
            // EMC 初始化失败时，驱动器将处于不可用状态
            // 实际调用 IO 操作时，会检查 IsAvailable() 并返回失败
            // 这种设计允许在测试环境中容错，同时在生产环境中通过日志监控发现问题
            emcLogger.LogWarning(
                "EMC 控制器将处于不可用状态。所有 IO 操作将返回失败。如果这是生产环境，请立即检查硬件连接和配置。");
        }
        else
        {
            emcLogger.LogInformation(
                "EMC 控制器初始化成功。CardNo: {CardNo}, PortNo: {PortNo}, ControllerIp: {ControllerIp}",
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
        if (_emcController == null)
        {
            throw new InvalidOperationException("EMC 控制器未初始化，无法创建摆轮驱动器");
        }

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
