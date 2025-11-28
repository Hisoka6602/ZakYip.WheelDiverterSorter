using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Hardware;
using ZakYip.WheelDiverterSorter.Core.LineModel;
using ZakYip.WheelDiverterSorter.Core.LineModel.Bindings;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Drivers;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Execution;

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
    /// - <see cref="IVendorDriverFactory"/> -> <see cref="LeadshineVendorDriverFactory"/>
    /// - <see cref="IWheelDiverterDriverManager"/> -> <see cref="FactoryBasedDriverManager"/>
    /// - <see cref="IWheelCommandExecutor"/> -> <see cref="WheelCommandExecutor"/>
    /// - <see cref="ISwitchingPathExecutor"/> -> <see cref="HardwareSwitchingPathExecutor"/>
    /// - <see cref="IIoLinkageDriver"/> (通过工厂创建)
    /// - <see cref="IIoLinkageCoordinator"/> -> <see cref="DefaultIoLinkageCoordinator"/>
    /// </remarks>
    public static IServiceCollection AddLeadshineIo(this IServiceCollection services)
    {
        // 注册雷赛厂商驱动工厂
        services.AddSingleton<IVendorDriverFactory>(sp =>
        {
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var options = sp.GetRequiredService<DriverOptions>();
            return new LeadshineVendorDriverFactory(loggerFactory, options.Leadshine);
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

        // 注册 IO 联动驱动（通过工厂创建）
        services.AddSingleton<IIoLinkageDriver>(sp =>
        {
            var factory = sp.GetRequiredService<IVendorDriverFactory>();
            return factory.CreateIoLinkageDriver();
        });

        // 注册 IO 联动协调器
        services.AddSingleton<IIoLinkageCoordinator, DefaultIoLinkageCoordinator>();

        return services;
    }
}
