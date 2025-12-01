using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Communication;
using ZakYip.WheelDiverterSorter.Core.Hardware.Devices;
using ZakYip.WheelDiverterSorter.Core.Hardware.IoLinkage;
using ZakYip.WheelDiverterSorter.Core.Hardware.Mappings;
using ZakYip.WheelDiverterSorter.Core.Hardware.Ports;
using ZakYip.WheelDiverterSorter.Core.Hardware.Providers;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Execution;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Ingress;
using ZakYip.WheelDiverterSorter.Core.Enums.System;
using ZakYip.WheelDiverterSorter.Core.LineModel;
using ZakYip.WheelDiverterSorter.Core.LineModel.Bindings;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;
using ZakYip.WheelDiverterSorter.Configuration.Persistence.Repositories.LiteDb;
using ZakYip.WheelDiverterSorter.Core.LineModel.Orchestration;
using ZakYip.WheelDiverterSorter.Core.LineModel.Runtime;
using ZakYip.WheelDiverterSorter.Core.LineModel.Routing;
using ZakYip.WheelDiverterSorter.Core.LineModel.Segments;
using ZakYip.WheelDiverterSorter.Core.LineModel.Topology;
using ZakYip.WheelDiverterSorter.Core.Sorting;
using ZakYip.WheelDiverterSorter.Core.Sorting.Orchestration;
using ZakYip.WheelDiverterSorter.Core.Sorting.Policies;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Drivers;
using ZakYip.WheelDiverterSorter.Drivers.Vendors.Leadshine;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware.Vendors;
using ZakYip.WheelDiverterSorter.Drivers.Vendors.ShuDiNiao;
using ZakYip.WheelDiverterSorter.Drivers.Vendors.Simulated;
using ZakYip.WheelDiverterSorter.Execution;
using ZakYip.WheelDiverterSorter.Execution.Concurrency;
using ZakYip.WheelDiverterSorter.Execution.Extensions;
using ZakYip.WheelDiverterSorter.Execution.Orchestration;
using ZakYip.WheelDiverterSorter.Execution.PathExecution;
using ZakYip.WheelDiverterSorter.Execution.Routing;
using ZakYip.WheelDiverterSorter.Execution.Segments;
using ZakYip.WheelDiverterSorter.Ingress;
using ZakYip.WheelDiverterSorter.Ingress.Adapters;
using ZakYip.WheelDiverterSorter.Ingress.Services;
using ZakYip.WheelDiverterSorter.Observability;
using ZakYip.WheelDiverterSorter.Observability.Utilities;
using ZakYip.WheelDiverterSorter.Application.Services;
using ZakYip.WheelDiverterSorter.Application.Services.Config;
using ZakYip.WheelDiverterSorter.Application.Services.Caching;
using ZakYip.WheelDiverterSorter.Application.Services.Health;
using ZakYip.WheelDiverterSorter.Application.Services.Sorting;
using ZakYip.WheelDiverterSorter.Application.Services.Simulation;
using ZakYip.WheelDiverterSorter.Application.Services.Metrics;
using ZakYip.WheelDiverterSorter.Application.Services.Topology;
using ZakYip.WheelDiverterSorter.Application.Services.Debug;

namespace ZakYip.WheelDiverterSorter.Application.Extensions;

