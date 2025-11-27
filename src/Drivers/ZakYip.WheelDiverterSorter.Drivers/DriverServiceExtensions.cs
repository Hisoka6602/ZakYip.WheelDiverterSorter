using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.LineModel;
using ZakYip.WheelDiverterSorter.Core.LineModel.Bindings;
using ZakYip.WheelDiverterSorter.Core.LineModel.Runtime;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Drivers.Abstractions;
using ZakYip.WheelDiverterSorter.Drivers.Vendors.Leadshine;
using ZakYip.WheelDiverterSorter.Drivers.Vendors.Simulated;
using ZakYip.WheelDiverterSorter.Execution;

namespace ZakYip.WheelDiverterSorter.Drivers;

/// <summary>
/// 驱动器服务注册扩展
/// </summary>
public static class DriverServiceExtensions
{
    /// <summary>
    /// 添加驱动器服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置</param>
    /// <returns>服务集合</returns>
    /// <remarks>
    /// 如果已注册 IRuntimeProfile，则使用其 UseHardwareDriver 属性来决定驱动器类型。
    /// 否则回退到读取配置 "Driver:UseHardwareDriver"（向后兼容）。
    /// If IRuntimeProfile is registered, uses its UseHardwareDriver property to determine driver type.
    /// Otherwise falls back to reading "Driver:UseHardwareDriver" configuration (backward compatible).
    /// </remarks>
    public static IServiceCollection AddDriverServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 绑定配置
        var options = new DriverOptions();
        configuration.GetSection("Driver").Bind(options);
        services.AddSingleton(options);

        // 注册厂商驱动工厂
        services.AddSingleton<IVendorDriverFactory>(sp =>
        {
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            
            // 优先使用 IRuntimeProfile 判断是否使用硬件驱动
            var runtimeProfile = sp.GetService<IRuntimeProfile>();
            var useHardwareDriver = runtimeProfile?.UseHardwareDriver ?? options.UseHardwareDriver;
            
            if (useHardwareDriver)
            {
                // 根据配置的 VendorId 创建对应的工厂
                // 如果配置中没有 VendorId，默认使用 Leadshine（向后兼容）
                var vendorId = options.VendorId ?? VendorId.Leadshine;
                
                return vendorId switch
                {
                    VendorId.Leadshine => new LeadshineVendorDriverFactory(loggerFactory, options.Leadshine),
                    VendorId.Simulated => new SimulatedVendorDriverFactory(loggerFactory),
                    _ => throw new NotSupportedException($"厂商 {vendorId} 尚未实现驱动工厂")
                };
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
