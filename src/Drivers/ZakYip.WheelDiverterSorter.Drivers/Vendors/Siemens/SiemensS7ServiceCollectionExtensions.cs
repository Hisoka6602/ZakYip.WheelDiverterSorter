using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Hardware.Devices;
using ZakYip.WheelDiverterSorter.Drivers.Vendors.Siemens.Configuration;
using S7DiverterConfig = ZakYip.WheelDiverterSorter.Drivers.Vendors.Siemens.Configuration.S7DiverterConfigDto;

namespace ZakYip.WheelDiverterSorter.Drivers.Vendors.Siemens;

/// <summary>
/// 西门子 S7 PLC 服务注册扩展方法
/// Siemens S7 PLC Service Collection Extensions
/// </summary>
/// <remarks>
/// 用于注册西门子 S7 PLC 相关实现到 DI 容器。
/// 使用方式：在 Program.cs 中调用 <c>builder.Services.AddSiemensS7()</c>
/// </remarks>
public static class SiemensS7ServiceCollectionExtensions
{
    /// <summary>
    /// 注册西门子 S7 PLC 实现
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="options">S7 配置选项</param>
    /// <returns>服务集合</returns>
    /// <remarks>
    /// 注册以下服务：
    /// - <see cref="S7Connection"/> (用于 PLC 连接管理)
    /// - <see cref="IWheelDiverterDriverManager"/> -> S7 驱动管理器
    /// </remarks>
    public static IServiceCollection AddSiemensS7(this IServiceCollection services, S7Options options)
    {
        // 注册 S7 连接管理器
        services.AddSingleton(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<S7Connection>>();
            return new S7Connection(logger, options);
        });

        // 注册西门子摆轮驱动管理器
        services.AddSingleton<IWheelDiverterDriverManager>(sp =>
        {
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var connection = sp.GetRequiredService<S7Connection>();
            
            // 从配置创建驱动器
            var drivers = new List<IWheelDiverterDriver>();
            foreach (var diverterConfig in options.Diverters)
            {
                var outputPort = new S7OutputPort(
                    loggerFactory.CreateLogger<S7OutputPort>(),
                    connection,
                    diverterConfig.OutputDbNumber);
                
                // 直接创建 S7 摆轮驱动器（已移除 IDiverterController 中间层）
                // PR-CONFIG-HOTRELOAD02: S7DiverterConfig 现在是 S7DiverterConfigDto 的别名，直接使用
                var driver = new S7WheelDiverterDriver(
                    loggerFactory.CreateLogger<S7WheelDiverterDriver>(),
                    outputPort,
                    diverterConfig);
                    
                drivers.Add(driver);
            }
            
            return new FactoryBasedDriverManager(drivers, loggerFactory);
        });

        return services;
    }
}
