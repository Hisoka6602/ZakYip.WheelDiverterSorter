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
        // PR-SWAGGER-FIX: 不在 DI 注册时同步初始化，避免阻塞应用启动
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
            
            // PR-SWAGGER-FIX: 不在 DI 注册时同步初始化
            // EMC 控制器将在后台服务（IoLinkageInitHostedService）中异步初始化
            // 或在首次实际使用时延迟初始化
            emcLogger.LogInformation(
                "EMC 控制器已创建（未初始化）。CardNo: {CardNo}, PortNo: {PortNo}, ControllerIp: {ControllerIp}",
                options.Leadshine.CardNo,
                options.Leadshine.PortNo,
                options.Leadshine.ControllerIp ?? "N/A (PCI Mode)");
            
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
        // 使用数据库配置（从 LiteDB SensorConfiguration）而不是 appsettings.json
        services.AddSingleton<ISensorVendorConfigProvider>(sp =>
        {
            var sensorRepository = sp.GetRequiredService<Core.LineModel.Configuration.Repositories.Interfaces.ISensorConfigurationRepository>();
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger<DatabaseBackedLeadshineSensorVendorConfigProvider>();
            var options = sp.GetRequiredService<DriverOptions>();

            return new DatabaseBackedLeadshineSensorVendorConfigProvider(
                sensorRepository,
                logger,
                options.Leadshine.CardNo);
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
