using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;
using ZakYip.WheelDiverterSorter.Core.Hardware.Devices;
using ZakYip.WheelDiverterSorter.Core.Hardware.IoLinkage;
using ZakYip.WheelDiverterSorter.Core.Hardware.Mappings;
using ZakYip.WheelDiverterSorter.Core.Hardware.Ports;
using ZakYip.WheelDiverterSorter.Core.Hardware.Providers;

namespace ZakYip.WheelDiverterSorter.Drivers.Vendors.Modi;

/// <summary>
/// 莫迪摆轮服务注册扩展方法
/// Modi Wheel Diverter Service Collection Extensions
/// </summary>
/// <remarks>
/// 用于注册莫迪摆轮相关实现到 DI 容器。
/// 使用方式：在 Program.cs 中调用 <c>builder.Services.AddModiWheelDiverter()</c>
/// </remarks>
public static class ModiWheelServiceCollectionExtensions
{
    /// <summary>
    /// 注册莫迪摆轮实现
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <returns>服务集合</returns>
    /// <remarks>
    /// 注册以下服务：
    /// - <see cref="IWheelDiverterDriverManager"/> -> Modi 驱动管理器
    /// </remarks>
    public static IServiceCollection AddModiWheelDiverter(this IServiceCollection services)
    {
        // 注册莫迪摆轮驱动管理器
        services.AddSingleton<IWheelDiverterDriverManager>(sp =>
        {
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var configRepo = sp.GetService<IWheelDiverterConfigurationRepository>();
            
            // 从配置仓储获取配置，如果没有则使用默认配置
            var config = configRepo?.Get() ?? WheelDiverterConfiguration.GetDefault();
            var enabledDevices = GetEnabledModiDevices(config).ToList();
            
            if (enabledDevices.Count == 0)
            {
                var logger = loggerFactory.CreateLogger("ModiWheelDiverter");
                logger.LogWarning("莫迪摆轮配置为空，将使用空配置初始化驱动管理器");
                
                // 返回一个空的 FactoryBasedDriverManager
                return new FactoryBasedDriverManager(Array.Empty<IWheelDiverterDriver>(), loggerFactory);
            }
            
            // 创建 Modi 驱动器列表
            var drivers = new List<IWheelDiverterDriver>();
            foreach (var device in enabledDevices)
            {
                var driverLogger = loggerFactory.CreateLogger<ModiWheelDiverterDriver>();
                drivers.Add(new ModiWheelDiverterDriver(device, driverLogger));
            }
            
            return new FactoryBasedDriverManager(drivers, loggerFactory);
        });

        return services;
    }

    /// <summary>
    /// 注册莫迪摆轮实现（仿真模式）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <returns>服务集合</returns>
    /// <remarks>
    /// 注册莫迪摆轮的仿真实现，用于测试和开发环境。
    /// </remarks>
    public static IServiceCollection AddModiWheelDiverterSimulated(this IServiceCollection services)
    {
        // 注册莫迪摆轮驱动管理器（仿真模式）
        services.AddSingleton<IWheelDiverterDriverManager>(sp =>
        {
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var configRepo = sp.GetService<IWheelDiverterConfigurationRepository>();
            
            // 从配置仓储获取配置
            var config = configRepo?.Get() ?? WheelDiverterConfiguration.GetDefault();
            var enabledDevices = GetEnabledModiDevices(config).ToList();
            
            // 始终使用仿真设备（因为这是仿真方法）
            var simulatedDrivers = new List<IWheelDiverterDriver>();
            foreach (var device in enabledDevices)
            {
                var simLogger = loggerFactory.CreateLogger<ModiSimulatedDevice>();
                simulatedDrivers.Add(new ModiSimulatedDevice(device, simLogger));
            }
            
            return new FactoryBasedDriverManager(simulatedDrivers, loggerFactory);
        });

        return services;
    }
    
    /// <summary>
    /// 获取已启用的 Modi 设备列表
    /// </summary>
    private static IEnumerable<ModiDeviceEntry> GetEnabledModiDevices(WheelDiverterConfiguration config)
    {
        return config.Modi?.Devices?.Where(d => d.IsEnabled) ?? Enumerable.Empty<ModiDeviceEntry>();
    }
}
