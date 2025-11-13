using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZakYip.WheelDiverterSorter.Drivers.Abstractions;
using ZakYip.WheelDiverterSorter.Drivers.Leadshine;
using ZakYip.WheelDiverterSorter.Ingress.Configuration;
using ZakYip.WheelDiverterSorter.Ingress.Sensors;

namespace ZakYip.WheelDiverterSorter.Ingress;

/// <summary>
/// 传感器服务注册扩展
/// </summary>
public static class SensorServiceExtensions
{
    /// <summary>
    /// 添加传感器服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddSensorServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 绑定配置
        var sensorOptions = new SensorOptions();
        configuration.GetSection("Sensor").Bind(sensorOptions);

        // 绑定包裹检测配置
        services.Configure<ParcelDetectionOptions>(
            options => configuration.GetSection("ParcelDetection").Bind(options));

        // 注册传感器工厂
        if (sensorOptions.UseHardwareSensor)
        {
            // 使用硬件传感器
            switch (sensorOptions.VendorType)
            {
                case SensorVendorType.Leadshine:
                    AddLeadshineSensorServices(services, sensorOptions);
                    break;

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
        else
        {
            // 使用模拟传感器
            AddMockSensorServices(services, sensorOptions);
        }

        // 注册包裹检测服务
        services.AddSingleton<IParcelDetectionService, Services.ParcelDetectionService>();

        // 注册传感器健康监控服务
        services.AddSingleton<Services.ISensorHealthMonitor, Services.SensorHealthMonitor>();

        return services;
    }

    /// <summary>
    /// 添加雷赛传感器服务
    /// </summary>
    private static void AddLeadshineSensorServices(
        IServiceCollection services,
        SensorOptions sensorOptions)
    {
        if (sensorOptions.Leadshine == null)
        {
            throw new InvalidOperationException("雷赛传感器配置不能为空");
        }

        // 注册输入端口
        services.AddSingleton<IInputPort>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<LeadshineInputPort>>();
            return new LeadshineInputPort(logger, sensorOptions.Leadshine.CardNo);
        });

        // 注册传感器工厂
        services.AddSingleton<ISensorFactory>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<LeadshineSensorFactory>>();
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var inputPort = sp.GetRequiredService<IInputPort>();
            return new LeadshineSensorFactory(
                logger,
                loggerFactory,
                inputPort,
                sensorOptions.Leadshine);
        });

        // 注册传感器实例
        services.AddSingleton<IEnumerable<ISensor>>(sp =>
        {
            var factory = sp.GetRequiredService<ISensorFactory>();
            return factory.CreateSensors();
        });
    }

    /// <summary>
    /// 添加模拟传感器服务
    /// </summary>
    private static void AddMockSensorServices(
        IServiceCollection services,
        SensorOptions sensorOptions)
    {
        // 如果没有配置模拟传感器，使用默认配置
        if (!sensorOptions.MockSensors.Any())
        {
            sensorOptions.MockSensors = new List<MockSensorConfigDto>
            {
                new() { SensorId = "SENSOR_PE_01", Type = SensorType.Photoelectric, IsEnabled = true },
                new() { SensorId = "SENSOR_LASER_01", Type = SensorType.Laser, IsEnabled = true }
            };
        }

        // 注册传感器工厂
        services.AddSingleton<ISensorFactory>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<MockSensorFactory>>();
            return new MockSensorFactory(logger, sensorOptions.MockSensors);
        });

        // 注册传感器实例
        services.AddSingleton<IEnumerable<ISensor>>(sp =>
        {
            var factory = sp.GetRequiredService<ISensorFactory>();
            return factory.CreateSensors();
        });
    }
}
