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

namespace ZakYip.WheelDiverterSorter.Drivers.Vendors.Simulated;

/// <summary>
/// 模拟驱动器服务注册扩展方法
/// Simulated Driver Service Collection Extensions
/// </summary>
/// <remarks>
/// 用于注册模拟/仿真驱动器相关实现到 DI 容器。
/// 使用方式：在 Program.cs 中调用 <c>builder.Services.AddSimulatedIo()</c>
/// </remarks>
public static class SimulatedDriverServiceCollectionExtensions
{
    /// <summary>
    /// 注册模拟 IO 实现
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <returns>服务集合</returns>
    /// <remarks>
    /// 注册以下服务：
    /// - <see cref="IVendorDriverFactory"/> -> <see cref="SimulatedVendorDriverFactory"/>
    /// - <see cref="IWheelDiverterDriverManager"/> -> <see cref="FactoryBasedDriverManager"/>
    /// - <see cref="IWheelCommandExecutor"/> -> <see cref="WheelCommandExecutor"/>
    /// - <see cref="ISwitchingPathExecutor"/> -> <see cref="MockSwitchingPathExecutor"/>
    /// - <see cref="IIoLinkageDriver"/> (通过工厂创建)
    /// - <see cref="IIoLinkageCoordinator"/> -> <see cref="DefaultIoLinkageCoordinator"/>
    /// </remarks>
    public static IServiceCollection AddSimulatedIo(this IServiceCollection services)
    {
        // 注册模拟厂商驱动工厂
        services.AddSingleton<IVendorDriverFactory>(sp =>
        {
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            return new SimulatedVendorDriverFactory(loggerFactory);
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

        // 注册 MockSwitchingPathExecutor
        services.AddSingleton<MockSwitchingPathExecutor>();
        
        // 使用模拟驱动器的路径执行器
        services.AddSingleton<ISwitchingPathExecutor>(sp => sp.GetRequiredService<MockSwitchingPathExecutor>());

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

    /// <summary>
    /// 注册模拟摆轮实现
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <returns>服务集合</returns>
    /// <remarks>
    /// 仅注册模拟摆轮相关服务，不包括 IO 相关服务。
    /// 用于需要混合使用真实 IO 和模拟摆轮的场景。
    /// </remarks>
    public static IServiceCollection AddSimulatedWheelDiverter(this IServiceCollection services)
    {
        // 注册模拟摆轮驱动管理器（返回空列表，使用 MockSwitchingPathExecutor）
        services.AddSingleton<IWheelDiverterDriverManager>(sp =>
        {
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            return new FactoryBasedDriverManager(Array.Empty<IWheelDiverterDriver>(), loggerFactory);
        });

        // 注册统一的摆轮命令执行器
        services.AddSingleton<IWheelCommandExecutor>(sp =>
        {
            var driverManager = sp.GetRequiredService<IWheelDiverterDriverManager>();
            var logger = sp.GetRequiredService<ILogger<WheelCommandExecutor>>();
            return new WheelCommandExecutor(driverManager, logger);
        });

        // 注册 MockSwitchingPathExecutor
        services.AddSingleton<MockSwitchingPathExecutor>();
        
        // 使用模拟驱动器的路径执行器
        services.AddSingleton<ISwitchingPathExecutor>(sp => sp.GetRequiredService<MockSwitchingPathExecutor>());

        return services;
    }

    /// <summary>
    /// 注册模拟线体实现
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <returns>服务集合</returns>
    /// <remarks>
    /// 注册以下服务：
    /// - <see cref="IConveyorLineSegmentDevice"/> -> <see cref="SimulatedConveyorLineSegmentDevice"/>
    /// </remarks>
    public static IServiceCollection AddSimulatedConveyorLine(this IServiceCollection services)
    {
        // 注册模拟线体段设备
        services.AddSingleton<IConveyorLineSegmentDevice>(sp =>
        {
            var logger = sp.GetService<ILogger<SimulatedConveyorLineSegmentDevice>>();
            return new SimulatedConveyorLineSegmentDevice("SimulatedConveyor", logger);
        });

        return services;
    }

    /// <summary>
    /// 注册模拟离散 IO 组实现
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="groupName">IO 组名称</param>
    /// <param name="portCount">端口数量</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddSimulatedDiscreteIoGroup(
        this IServiceCollection services,
        string groupName = "SimulatedIoGroup",
        int portCount = 32)
    {
        services.AddSingleton<IDiscreteIoGroup>(sp =>
        {
            var logger = sp.GetService<ILogger<SimulatedDiscreteIoGroup>>();
            return new SimulatedDiscreteIoGroup(groupName, portCount, logger);
        });

        return services;
    }
}
