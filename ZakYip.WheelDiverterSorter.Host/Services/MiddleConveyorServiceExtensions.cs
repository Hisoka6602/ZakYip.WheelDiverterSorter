using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core;
using ZakYip.WheelDiverterSorter.Core.Configuration;
using ZakYip.WheelDiverterSorter.Drivers.Abstractions;
using ZakYip.WheelDiverterSorter.Drivers.Leadshine;
using ZakYip.WheelDiverterSorter.Drivers.Simulated;
using ZakYip.WheelDiverterSorter.Execution;

namespace ZakYip.WheelDiverterSorter.Host.Services;

/// <summary>
/// 中段皮带 IO 联动服务注册扩展。
/// </summary>
public static class MiddleConveyorServiceExtensions
{
    /// <summary>
    /// 注册中段皮带 IO 联动相关服务
    /// </summary>
    public static IServiceCollection AddMiddleConveyorServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 绑定配置
        var options = new MiddleConveyorIoOptions
        {
            Enabled = true,
            IsSimulationMode = false,
            Segments = Array.Empty<ConveyorIoMapping>()
        };
        configuration.GetSection("MiddleConveyorIo").Bind(options);

        // 注册配置为单例
        services.AddSingleton(options);

        if (!options.Enabled)
        {
            // 如果未启用，注册空实现
            services.AddSingleton<IMiddleConveyorCoordinator>(sp =>
                new MiddleConveyorCoordinator(
                    Array.Empty<IConveyorSegment>(),
                    options,
                    sp.GetRequiredService<ILogger<MiddleConveyorCoordinator>>()));
            return services;
        }

        // 根据仿真模式选择驱动实现
        if (options.IsSimulationMode)
        {
            // 仿真模式：注册仿真驱动
            services.AddSingleton<IMiddleConveyorCoordinator>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<MiddleConveyorCoordinator>>();
                var segments = CreateConveyorSegments(
                    options.Segments,
                    isSimulation: true,
                    sp);

                return new MiddleConveyorCoordinator(segments, options, logger);
            });
        }
        else
        {
            // 生产模式：注册硬件驱动
            services.AddSingleton<IMiddleConveyorCoordinator>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<MiddleConveyorCoordinator>>();
                var segments = CreateConveyorSegments(
                    options.Segments,
                    isSimulation: false,
                    sp);

                return new MiddleConveyorCoordinator(segments, options, logger);
            });
        }

        return services;
    }

    private static IReadOnlyList<IConveyorSegment> CreateConveyorSegments(
        IReadOnlyList<ConveyorIoMapping> mappings,
        bool isSimulation,
        IServiceProvider serviceProvider)
    {
        var segments = new List<IConveyorSegment>();

        foreach (var mapping in mappings)
        {
            IConveyorSegmentDriver driver;

            if (isSimulation)
            {
                // 创建仿真驱动
                driver = new SimulatedConveyorSegmentDriver(
                    mapping,
                    serviceProvider.GetRequiredService<ILogger<SimulatedConveyorSegmentDriver>>());
            }
            else
            {
                // 创建硬件驱动（雷赛）
                var emcController = serviceProvider.GetRequiredService<IEmcController>();
                driver = new LeadshineConveyorSegmentDriver(
                    mapping,
                    emcController,
                    serviceProvider.GetRequiredService<ILogger<LeadshineConveyorSegmentDriver>>());
            }

            // 创建 ConveyorSegment
            var segment = new ConveyorSegment(
                driver,
                serviceProvider.GetRequiredService<ILogger<ConveyorSegment>>());

            segments.Add(segment);
        }

        return segments;
    }
}