/// <summary>
/// WheelDiverterSorter 服务集合扩展方法
/// Unified DI entry point for all WheelDiverterSorter services
/// </summary>
/// <remarks>
/// 提供统一的服务注册入口，将所有 Core/Execution/Drivers/Ingress/Communication/Observability/Simulation 
/// 相关服务注册合并到 Application 层，使 Host 层只需调用这一个方法即可完成所有服务注册。
/// 
/// **依赖关系**：
/// Host → Application → (Core/Execution/Drivers/Ingress/Communication/Observability/Simulation)
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
    /// 8. 应用层服务
    /// 9. 分拣服务
    /// 10. 驱动器服务（根据运行模式选择硬件/仿真）
    /// 11. 并发控制服务
    /// 12. 节点健康服务
    /// 13. 传感器服务
    /// 14. RuleEngine 通信服务
    /// 15. 改口功能服务
    /// 16. 中段皮带 IO 联动服务
    /// 17. 仿真服务
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

        // 8. 注册应用层服务（调用 ApplicationServiceExtensions）
        services.AddWheelDiverterApplication();

        // 9. 注册分拣服务
        services.AddSortingServices(configuration);

        // 10. 根据运行模式注册驱动器服务
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

        // 11. 注册并发控制服务
        services.AddConcurrencyControl(configuration);
        services.DecorateWithConcurrencyControl();

        // 12. 注册节点健康服务
        services.AddNodeHealthServices();

        // 13. 注册传感器服务
        services.AddSensorServices(configuration);

        // 14. 注册 RuleEngine 通信服务
        services.AddRuleEngineCommunication(configuration);
        services.AddUpstreamConnectionManagement(configuration);

        // 15. 注册改口功能服务
        services.AddSingleton<IRoutePlanRepository, InMemoryRoutePlanRepository>();
        services.AddSingleton<IRouteReplanner, RouteReplanner>();

        // 16. 注册中段皮带 IO 联动服务
        services.AddMiddleConveyorServices(configuration);

        // 17. 注册仿真服务
        services.AddSimulationServices(configuration);

        return services;
    }

    #region Private Helper Methods - Configuration Repositories

    /// <summary>
    /// 注册所有配置仓储服务（路由、系统、驱动器、传感器、通信）
    /// </summary>
    private static IServiceCollection AddConfigurationRepositories(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 配置路由数据库路径
        var databasePath = configuration["RouteConfiguration:DatabasePath"] ?? "Data/routes.db";
        var fullDatabasePath = Path.Combine(AppContext.BaseDirectory, databasePath);

        // 确保数据目录存在
        var dataDirectory = Path.GetDirectoryName(fullDatabasePath);
        if (!string.IsNullOrEmpty(dataDirectory) && !Directory.Exists(dataDirectory))
        {
            Directory.CreateDirectory(dataDirectory);
        }

        // 注册路由配置仓储为单例
        services.AddSingleton<IRouteConfigurationRepository>(serviceProvider =>
        {
            var repository = new LiteDbRouteConfigurationRepository(fullDatabasePath);
            repository.InitializeDefaultData();
            return repository;
        });

        // 注册系统配置仓储为单例
        services.AddSingleton<ISystemConfigurationRepository>(serviceProvider =>
        {
            var clock = serviceProvider.GetRequiredService<ISystemClock>();
            var repository = new LiteDbSystemConfigurationRepository(fullDatabasePath, clock);
            repository.InitializeDefault(clock.LocalNow);
            return repository;
        });

        // 注册驱动器配置仓储为单例
        services.AddSingleton<IDriverConfigurationRepository>(serviceProvider =>
        {
            var repository = new LiteDbDriverConfigurationRepository(fullDatabasePath);
            repository.InitializeDefault();
            return repository;
        });

        // 注册摆轮配置仓储为单例
        services.AddSingleton<IWheelDiverterConfigurationRepository>(serviceProvider =>
        {
            var repository = new LiteDbWheelDiverterConfigurationRepository(fullDatabasePath);
            repository.InitializeDefault();
            return repository;
        });

        // 注册传感器配置仓储为单例
        services.AddSingleton<ISensorConfigurationRepository>(serviceProvider =>
        {
            var repository = new LiteDbSensorConfigurationRepository(fullDatabasePath);
            repository.InitializeDefault();
            return repository;
        });

        // 注册通信配置仓储为单例
        services.AddSingleton<ICommunicationConfigurationRepository>(serviceProvider =>
        {
            var repository = new LiteDbCommunicationConfigurationRepository(fullDatabasePath);
            repository.InitializeDefault();
            return repository;
        });

        // 注册面板配置仓储为单例
        services.AddSingleton<IPanelConfigurationRepository>(serviceProvider =>
        {
            var clock = serviceProvider.GetRequiredService<ISystemClock>();
            var repository = new LiteDbPanelConfigurationRepository(fullDatabasePath);
            repository.InitializeDefault(clock.LocalNow);
            return repository;
        });

        // 注册 IO 联动配置仓储为单例
        services.AddSingleton<IIoLinkageConfigurationRepository>(serviceProvider =>
        {
            var clock = serviceProvider.GetRequiredService<ISystemClock>();
            var repository = new LiteDbIoLinkageConfigurationRepository(fullDatabasePath);
            repository.InitializeDefault(clock.LocalNow);
            return repository;
        });

        // 注册摆轮硬件绑定配置仓储为单例
        services.AddSingleton<IWheelBindingsRepository>(serviceProvider =>
        {
            var clock = serviceProvider.GetRequiredService<ISystemClock>();
            var repository = new LiteDbWheelBindingsRepository(fullDatabasePath, clock);
            repository.InitializeDefault(clock.LocalNow);
            return repository;
        });

        // 注册日志配置仓储为单例
        services.AddSingleton<ILoggingConfigurationRepository>(serviceProvider =>
        {
            var clock = serviceProvider.GetRequiredService<ISystemClock>();
            var repository = new LiteDbLoggingConfigurationRepository(fullDatabasePath, clock);
            repository.InitializeDefault(clock.LocalNow);
            return repository;
        });

        // 注册格口路径拓扑配置仓储为单例
        services.AddSingleton<IChutePathTopologyRepository>(serviceProvider =>
        {
            var clock = serviceProvider.GetRequiredService<ISystemClock>();
            var repository = new LiteDbChutePathTopologyRepository(fullDatabasePath, clock);
            repository.InitializeDefault(clock.LocalNow);
            return repository;
        });

        return services;
    }

    #endregion

    #region Private Helper Methods - Sorting Services

    /// <summary>
    /// 注册分拣相关服务
    /// </summary>
    private static IServiceCollection AddSortingServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 注册摆轮分拣相关服务
        // 首先注册基础路径生成器
        services.AddSingleton<DefaultSwitchingPathGenerator>();

        // 使用装饰器模式添加缓存功能（可选，通过配置启用）
        var enablePathCaching = configuration.GetValue<bool>("Performance:EnablePathCaching", true);
        if (enablePathCaching)
        {
            services.AddSingleton<ISwitchingPathGenerator>(serviceProvider =>
            {
                var innerGenerator = serviceProvider.GetRequiredService<DefaultSwitchingPathGenerator>();
                var cache = serviceProvider.GetRequiredService<IMemoryCache>();
                var clock = serviceProvider.GetRequiredService<ISystemClock>();
                var logger = serviceProvider.GetRequiredService<ILogger<CachedSwitchingPathGenerator>>();
                return new CachedSwitchingPathGenerator(innerGenerator, cache, clock, logger);
            });
        }
        else
        {
            services.AddSingleton<ISwitchingPathGenerator>(serviceProvider =>
                serviceProvider.GetRequiredService<DefaultSwitchingPathGenerator>());
        }

        // 注册优化的分拣服务
        services.AddSingleton<OptimizedSortingService>();

        // 注册包裹检测服务
        services.AddSingleton<IParcelDetectionService, ParcelDetectionService>();

        // 注册适配器：将 Ingress 层的具体实现适配为 Execution 层抽象
        // PR-U1: IUpstreamRoutingClient 现在由 AddRuleEngineCommunication 直接注册，不再需要 Adapter
        services.AddSingleton<ISensorEventProvider, SensorEventProviderAdapter>();
        services.AddSingleton<ICongestionDataCollector, CongestionDataCollector>();

        // 注册 UpstreamConnectionOptions（从配置绑定）
        services.Configure<UpstreamConnectionOptions>(configuration.GetSection(UpstreamConnectionOptions.SectionName));

        // 注册分拣异常处理器
        services.AddSingleton<ISortingExceptionHandler, SortingExceptionHandler>();

        // 注册分拣编排服务
        services.AddSingleton<ISortingOrchestrator, SortingOrchestrator>();

        // 注册路由-拓扑一致性检查器
        services.AddSingleton<IRouteTopologyConsistencyChecker, RouteTopologyConsistencyChecker>();

        return services;
    }

    #endregion

    #region Private Helper Methods - Runtime Profile

    /// <summary>
    /// 添加运行时配置文件服务
    /// </summary>
    private static IServiceCollection AddRuntimeProfile(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 读取运行模式配置
        var modeString = configuration.GetValue<string>("Runtime:Mode");
        var mode = ParseRuntimeMode(modeString);

        // 根据模式注册对应的 IRuntimeProfile 实现
        services.AddSingleton<IRuntimeProfile>(sp => CreateRuntimeProfile(mode));

        return services;
    }

    private static RuntimeMode ParseRuntimeMode(string? modeString)
    {
        if (string.IsNullOrWhiteSpace(modeString))
        {
            return RuntimeMode.Production;
        }

        // 首先检查是否为数字字符串，如果是则拒绝并返回默认值
        if (int.TryParse(modeString.Trim(), out _))
        {
            return RuntimeMode.Production;
        }

        var normalizedInput = modeString.Trim()
            .Replace("_", "")
            .Replace("-", "")
            .ToLowerInvariant();

        return normalizedInput switch
        {
            "production" => RuntimeMode.Production,
            "simulation" => RuntimeMode.Simulation,
            "performancetest" => RuntimeMode.PerformanceTest,
            _ when Enum.TryParse<RuntimeMode>(modeString, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed) => parsed,
            _ => RuntimeMode.Production
        };
    }

    private static IRuntimeProfile CreateRuntimeProfile(RuntimeMode mode)
    {
        return mode switch
        {
            RuntimeMode.Production => new ProductionRuntimeProfile(),
            RuntimeMode.Simulation => new SimulationRuntimeProfile(),
            RuntimeMode.PerformanceTest => new PerformanceTestRuntimeProfile(),
            _ => new ProductionRuntimeProfile()
        };
    }

    #endregion

    #region Private Helper Methods - Middle Conveyor Services

    /// <summary>
    /// 注册中段皮带 IO 联动相关服务
    /// </summary>
    private static IServiceCollection AddMiddleConveyorServices(
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

        var factory = serviceProvider.GetService<IVendorDriverFactory>();

        foreach (var mapping in mappings)
        {
            IConveyorSegmentDriver? driver = null;

            if (factory != null)
            {
                driver = factory.CreateConveyorSegmentDriver(mapping.SegmentKey);
            }

            if (driver == null)
            {
                if (isSimulation)
                {
                    driver = new SimulatedConveyorSegmentDriver(
                        mapping,
                        serviceProvider.GetRequiredService<ILogger<SimulatedConveyorSegmentDriver>>());
                }
                else
                {
                    var emcController = serviceProvider.GetRequiredService<IEmcController>();
                    driver = new LeadshineConveyorSegmentDriver(
                        mapping,
                        emcController,
                        serviceProvider.GetRequiredService<ILogger<LeadshineConveyorSegmentDriver>>());
                }
            }

            var segment = new ConveyorSegment(
                driver,
                serviceProvider.GetRequiredService<ILogger<ConveyorSegment>>(),
                serviceProvider.GetRequiredService<ISystemClock>());

            segments.Add(segment);
        }

        return segments;
    }

    #endregion

    #region Private Helper Methods - Simulation Services

    /// <summary>
    /// 注册仿真服务（用于 API 触发仿真）
    /// </summary>
    private static IServiceCollection AddSimulationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var enableApiSimulation = configuration.GetValue<bool>("Simulation:EnableApiSimulation", false);
        
        if (!enableApiSimulation)
        {
            return services;
        }

        // 注册仿真服务
        services.AddSingleton<Simulation.Services.SimulationRunner>();
        services.AddSingleton<Simulation.Services.SimulationScenarioRunner>();
        services.AddSingleton<Simulation.Services.ParcelTimelineFactory>();
        services.AddSingleton<Simulation.Services.SimulationReportPrinter>();
        
        // 注册 ISimulationScenarioRunner 接口
        services.AddSingleton<Simulation.Services.ISimulationScenarioRunner>(
            serviceProvider => serviceProvider.GetRequiredService<Simulation.Services.SimulationScenarioRunner>());

        return services;
    }

    #endregion

    #region Runtime Profile Implementations

    /// <summary>
    /// 生产环境运行时配置文件
    /// </summary>
    private sealed class ProductionRuntimeProfile : IRuntimeProfile
    {
        public RuntimeMode Mode => RuntimeMode.Production;
        public bool UseHardwareDriver => true;
        public bool IsSimulationMode => false;
        public bool IsPerformanceTestMode => false;
        public bool EnableIoOperations => true;
        public bool EnableUpstreamCommunication => true;
        public bool EnableHealthCheckTasks => true;
        public bool EnablePerformanceMonitoring => true;
        public string GetModeDescription() => "生产模式 - 使用真实硬件驱动，连接实际上游系统";
    }

    /// <summary>
    /// 仿真模式运行时配置文件
    /// </summary>
    private sealed class SimulationRuntimeProfile : IRuntimeProfile
    {
        public RuntimeMode Mode => RuntimeMode.Simulation;
        public bool UseHardwareDriver => false;
        public bool IsSimulationMode => true;
        public bool IsPerformanceTestMode => false;
        public bool EnableIoOperations => true;
        public bool EnableUpstreamCommunication => true;
        public bool EnableHealthCheckTasks => true;
        public bool EnablePerformanceMonitoring => true;
        public string GetModeDescription() => "仿真模式 - 使用模拟驱动器，虚拟传感器和条码源";
    }

    /// <summary>
    /// 性能测试模式运行时配置文件
    /// </summary>
    private sealed class PerformanceTestRuntimeProfile : IRuntimeProfile
    {
        public RuntimeMode Mode => RuntimeMode.PerformanceTest;
        public bool UseHardwareDriver => false;
        public bool IsSimulationMode => false;
        public bool IsPerformanceTestMode => true;
        public bool EnableIoOperations => false;
        public bool EnableUpstreamCommunication => false;
        public bool EnableHealthCheckTasks => false;
        public bool EnablePerformanceMonitoring => true;
        public string GetModeDescription() => "性能测试模式 - 跳过实际 IO，专注于路径/算法性能测试";
    }

    #endregion
}
