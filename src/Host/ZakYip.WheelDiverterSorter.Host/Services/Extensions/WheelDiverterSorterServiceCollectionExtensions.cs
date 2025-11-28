using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Caching.Memory;
using ZakYip.WheelDiverterSorter.Drivers;
using ZakYip.WheelDiverterSorter.Drivers.Vendors.Leadshine;
using ZakYip.WheelDiverterSorter.Drivers.Vendors.Simulated;
using ZakYip.WheelDiverterSorter.Drivers.Vendors.ShuDiNiao;
using ZakYip.WheelDiverterSorter.Ingress;
using ZakYip.WheelDiverterSorter.Execution;
using ZakYip.WheelDiverterSorter.Execution.Routing;
using ZakYip.WheelDiverterSorter.Execution.Concurrency;
using ZakYip.WheelDiverterSorter.Communication;
using ZakYip.WheelDiverterSorter.Observability;
using ZakYip.WheelDiverterSorter.Observability.Utilities;
using ZakYip.WheelDiverterSorter.Core.Sorting;
using ZakYip.WheelDiverterSorter.Core.LineModel.Routing;
using ZakYip.WheelDiverterSorter.Host.Commands;
using ZakYip.WheelDiverterSorter.Host.Services.Application;
using ZakYip.WheelDiverterSorter.Host.Services.Workers;
using HostApplicationServices = ZakYip.WheelDiverterSorter.Host.Application.Services;

namespace ZakYip.WheelDiverterSorter.Host.Services.Extensions;

