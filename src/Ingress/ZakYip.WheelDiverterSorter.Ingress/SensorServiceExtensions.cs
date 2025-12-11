using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using ZakYip.WheelDiverterSorter.Core.Enums;
using Microsoft.Extensions.DependencyInjection;
using ZakYip.WheelDiverterSorter.Ingress.Sensors;
using ZakYip.WheelDiverterSorter.Core.Hardware.Devices;
using ZakYip.WheelDiverterSorter.Core.Hardware.IoLinkage;
using ZakYip.WheelDiverterSorter.Core.Hardware.Mappings;
using ZakYip.WheelDiverterSorter.Core.Hardware.Ports;
using ZakYip.WheelDiverterSorter.Core.Hardware.Providers;
using ZakYip.WheelDiverterSorter.Ingress.Configuration;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Core.LineModel.Runtime;
using ZakYip.WheelDiverterSorter.Observability.Utilities;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;
using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using ZakYip.WheelDiverterSorter.Host.StateMachine;

namespace ZakYip.WheelDiverterSorter.Ingress;

/// <summary>
/// 传感器服务注册扩展
/// </summary>
/// <remarks>
/// 本扩展类属于 Ingress 层，负责注册传感器相关的服务。
/// 仅依赖 Core 层的抽象接口，不直接依赖具体驱动实现。
/// 具体驱动的注册应在 Host 层或 Drivers 层的服务扩展中完成。
/// 
/// **架构原则**：
/// - 默认使用真实硬件传感器（Leadshine/Siemens等）
/// - 只有在仿真模式下（IRuntimeProfile.IsSimulationMode == true）才使用Mock传感器
/// - 通过 POST /api/simulation/run-scenario-e 等端点进入仿真模式
/// </remarks>
public static class SensorServiceExtensions {

    /// <summary>
    /// 添加传感器服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置</param>
    /// <returns>服务集合</returns>
    /// <remarks>
    /// 调用此方法前，需确保以下服务已注册：
    /// - IInputPort（硬件传感器模式需要）
    /// - IRuntimeProfile（用于判断是否使用Mock传感器）
    /// 
    /// 传感器类型选择逻辑：
    /// - 如果 IRuntimeProfile.IsSimulationMode 返回 true：使用 MockSensor
    /// - 否则：使用真实硬件传感器（根据 VendorType 配置）
    /// </remarks>
    public static IServiceCollection AddSensorServices(
        this IServiceCollection services,
        IConfiguration configuration) {
        // 绑定配置
        var sensorOptions = new SensorOptions();
        configuration.GetSection("Sensor").Bind(sensorOptions);

        // 绑定包裹检测配置
        services.Configure<ParcelDetectionOptions>(
            options => configuration.GetSection("ParcelDetection").Bind(options));

        // 注册传感器工厂 - 运行时根据仿真模式动态选择
        // 这里注册一个工厂，它会在运行时检查 IRuntimeProfile
        services.AddSingleton<ISensorFactory>(sp => {
            var runtimeProfile = sp.GetService<IRuntimeProfile>();
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger("ZakYip.WheelDiverterSorter.Ingress.SensorServiceExtensions");
            
            // 判断是否为仿真模式
            bool isSimulationMode = runtimeProfile?.IsSimulationMode ?? false;
            
            if (isSimulationMode)
            {
                // 仿真模式：使用Mock传感器
                logger.LogInformation("系统运行在仿真模式下，使用Mock传感器");
                return CreateMockSensorFactory(sp, sensorOptions);
            }
            else
            {
                // 正常模式：使用真实硬件传感器
                logger.LogInformation("系统运行在真实硬件模式下，使用 {VendorType} 传感器", sensorOptions.VendorType);
                return CreateHardwareSensorFactory(sp, sensorOptions);
            }
        });

        // 注册传感器实例
        services.AddSingleton<IEnumerable<ISensor>>(sp => {
            var factory = sp.GetRequiredService<ISensorFactory>();
            return factory.CreateSensors();
        });

        // 注册包裹检测服务
        services.AddSingleton<IParcelDetectionService>(sp =>
        {
            var sensors = sp.GetRequiredService<IEnumerable<ISensor>>();
            var options = sp.GetService<IOptions<ParcelDetectionOptions>>();
            var logger = sp.GetService<ILogger<Services.ParcelDetectionService>>();
            var healthMonitor = sp.GetService<Services.ISensorHealthMonitor>();
            var sensorConfigRepo = sp.GetService<ISensorConfigurationRepository>();
            var systemStateManager = sp.GetService<ISystemStateManager>();
            
            return new Services.ParcelDetectionService(
                sensors,
                options,
                logger,
                healthMonitor,
                sensorConfigRepo,
                systemStateManager);
        });

        // 注册传感器健康监控服务
        services.AddSingleton<Services.ISensorHealthMonitor, Services.SensorHealthMonitor>();

        return services;
    }

    /// <summary>
    /// 创建硬件传感器工厂
    /// </summary>
    private static ISensorFactory CreateHardwareSensorFactory(
        IServiceProvider sp,
        SensorOptions sensorOptions)
    {
        switch (sensorOptions.VendorType) {
            case SensorVendorType.Leadshine:
                return CreateLeadshineSensorFactory(sp, sensorOptions);

            case SensorVendorType.Siemens:
            case SensorVendorType.Mitsubishi:
            case SensorVendorType.Omron:
                throw new NotImplementedException(
                    $"传感器厂商类型 {sensorOptions.VendorType} 尚未实现，请联系开发团队");

            default:
                throw new NotSupportedException(
                    $"不支持的传感器厂商类型: {sensorOptions.VendorType}");
        }
    }

    /// <summary>
    /// 创建雷赛传感器工厂
    /// </summary>
    private static ISensorFactory CreateLeadshineSensorFactory(
        IServiceProvider sp,
        SensorOptions sensorOptions)
    {
        var logger = sp.GetRequiredService<ILogger<LeadshineSensorFactory>>();
        var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
        var logDeduplicator = sp.GetRequiredService<ILogDeduplicator>();
        var inputPort = sp.GetRequiredService<IInputPort>();
        var configProvider = sp.GetRequiredService<ISensorVendorConfigProvider>();
        var systemClock = sp.GetRequiredService<ISystemClock>();
        
        return new LeadshineSensorFactory(
            logger,
            loggerFactory,
            logDeduplicator,
            inputPort,
            configProvider,
            systemClock,
            sensorOptions.PollingIntervalMs);
    }

    /// <summary>
    /// 创建Mock传感器工厂
    /// </summary>
    private static ISensorFactory CreateMockSensorFactory(
        IServiceProvider sp,
        SensorOptions sensorOptions)
    {
        // 如果没有配置模拟传感器，使用默认配置
        if (!sensorOptions.MockSensors.Any()) {
            sensorOptions.MockSensors = new List<MockSensorConfigDto>
            {
                new() { SensorId = 1, Type = SensorType.Photoelectric, IsEnabled = true },
                new() { SensorId = 2, Type = SensorType.Laser, IsEnabled = true }
            };
        }

        var logger = sp.GetRequiredService<ILogger<MockSensorFactory>>();
        return new MockSensorFactory(logger, sensorOptions.MockSensors);
    }
}