using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.LineModel;
using ZakYip.WheelDiverterSorter.Core.LineModel.Bindings;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;
using ZakYip.WheelDiverterSorter.Core.LineModel.Runtime;
using ZakYip.WheelDiverterSorter.Core.LineModel.Segments;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Drivers;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Drivers;
using ZakYip.WheelDiverterSorter.Execution;
using ZakYip.WheelDiverterSorter.Execution.Segments;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Execution;

namespace ZakYip.WheelDiverterSorter.Host.Services;

/// <summary>
/// 中段皮带 IO 联动服务注册扩展。
/// </summary>
public static class MiddleConveyorServiceExtensions
{
    /// <summary>
    /// 注册中段皮带 IO 联动相关服务
    /// </summary>
    /// <remarks>
    /// 如果已注册 IRuntimeProfile，则使用其 IsSimulationMode 属性来决定驱动器类型。
    /// 否则回退到读取配置 "MiddleConveyorIo:IsSimulationMode"（向后兼容）。
    /// If IRuntimeProfile is registered, uses its IsSimulationMode property to determine driver type.
    /// Otherwise falls back to reading "MiddleConveyorIo:IsSimulationMode" configuration (backward compatible).
    /// </remarks>
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

        // 使用工厂模式延迟到运行时决定使用哪种驱动实现
        services.AddSingleton<IMiddleConveyorCoordinator>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<MiddleConveyorCoordinator>>();
            
            // 优先使用 IRuntimeProfile 判断是否为仿真模式
            var runtimeProfile = sp.GetService<IRuntimeProfile>();
            var isSimulation = runtimeProfile?.IsSimulationMode ?? options.IsSimulationMode;
            
            var segments = CreateConveyorSegments(
                options.Segments,
                isSimulation,
                sp);

            return new MiddleConveyorCoordinator(segments, options, logger);
        });

        return services;
    }

    private static IReadOnlyList<IConveyorSegment> CreateConveyorSegments(
        IReadOnlyList<ConveyorIoMapping> mappings,
        bool isSimulation,
        IServiceProvider serviceProvider)
    {
        var segments = new List<IConveyorSegment>();

        // 获取厂商驱动工厂
        var factory = serviceProvider.GetService<IVendorDriverFactory>();

        foreach (var mapping in mappings)
        {
            IConveyorSegmentDriver? driver = null;

            if (factory != null)
            {
                // 使用工厂创建驱动
                driver = factory.CreateConveyorSegmentDriver(mapping.SegmentKey);
            }

            // 如果工厂不支持或未提供，回退到直接创建
            if (driver == null)
            {
                if (isSimulation)
                {
                    // 创建仿真驱动
                    driver = new Drivers.Vendors.Simulated.SimulatedConveyorSegmentDriver(
                        mapping,
                        serviceProvider.GetRequiredService<ILogger<Drivers.Vendors.Simulated.SimulatedConveyorSegmentDriver>>());
                }
                else
                {
                    // 创建硬件驱动（雷赛）
                    var emcController = serviceProvider.GetRequiredService<IEmcController>();
                    driver = new Drivers.Vendors.Leadshine.LeadshineConveyorSegmentDriver(
                        mapping,
                        emcController,
                        serviceProvider.GetRequiredService<ILogger<Drivers.Vendors.Leadshine.LeadshineConveyorSegmentDriver>>());
                }
            }

            // 创建 ConveyorSegment
            var segment = new ConveyorSegment(
                driver,
                serviceProvider.GetRequiredService<ILogger<ConveyorSegment>>(),
                serviceProvider.GetRequiredService<ISystemClock>());

            segments.Add(segment);
        }

        return segments;
    }
}
