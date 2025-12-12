using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZakYip.WheelDiverterSorter.Communication;
using ZakYip.WheelDiverterSorter.Core.Hardware.Devices;
using ZakYip.WheelDiverterSorter.Core.Hardware.IoLinkage;
using ZakYip.WheelDiverterSorter.Core.Hardware.Mappings;
using ZakYip.WheelDiverterSorter.Core.Hardware.Ports;
using ZakYip.WheelDiverterSorter.Core.Hardware.Providers;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Execution;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Ingress;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Upstream;
using ZakYip.WheelDiverterSorter.Core.Enums.System;
using ZakYip.WheelDiverterSorter.Core.LineModel;
using ZakYip.WheelDiverterSorter.Core.LineModel.Bindings;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;
using ZakYip.WheelDiverterSorter.Configuration.Persistence.Repositories.LiteDb;
using ZakYip.WheelDiverterSorter.Core.LineModel.Orchestration;
using ZakYip.WheelDiverterSorter.Core.LineModel.Runtime;
using ZakYip.WheelDiverterSorter.Core.LineModel.Routing;
using ZakYip.WheelDiverterSorter.Core.LineModel.Topology;
using ZakYip.WheelDiverterSorter.Core.LineModel.Tracing;
using ZakYip.WheelDiverterSorter.Core.Sorting;
using ZakYip.WheelDiverterSorter.Core.Sorting.Interfaces;
using ZakYip.WheelDiverterSorter.Core.Sorting.Orchestration;
using ZakYip.WheelDiverterSorter.Core.Sorting.Overload;
using ZakYip.WheelDiverterSorter.Core.Sorting.Policies;
using ZakYip.WheelDiverterSorter.Core.Sorting.Strategy;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Drivers;
using ZakYip.WheelDiverterSorter.Drivers.Vendors.Leadshine;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware.Vendors;
using ZakYip.WheelDiverterSorter.Drivers.Vendors.ShuDiNiao;
using ZakYip.WheelDiverterSorter.Drivers.Vendors.Siemens;
using ZakYip.WheelDiverterSorter.Drivers.Vendors.Simulated;
using ZakYip.WheelDiverterSorter.Execution;
using ZakYip.WheelDiverterSorter.Execution.Concurrency;
using ZakYip.WheelDiverterSorter.Execution.Extensions;
using ZakYip.WheelDiverterSorter.Execution.Health;
using ZakYip.WheelDiverterSorter.Execution.Infrastructure;
using ZakYip.WheelDiverterSorter.Execution.Orchestration;
using ZakYip.WheelDiverterSorter.Execution.PathExecution;
using ZakYip.WheelDiverterSorter.Execution.Queues;
using ZakYip.WheelDiverterSorter.Execution.Routing;
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
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Drivers.Vendors.Siemens.Configuration;

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
        services.AddNetworkConnectivityChecker();  // 添加网络连通性检查器

        // 6. 添加包裹生命周期日志和追踪服务
        services.AddParcelLifecycleLogger();
        services.AddParcelTraceLogging();
        
        // 配置日志清理选项
        services.Configure<Observability.Tracing.LogCleanupOptions>(
            configuration.GetSection(Observability.Tracing.LogCleanupOptions.SectionName));
        services.AddLogCleanup();

        // 7. 添加配置审计日志服务
        services.AddConfigurationAuditLogger();

        // 8. 注册所有配置仓储
        services.AddConfigurationRepositories(configuration);

        // 9. 注册应用层服务（调用 ApplicationServiceExtensions）
        services.AddWheelDiverterApplication();

        // 10. 注册分拣服务
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
            services.AddSimulatedIo();
        }
        else
        {
            // 生产模式：根据数据库配置动态注册驱动器
            // 注意：由于需要访问数据库才能确定厂商类型，这里使用工厂模式延迟解析
            services.AddProductionModeDrivers(configuration);
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

        // 15. 注册改口功能服务（使用LiteDB持久化）
        services.AddSingleton<IRoutePlanRepository>(sp =>
        {
            var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "route_plans.db");
            var directory = Path.GetDirectoryName(dbPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            return new LiteDbRoutePlanRepository(dbPath);
        });
        services.AddSingleton<IRouteReplanner, RouteReplanner>();

        // 16. 注册中段皮带 IO 联动服务
        // Middle conveyor services removed - functionality replaced by ConveyorSegmentConfiguration

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

        // WheelHardwareBinding removed - wheel binding now handled via topology + vendor config association

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

        // 注册输送线段配置仓储为单例
        services.AddSingleton<IConveyorSegmentRepository>(serviceProvider =>
        {
            var clock = serviceProvider.GetRequiredService<ISystemClock>();
            var repository = new LiteDbConveyorSegmentRepository(fullDatabasePath, clock);
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
        // 显式指定构造函数，确保同时注入 RouteRepository 和 TopologyRepository
        // TopologyRepository 有更高优先级（支持 N 摆轮模型）
        services.AddSingleton<DefaultSwitchingPathGenerator>(sp =>
        {
            var routeRepo = sp.GetRequiredService<IRouteConfigurationRepository>();
            var topologyRepo = sp.GetRequiredService<IChutePathTopologyRepository>();
            var clock = sp.GetRequiredService<ISystemClock>();
            var conveyorSegmentRepo = sp.GetService<IConveyorSegmentRepository>(); // 可选，用于动态TTL计算
            var logger = sp.GetService<ILogger<DefaultSwitchingPathGenerator>>();
            return new DefaultSwitchingPathGenerator(routeRepo, topologyRepo, clock, conveyorSegmentRepo, logger);
        });

        // 使用装饰器模式添加缓存功能（可选，通过配置启用）
        var enablePathCaching = configuration.GetValue<bool>("Performance:EnablePathCaching", true);
        if (enablePathCaching)
        {
            // 注册带缓存的路径生成器
            services.AddSingleton<CachedSwitchingPathGenerator>(serviceProvider =>
            {
                var innerGenerator = serviceProvider.GetRequiredService<DefaultSwitchingPathGenerator>();
                var configCache = serviceProvider.GetRequiredService<ISlidingConfigCache>();
                var clock = serviceProvider.GetRequiredService<ISystemClock>();
                var logger = serviceProvider.GetRequiredService<ILogger<CachedSwitchingPathGenerator>>();
                return new CachedSwitchingPathGenerator(innerGenerator, configCache, clock, logger);
            });

            // 将 CachedSwitchingPathGenerator 注册为 ISwitchingPathGenerator
            services.AddSingleton<ISwitchingPathGenerator>(sp => sp.GetRequiredService<CachedSwitchingPathGenerator>());

            // 将 CachedSwitchingPathGenerator 注册为 IPathCacheManager（用于拓扑变更时清除缓存）
            services.AddSingleton<IPathCacheManager>(sp => sp.GetRequiredService<CachedSwitchingPathGenerator>());
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

        // 注册系统运行状态服务（必须，用于状态验证）
        // Note: ISystemStateManager 已在 Host 层注册，不在此处注册

        // 注册分拣异常处理器
        services.AddSingleton<ISortingExceptionHandler, SortingExceptionHandler>();

        // 注册 Position-Index 队列管理器（新的队列系统）
        services.AddSingleton<IPositionIndexQueueManager, PositionIndexQueueManager>();

        // 注册格口路径拓扑服务
        services.AddSingleton<IChutePathTopologyService, ChutePathTopologyService>();

        // 注册输送线段配置服务
        services.AddSingleton<IConveyorSegmentService, ConveyorSegmentService>();

        // 注册分拣编排服务（支持Client和Server模式通知）
        services.AddSingleton<ISortingOrchestrator>(sp =>
        {
            var sensorEventProvider = sp.GetRequiredService<ISensorEventProvider>();
            var upstreamClient = sp.GetRequiredService<IUpstreamRoutingClient>();
            var pathGenerator = sp.GetRequiredService<ISwitchingPathGenerator>();
            var pathExecutor = sp.GetRequiredService<ISwitchingPathExecutor>();
            var options = sp.GetRequiredService<IOptions<UpstreamConnectionOptions>>();
            var systemConfigRepository = sp.GetRequiredService<ISystemConfigurationRepository>();
            var clock = sp.GetRequiredService<ISystemClock>();
            var logger = sp.GetRequiredService<ILogger<SortingOrchestrator>>();
            var exceptionHandler = sp.GetRequiredService<ISortingExceptionHandler>();
            var systemStateManager = sp.GetRequiredService<ISystemStateManager>(); // 必需
            
            // 可选依赖
            var pathFailureHandler = sp.GetService<IPathFailureHandler>();
            var congestionDetector = sp.GetService<ICongestionDetector>();
            var congestionCollector = sp.GetService<ICongestionDataCollector>();
            var metrics = sp.GetService<PrometheusMetrics>();
            var traceSink = sp.GetService<IParcelTraceSink>();
            var pathHealthChecker = sp.GetService<PathHealthChecker>();
            var timeoutCalculator = sp.GetService<IChuteAssignmentTimeoutCalculator>();
            var chuteSelectionService = sp.GetService<IChuteSelectionService>();
            
            // 新的 Position-Index 队列系统依赖
            var queueManager = sp.GetService<IPositionIndexQueueManager>();
            var topologyRepository = sp.GetService<IChutePathTopologyRepository>();
            var segmentRepository = sp.GetService<IConveyorSegmentRepository>();
            var sensorConfigRepository = sp.GetService<ISensorConfigurationRepository>();
            var safeExecutor = sp.GetService<ISafeExecutionService>();
            
            return new SortingOrchestrator(
                sensorEventProvider,
                upstreamClient,
                pathGenerator,
                pathExecutor,
                options,
                systemConfigRepository,
                clock,
                logger,
                exceptionHandler,
                systemStateManager, // 必需参数
                pathFailureHandler,
                congestionDetector,
                congestionCollector,
                metrics,
                traceSink,
                pathHealthChecker,
                timeoutCalculator,
                chuteSelectionService,
                queueManager,
                topologyRepository,
                segmentRepository,
                sensorConfigRepository,
                safeExecutor);
        });

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
    // AddMiddleConveyorServices and CreateConveyorSegments methods removed
    // Middle conveyor functionality replaced by ConveyorSegmentConfiguration

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

    #region Private Helper Methods - Production Mode Drivers

    /// <summary>
    /// 注册生产模式的驱动器服务
    /// </summary>
    /// <remarks>
    /// 根据数据库配置的 DriverVendorType 决定注册哪种 IO 驱动器：
    /// - Leadshine（默认）: 雷赛 IO 驱动器 + 数递鸟摆轮驱动器
    /// - Siemens: 西门子 S7 IO 驱动器 + 数递鸟摆轮驱动器
    /// - Mock: 模拟驱动器
    /// 
    /// 注意：IO 驱动器会在应用启动时立即初始化，确保连接问题能够及早发现。
    /// </remarks>
    private static IServiceCollection AddProductionModeDrivers(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 先注册雷赛服务（包含 EMC 控制器等基础设施）
        // 这些服务即使不使用雷赛 IO 驱动器也可能被其他组件需要
        services.AddLeadshineIo();
        
        // 注册数递鸟摆轮驱动器
        services.AddShuDiNiaoWheelDiverter();
        
        // 注册面板输入读取器（根据配置动态选择）
        // 注意：面板配置在应用启动时读取一次，配置变更需要重启应用才能生效
        // 这包括 UseSimulation 标志和 IO 位映射配置
        services.AddSingleton<IPanelInputReader>(sp =>
        {
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var panelConfigRepo = sp.GetRequiredService<IPanelConfigurationRepository>();
            var systemClock = sp.GetRequiredService<ISystemClock>();
            var fallbackLogger = loggerFactory.CreateLogger("PanelInputReader");
            
            PanelConfiguration? panelConfig = null;
            try
            {
                // 从数据库读取面板配置
                panelConfig = panelConfigRepo.Get();
            }
            catch (Exception ex)
            {
                fallbackLogger.LogError(ex, "读取面板配置时发生异常");
                // 如果无法读取配置，无法判断是否应使用仿真模式，视为严重配置错误
                throw new InvalidOperationException(
                    "无法从数据库读取面板配置，系统无法确定是否应使用仿真或硬件模式。请检查数据库初始化和配置。", ex);
            }

            // 配置缺失时，抛出异常
            if (panelConfig == null)
            {
                throw new InvalidOperationException(
                    "未找到面板配置，无法初始化面板输入读取器。请先完成面板配置初始化。");
            }
                
            // 根据 IRuntimeProfile.IsSimulationMode 选择实现
            var runtimeProfile = sp.GetService<IRuntimeProfile>();
            bool isSimulationMode = runtimeProfile?.IsSimulationMode ?? false;
            
            if (isSimulationMode)
            {
                var logger = loggerFactory.CreateLogger<SimulatedPanelInputReader>();
                logger.LogInformation("使用仿真面板输入读取器（仿真模式）");
                return new SimulatedPanelInputReader(systemClock);
            }
            else
            {
                // 使用雷赛 IO 读取器
                var logger = loggerFactory.CreateLogger<LeadshinePanelInputReader>();
                var inputPort = sp.GetRequiredService<IInputPort>();
                
                logger.LogInformation("使用雷赛硬件面板输入读取器");
                return new LeadshinePanelInputReader(
                    logger,
                    inputPort,
                    panelConfigRepo,
                    systemClock);
            }
        });
        
        // 注册面板 IO 协调器
        services.AddSingleton<IPanelIoCoordinator, DefaultPanelIoCoordinator>();
        
        // 替换 IIoLinkageDriver 注册为动态选择版本
        // 移除已有的 IIoLinkageDriver 注册（由 AddLeadshineIo 注册的）
        var existingDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IIoLinkageDriver));
        if (existingDescriptor != null)
        {
            services.Remove(existingDescriptor);
        }
        
        // 重新注册为动态版本
        // DI容器的Singleton注册已经确保工厂只会被调用一次
        services.AddSingleton<IIoLinkageDriver>(sp =>
        {
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger("DynamicIoLinkageDriver");
            
            try
            {
                // 检查是否为测试环境
                var isTestEnv = configuration.GetValue<bool>("IsTestEnvironment", false);
                if (isTestEnv)
                {
                    logger.LogInformation("检测到测试环境，使用模拟 IO 联动驱动器");
                    return new SimulatedIoLinkageDriver(
                        loggerFactory.CreateLogger<SimulatedIoLinkageDriver>());
                }
                
                var driverRepo = sp.GetRequiredService<IDriverConfigurationRepository>();
                
                // 从数据库读取驱动器配置
                var driverConfig = driverRepo.Get();
                var vendorType = driverConfig.VendorType;
                
                // 使用 IRuntimeProfile.IsSimulationMode 判断是否使用模拟驱动器
                var runtimeProfile = sp.GetService<IRuntimeProfile>();
                bool isSimulationMode = runtimeProfile?.IsSimulationMode ?? false;
                
                logger.LogInformation(
                    "正在根据数据库配置选择 IO 联动驱动器: VendorType={VendorType}, SimulationMode={SimulationMode}",
                    vendorType,
                    isSimulationMode);
                
                // 如果是仿真模式或使用 Mock，返回模拟驱动器
                if (isSimulationMode || vendorType == DriverVendorType.Mock)
                {
                    logger.LogInformation("使用模拟 IO 联动驱动器");
                    return new SimulatedIoLinkageDriver(
                        loggerFactory.CreateLogger<SimulatedIoLinkageDriver>());
                }
                
                // 根据厂商类型选择驱动器
                return vendorType switch
                {
                    DriverVendorType.Leadshine => CreateLeadshineDriver(sp, logger),
                    DriverVendorType.Siemens => CreateS7IoLinkageDriver(sp, configuration, loggerFactory),
                    _ => CreateDefaultDriver(logger, vendorType)
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "创建 IO 联动驱动器失败，回退到模拟驱动器");
                return new SimulatedIoLinkageDriver(
                    loggerFactory.CreateLogger<SimulatedIoLinkageDriver>());
            }
        });
        
        // 注册驱动器初始化服务，在启动时强制解析驱动器
        services.AddHostedService<IoDriverInitializationService>();
        
        return services;
    }
    
    /// <summary>
    /// 创建雷赛IO联动驱动器
    /// </summary>
    private static IIoLinkageDriver CreateLeadshineDriver(IServiceProvider sp, ILogger logger)
    {
        logger.LogInformation("使用雷赛 IO 联动驱动器");
        // 从工厂获取已初始化的 IO 联动驱动器
        // AddLeadshineIo() 已经创建并初始化了 EMC 控制器
        var factory = sp.GetRequiredService<IVendorDriverFactory>();
        return factory.CreateIoLinkageDriver();
    }
    
    /// <summary>
    /// 创建默认驱动器（当厂商类型未知时）
    /// </summary>
    private static IIoLinkageDriver CreateDefaultDriver(ILogger logger, DriverVendorType vendorType)
    {
        logger.LogWarning(
            "未知的驱动器厂商类型: {VendorType}，回退到雷赛驱动器",
            vendorType);
        throw new NotSupportedException($"不支持的驱动器厂商类型: {vendorType}");
    }
    
    /// <summary>
    /// 创建西门子S7 IO联动驱动器
    /// </summary>
    private static IIoLinkageDriver CreateS7IoLinkageDriver(
        IServiceProvider sp,
        IConfiguration configuration,
        ILoggerFactory loggerFactory)
    {
        var s7Config = configuration.GetSection("S7").Get<S7Options>();
        
        if (s7Config == null)
        {
            throw new InvalidOperationException("S7 配置未找到，请在 appsettings.json 中配置 S7 节点");
        }
        
        // 获取必需的依赖服务
        var clock = sp.GetRequiredService<ISystemClock>();
        var safeExecutor = sp.GetRequiredService<ISafeExecutionService>();
        
        // 创建 S7 连接
        var s7Connection = new S7Connection(
            loggerFactory.CreateLogger<S7Connection>(),
            new SimpleOptionsMonitor<S7Options>(s7Config),
            clock,
            safeExecutor);
        
        // 创建 S7 IO 联动驱动器
        return new S7IoLinkageDriver(
            s7Connection,
            loggerFactory.CreateLogger<S7IoLinkageDriver>(),
            loggerFactory);
    }
    
    /// <summary>
    /// S7默认配置常量
    /// </summary>
    private static class S7DefaultConfiguration
    {
        public const string DefaultIpAddress = "192.168.0.1";
        public const int DefaultRack = 0;
        public const int DefaultSlot = 1;
    }
    
    #endregion
    
}

/// <summary>
/// 简单的 IOptionsMonitor 实现，用于不需要热更新的场景
/// </summary>
file sealed class SimpleOptionsMonitor<T> : IOptionsMonitor<T>
{
    private readonly T _currentValue;
    
    public SimpleOptionsMonitor(T currentValue)
    {
        _currentValue = currentValue;
    }
    
    public T CurrentValue => _currentValue;
    
    public T Get(string? name) => _currentValue;
    
    public IDisposable? OnChange(Action<T, string?> listener) => null;
}

/// <summary>
/// IO 驱动器初始化服务 - 在应用启动时强制解析和初始化 IO 驱动器
/// </summary>
file sealed class IoDriverInitializationService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<IoDriverInitializationService> _logger;
    
    public IoDriverInitializationService(
        IServiceProvider serviceProvider,
        ILogger<IoDriverInitializationService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }
    
    public Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("正在初始化 IO 联动驱动器...");
            
            // 强制解析 IIoLinkageDriver 服务，触发初始化
            var driver = _serviceProvider.GetRequiredService<IIoLinkageDriver>();
            
            _logger.LogInformation(
                "IO 联动驱动器初始化成功: {DriverType}",
                driver.GetType().Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "IO 联动驱动器初始化失败");
            // 不抛出异常，让应用继续启动，但记录错误
            // 这样管理员可以看到问题并采取行动
        }
        
        return Task.CompletedTask;
    }
    
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("IO 驱动器初始化服务停止");
        return Task.CompletedTask;
    }
}
