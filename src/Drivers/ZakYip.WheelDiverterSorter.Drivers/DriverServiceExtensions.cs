using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.LineModel;
using ZakYip.WheelDiverterSorter.Core.LineModel.Bindings;
using ZakYip.WheelDiverterSorter.Core.Hardware;
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
            
            if (options.UseHardwareDriver)
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

        // 使用工厂创建驱动实例
        if (options.UseHardwareDriver)
        {
            // 使用硬件驱动器
            services.AddSingleton<ISwitchingPathExecutor>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<HardwareSwitchingPathExecutor>>();
                var factory = sp.GetRequiredService<IVendorDriverFactory>();
                var drivers = factory.CreateWheelDiverterDrivers();
                return new HardwareSwitchingPathExecutor(logger, drivers);
            });

            // 注册 IO 联动驱动
            services.AddSingleton<IIoLinkageDriver>(sp =>
            {
                var factory = sp.GetRequiredService<IVendorDriverFactory>();
                return factory.CreateIoLinkageDriver();
            });
        }
        else
        {
            // 使用模拟驱动器
            services.AddSingleton<ISwitchingPathExecutor, MockSwitchingPathExecutor>();

            // 注册仿真 IO 联动驱动
            services.AddSingleton<IIoLinkageDriver>(sp =>
            {
                var factory = sp.GetRequiredService<IVendorDriverFactory>();
                return factory.CreateIoLinkageDriver();
            });
        }

        // 注册 IO 联动协调器（不管是硬件还是仿真模式都需要）
        services.AddSingleton<IIoLinkageCoordinator, DefaultIoLinkageCoordinator>();

        return services;
    }
}
