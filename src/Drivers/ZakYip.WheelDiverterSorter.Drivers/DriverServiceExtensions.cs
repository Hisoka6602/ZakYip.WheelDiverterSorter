using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.LineModel;
using ZakYip.WheelDiverterSorter.Core.LineModel.Bindings;
using ZakYip.WheelDiverterSorter.Core.LineModel.Runtime;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Drivers;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Execution;
using ZakYip.WheelDiverterSorter.Drivers.Vendors.Leadshine;
using ZakYip.WheelDiverterSorter.Drivers.Vendors.Simulated;

namespace ZakYip.WheelDiverterSorter.Drivers;

/// <summary>
/// 驱动器服务注册扩展
/// Driver Service Registration Extensions
/// </summary>
/// <remarks>
/// <para>
/// 此类提供了一个向后兼容的方式来注册驱动器服务。
/// 推荐的方式是使用厂商特定的扩展方法（如 AddLeadshineIo、AddShuDiNiaoWheelDiverter 等）直接注册。
/// </para>
/// <para>
/// This class provides a backward-compatible way to register driver services.
/// The recommended approach is to use vendor-specific extension methods 
/// (e.g., AddLeadshineIo, AddShuDiNiaoWheelDiverter) for registration.
/// </para>
/// </remarks>
public static class DriverServiceExtensions
{
    /// <summary>
    /// 添加驱动器服务（向后兼容方法）
    /// Add driver services (backward compatible method)
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置</param>
    /// <returns>服务集合</returns>
    /// <remarks>
    /// <para>
    /// 此方法根据 IRuntimeProfile.UseHardwareDriver 或配置文件中的 "Driver:UseHardwareDriver" 
    /// 来决定使用硬件驱动还是模拟驱动。
    /// </para>
    /// <para>
    /// 推荐使用厂商特定的扩展方法替代此方法：
    /// - 雷赛 IO: <see cref="LeadshineIoServiceCollectionExtensions.AddLeadshineIo"/>
    /// - 模拟 IO: <see cref="SimulatedDriverServiceCollectionExtensions.AddSimulatedIo"/>
    /// </para>
    /// </remarks>
    [Obsolete("推荐使用厂商特定的扩展方法（如 AddLeadshineIo、AddSimulatedIo）替代此方法。")]
    public static IServiceCollection AddDriverServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 绑定配置
        var options = new DriverOptions();
        configuration.GetSection("Driver").Bind(options);
        services.AddSingleton(options);

        // 注册厂商驱动工厂（根据运行时配置决定使用硬件还是模拟）
        services.AddSingleton<IVendorDriverFactory>(sp =>
        {
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            
            // 优先使用 IRuntimeProfile 判断是否使用硬件驱动
            var runtimeProfile = sp.GetService<IRuntimeProfile>();
            var useHardwareDriver = runtimeProfile?.UseHardwareDriver ?? options.UseHardwareDriver;
            
            if (useHardwareDriver)
            {
                // 使用雷赛硬件驱动（默认硬件厂商）
                return new LeadshineVendorDriverFactory(loggerFactory, options.Leadshine);
            }
            else
            {
                // 使用模拟驱动器
                return new SimulatedVendorDriverFactory(loggerFactory);
            }
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

        // 使用统一执行器创建路径执行器（延迟到运行时决定使用哪种实现）
        services.AddSingleton<ISwitchingPathExecutor>(sp =>
        {
            var runtimeProfile = sp.GetService<IRuntimeProfile>();
            var useHardwareDriver = runtimeProfile?.UseHardwareDriver ?? options.UseHardwareDriver;
            
            if (useHardwareDriver)
            {
                // 使用硬件驱动器（通过统一的命令执行器）
                var logger = sp.GetRequiredService<ILogger<HardwareSwitchingPathExecutor>>();
                var commandExecutor = sp.GetRequiredService<IWheelCommandExecutor>();
                return new HardwareSwitchingPathExecutor(logger, commandExecutor);
            }
            else
            {
                // 使用模拟驱动器
                return sp.GetRequiredService<MockSwitchingPathExecutor>();
            }
        });
        
        // 注册 MockSwitchingPathExecutor（仅用于仿真/性能测试模式）
        services.AddSingleton<MockSwitchingPathExecutor>();

        // 注册 IO 联动驱动（始终通过工厂创建）
        services.AddSingleton<IIoLinkageDriver>(sp =>
        {
            var factory = sp.GetRequiredService<IVendorDriverFactory>();
            return factory.CreateIoLinkageDriver();
        });

        // 注册 IO 联动协调器（不管是硬件还是仿真模式都需要）
        services.AddSingleton<IIoLinkageCoordinator, DefaultIoLinkageCoordinator>();

        return services;
    }
}
