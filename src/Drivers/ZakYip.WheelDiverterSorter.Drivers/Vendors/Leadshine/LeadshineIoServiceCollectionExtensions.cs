using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Hardware;
using ZakYip.WheelDiverterSorter.Core.LineModel;
using ZakYip.WheelDiverterSorter.Core.LineModel.Bindings;
using ZakYip.WheelDiverterSorter.Core.Hardware.Devices;
using ZakYip.WheelDiverterSorter.Core.Hardware.IoLinkage;
using ZakYip.WheelDiverterSorter.Core.Hardware.Mappings;
using ZakYip.WheelDiverterSorter.Core.Hardware.Ports;
using ZakYip.WheelDiverterSorter.Core.Hardware.Providers;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Execution;
using ZakYip.WheelDiverterSorter.Drivers.Vendors.Leadshine.Configuration;

namespace ZakYip.WheelDiverterSorter.Drivers.Vendors.Leadshine;

/// <summary>
/// 雷赛 IO 服务注册扩展方法
/// Leadshine IO Service Collection Extensions
/// </summary>
/// <remarks>
/// 用于注册雷赛 IO 相关实现到 DI 容器。
/// 使用方式：在 Program.cs 中调用 <c>builder.Services.AddLeadshineIo()</c>
/// </remarks>
public static class LeadshineIoServiceCollectionExtensions
{
    /// <summary>
    /// 注册雷赛 IO 实现
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <returns>服务集合</returns>
    /// <remarks>
    /// 注册以下服务：
    /// - <see cref="IEmcController"/> -> <see cref="LeadshineEmcController"/> (Singleton)
    /// - <see cref="IVendorDriverFactory"/> -> <see cref="LeadshineVendorDriverFactory"/>
    /// - <see cref="IWheelDiverterDriverManager"/> -> <see cref="FactoryBasedDriverManager"/>
    /// - <see cref="IWheelCommandExecutor"/> -> <see cref="WheelCommandExecutor"/>
    /// - <see cref="ISwitchingPathExecutor"/> -> <see cref="HardwareSwitchingPathExecutor"/>
    /// - <see cref="IIoLinkageDriver"/> (通过工厂创建，与摆轮驱动器共享EMC控制器)
    /// - <see cref="IIoLinkageCoordinator"/> -> <see cref="DefaultIoLinkageCoordinator"/>
    /// - <see cref="ISensorVendorConfigProvider"/> -> <see cref="LeadshineSensorVendorConfigProvider"/>
    /// - <see cref="IInputPort"/> -> <see cref="LeadshineInputPort"/> (用于面板输入读取、IO状态查询等)
    /// - <see cref="IOutputPort"/> -> <see cref="LeadshineOutputPort"/> (用于IO输出控制)
    /// </remarks>
    public static IServiceCollection AddLeadshineIo(this IServiceCollection services)
    {
        // 注册 EMC 控制器为 Singleton（雷赛 IO 驱动和摆轮驱动共享同一个EMC控制器实例）
        services.AddSingleton<IEmcController>(sp =>
        {
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var options = sp.GetRequiredService<DriverOptions>();
            var emcLogger = loggerFactory.CreateLogger<LeadshineEmcController>();
            
            var emcController = new LeadshineEmcController(
                emcLogger,
                options.Leadshine.CardNo,
                options.Leadshine.PortNo,
                options.Leadshine.ControllerIp);
            
            // 同步初始化
            var initResult = emcController.InitializeAsync().GetAwaiter().GetResult();
            
            if (!initResult)
            {
                var errorMessage =
                    $"EMC 控制器初始化失败。CardNo: {options.Leadshine.CardNo}, PortNo: {options.Leadshine.PortNo}, " +
                    $"ControllerIp: {options.Leadshine.ControllerIp ?? "N/A (PCI Mode)"}。\n" +
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
                    options.Leadshine.CardNo,
                    options.Leadshine.PortNo,
                    options.Leadshine.ControllerIp ?? "N/A (PCI Mode)");
            }
            
            return emcController;
        });
        
        // 注册雷赛厂商驱动工厂（使用已注册的 EMC 控制器）
        services.AddSingleton<IVendorDriverFactory>(sp =>
        {
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var options = sp.GetRequiredService<DriverOptions>();
            var emcController = sp.GetRequiredService<IEmcController>();
            return new LeadshineVendorDriverFactory(loggerFactory, options.Leadshine, emcController);
        });

        // 注册 IWheelDiverterDriverManager（基于工厂驱动器列表的适配器）
        services.AddSingleton<IWheelDiverterDriverManager>(sp =>
        {
            var factory = sp.GetRequiredService<IVendorDriverFactory>();
            var drivers = factory.CreateWheelDiverterDrivers();
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            return new FactoryBasedDriverManager(drivers, loggerFactory);
        });

        // 注册统一的摆轮命令执行器
        services.AddSingleton<IWheelCommandExecutor>(sp =>
        {
            var driverManager = sp.GetRequiredService<IWheelDiverterDriverManager>();
            var logger = sp.GetRequiredService<ILogger<WheelCommandExecutor>>();
            return new WheelCommandExecutor(driverManager, logger);
        });

        // 使用硬件驱动器的路径执行器
        services.AddSingleton<ISwitchingPathExecutor>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<HardwareSwitchingPathExecutor>>();
            var commandExecutor = sp.GetRequiredService<IWheelCommandExecutor>();
            return new HardwareSwitchingPathExecutor(logger, commandExecutor);
        });

        // 注册 IO 联动驱动（通过工厂创建，共享EMC控制器）
        services.AddSingleton<IIoLinkageDriver>(sp =>
        {
            var factory = sp.GetRequiredService<IVendorDriverFactory>();
            return factory.CreateIoLinkageDriver();
        });

        // 注册 IO 联动协调器
        services.AddSingleton<IIoLinkageCoordinator, DefaultIoLinkageCoordinator>();

        // 注册传感器厂商配置提供者
        services.AddSingleton<ISensorVendorConfigProvider>(sp =>
        {
            var options = sp.GetRequiredService<DriverOptions>();
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger<LeadshineSensorVendorConfigProvider>();

            if (options.Sensor == null)
            {
                logger.LogWarning(
                    "雷赛传感器配置 (DriverOptions.Sensor) 未设置，使用空配置。" +
                    "如果需要使用传感器功能，请在 appsettings.json 中配置 Driver:Sensor 节点。");
                return new LeadshineSensorVendorConfigProvider(new LeadshineSensorOptions());
            }
            return new LeadshineSensorVendorConfigProvider(options.Sensor);
        });

        // 注册 IInputPort 和 IOutputPort（用于面板输入读取等功能）
        services.AddSingleton<IInputPort>(sp =>
        {
            var options = sp.GetRequiredService<DriverOptions>();
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger<LeadshineInputPort>();
            return new LeadshineInputPort(logger, options.Leadshine.CardNo);
        });

        services.AddSingleton<IOutputPort>(sp =>
        {
            var options = sp.GetRequiredService<DriverOptions>();
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var emcController = sp.GetRequiredService<IEmcController>();
            var logger = loggerFactory.CreateLogger<LeadshineOutputPort>();
            return new LeadshineOutputPort(logger, options.Leadshine.CardNo, emcController);
        });

        return services;
    }
}