/// <summary>
/// WheelDiverterSorter 服务集合扩展方法
/// Unified DI entry point for all WheelDiverterSorter services
/// </summary>
/// <remarks>
/// 提供统一的服务注册入口，将所有 Core/Execution/Drivers/Ingress/Communication/Observability/Simulation 
/// 相关服务注册合并到单一扩展方法中，简化 Program.cs 的配置。
/// 
/// **使用方法**：
/// <code>
/// builder.Services.AddWheelDiverterSorter(builder.Configuration);
/// </code>
/// </remarks>
public static class WheelDiverterSorterServiceCollectionExtensions
{
    /// <summary>
    /// 注册 WheelDiverterSorter 相关的全部服务
    /// Registers all services required for the WheelDiverterSorter system
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置对象</param>
    /// <returns>服务集合</returns>
    /// <remarks>
    /// 该方法按以下顺序注册服务：
    /// 1. 基础设施服务（安全执行器、系统时钟、日志去重）
    /// 2. 运行时配置文件服务
    /// 3. 分拣系统配置选项
    /// 4. 性能监控和缓存服务
    /// 5. Prometheus 指标和告警服务
    /// 6. 包裹追踪和日志清理服务
    /// 7. 配置仓储服务
    /// 8. 拓扑服务
    /// 9. 应用层服务
    /// 10. 分拣服务
    /// 11. 驱动器服务（根据运行模式选择硬件/仿真）
    /// 12. 仿真模式提供者
    /// 13. 健康检查和系统状态管理
    /// 14. 健康状态提供器
    /// 15. 并发控制服务
    /// 16. 节点健康服务
    /// 17. 传感器服务
    /// 18. RuleEngine 通信服务
    /// 19. 通信统计服务
    /// 20. 改口功能服务
    /// 21. 中段皮带 IO 联动服务
    /// 22. 仿真服务
    /// 23. 后台工作服务
    /// </remarks>
    public static IServiceCollection AddWheelDiverterSorter(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 1. 添加基础设施服务（安全执行器、系统时钟、日志去重）
        services.AddInfrastructureServices();

        // 2. 添加运行时配置文件服务
        services.AddRuntimeProfile(configuration);

        // 3. 注册分拣系统强类型配置选项
        services.AddSortingSystemOptions();
        services.AddUpstreamConnectionOptions();
        services.AddRoutingOptions();

        // 4. 添加性能监控和缓存服务
        services.AddMemoryCache(options =>
        {
            options.SizeLimit = 1000;
        });
        services.AddMetrics();
        services.AddSingleton<SorterMetrics>();

        // 5. 添加 Prometheus 指标服务和告警服务
        services.AddPrometheusMetrics();
        services.AddAlarmService();
        services.AddAlertSinks();

        // 6. 添加包裹生命周期日志和追踪服务
        services.AddParcelLifecycleLogger();
        services.AddParcelTraceLogging();
        
        // 配置日志清理选项
        services.Configure<Observability.Tracing.LogCleanupOptions>(
            configuration.GetSection(Observability.Tracing.LogCleanupOptions.SectionName));
        services.AddLogCleanup();

        // 7. 注册所有配置仓储
        services.AddConfigurationRepositories(configuration);

        // 8. 注册拓扑服务
        services.AddTopologyServices();

        // 9. 注册应用层服务
        services.AddScoped<HostApplicationServices.ISystemConfigService, HostApplicationServices.SystemConfigService>();
        services.AddScoped<HostApplicationServices.ILoggingConfigService, HostApplicationServices.LoggingConfigService>();

        // 10. 注册分拣服务
        services.AddSortingServices(configuration);

        // 11. 根据运行模式注册驱动器服务
        var runtimeMode = configuration.GetValue<string>("Runtime:Mode") ?? "Production";
        var driverOptions = new DriverOptions();
        configuration.GetSection("Driver").Bind(driverOptions);
        services.AddSingleton(driverOptions);

        if (runtimeMode.Equals("Simulation", StringComparison.OrdinalIgnoreCase) ||
            runtimeMode.Equals("PerformanceTest", StringComparison.OrdinalIgnoreCase))
        {
            // 仿真/性能测试模式
            services.AddSimulatedIo()
                    .AddSimulatedConveyorLine();
        }
        else
        {
            // 生产模式
            services.AddLeadshineIo()
                    .AddShuDiNiaoWheelDiverter()
                    .AddSimulatedConveyorLine();
        }

        // 12. 注册仿真模式提供者
        services.AddScoped<ISimulationModeProvider, SimulationModeProvider>();

        // 13. 注册健康检查和系统状态管理
        var enableHealthCheck = configuration.GetValue<bool>("HealthCheck:Enabled", true);
        if (enableHealthCheck)
        {
            services.AddHealthCheckServices();
            services.AddSystemStateManagement(
                Core.Enums.System.SystemState.Booting,
                enableSelfTest: true);
        }
        else
        {
            services.AddSystemStateManagement(Core.Enums.System.SystemState.Ready);
        }

        // 14. 注册健康状态提供器
        services.AddSingleton<Observability.Runtime.Health.IHealthStatusProvider,
            Health.HostHealthStatusProvider>();

        // 15. 注册并发控制服务
        services.AddConcurrencyControl(configuration);
        services.DecorateWithConcurrencyControl();

        // 16. 注册节点健康服务
        services.AddNodeHealthServices();

        // 17. 注册传感器服务
        services.AddSensorServices(configuration);

        // 18. 注册 RuleEngine 通信服务
        services.AddRuleEngineCommunication(configuration);
        services.AddUpstreamConnectionManagement(configuration);

        // 19. 注册通信统计服务
        services.AddSingleton<CommunicationStatsService>();

        // 20. 注册改口功能服务
        services.AddSingleton<IRoutePlanRepository, InMemoryRoutePlanRepository>();
        services.AddSingleton<IRouteReplanner, RouteReplanner>();
        services.AddSingleton<ChangeParcelChuteCommandHandler>();

        // 21. 注册中段皮带 IO 联动服务
        services.AddMiddleConveyorServices(configuration);

        // 22. 注册仿真服务
        services.AddSimulationServices(configuration);

        // 23. 注册后台工作服务
        services.AddHostedService<AlarmMonitoringWorker>();
        services.AddHostedService<RouteTopologyConsistencyCheckWorker>();

        return services;
    }
}
