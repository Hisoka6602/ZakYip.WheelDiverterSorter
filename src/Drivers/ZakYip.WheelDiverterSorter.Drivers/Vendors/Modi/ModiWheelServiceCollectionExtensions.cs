using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;
using ZakYip.WheelDiverterSorter.Drivers.Abstractions;

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
            
            if (config.Modi == null || !config.Modi.Devices.Any())
            {
                var logger = loggerFactory.CreateLogger("ModiWheelDiverter");
                logger.LogWarning("莫迪摆轮配置为空，将使用空配置初始化驱动管理器");
                
                // 返回一个空的 FactoryBasedDriverManager
                return new FactoryBasedDriverManager(Array.Empty<IWheelDiverterDriver>(), loggerFactory);
            }
            
            // 创建 Modi 驱动器列表
            var drivers = new List<IWheelDiverterDriver>();
            foreach (var device in config.Modi.Devices.Where(d => d.IsEnabled))
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
            
            if (config.Modi == null || !config.Modi.Devices.Any() || config.Modi.UseSimulation)
            {
                // 仿真模式，返回模拟设备
                var simulatedDrivers = new List<IWheelDiverterDriver>();
                
                // 如果有配置，按配置创建模拟设备
                if (config.Modi?.Devices != null)
                {
                    foreach (var device in config.Modi.Devices.Where(d => d.IsEnabled))
                    {
                        var simLogger = loggerFactory.CreateLogger<ModiSimulatedDevice>();
                        simulatedDrivers.Add(new ModiSimulatedDevice(device, simLogger));
                    }
                }
                
                return new FactoryBasedDriverManager(simulatedDrivers, loggerFactory);
            }
            
            // 如果配置不要求仿真，但调用了仿真方法，强制使用仿真
            var drivers = new List<IWheelDiverterDriver>();
            foreach (var device in config.Modi.Devices.Where(d => d.IsEnabled))
            {
                var simLogger = loggerFactory.CreateLogger<ModiSimulatedDevice>();
                drivers.Add(new ModiSimulatedDevice(device, simLogger));
            }
            
            return new FactoryBasedDriverManager(drivers, loggerFactory);
        });

        return services;
    }
}
