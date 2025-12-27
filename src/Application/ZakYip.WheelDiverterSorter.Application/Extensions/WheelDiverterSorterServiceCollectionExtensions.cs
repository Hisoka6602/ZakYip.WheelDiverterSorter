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
using ZakYip.WheelDiverterSorter.Execution;
using ZakYip.WheelDiverterSorter.Execution.Extensions;
using ZakYip.WheelDiverterSorter.Execution.Health;
using ZakYip.WheelDiverterSorter.Execution.Orchestration;
using ZakYip.WheelDiverterSorter.Execution.PathExecution;
using ZakYip.WheelDiverterSorter.Execution.Queues;
using ZakYip.WheelDiverterSorter.Execution.Routing;
using ZakYip.WheelDiverterSorter.Ingress;
using ZakYip.WheelDiverterSorter.Ingress.Services;
using ZakYip.WheelDiverterSorter.Observability;
using ZakYip.WheelDiverterSorter.Observability.Utilities;
using ZakYip.WheelDiverterSorter.Application.Services;
using ZakYip.WheelDiverterSorter.Application.Services.Config;
using ZakYip.WheelDiverterSorter.Application.Services.Caching;
using ZakYip.WheelDiverterSorter.Application.Services.Sorting;
using ZakYip.WheelDiverterSorter.Application.Services.Metrics;
using ZakYip.WheelDiverterSorter.Application.Services.Topology;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Drivers.Vendors.Siemens.Configuration;

namespace ZakYip.WheelDiverterSorter.Application.Extensions;

/// <summary>
/// WheelDiverterSorter 服务集合扩展方法
/// Unified DI entry point for all WheelDiverterSorter services
/// </summary>
/// <remarks>
/// 提供统一的服务注册入口，将所有 Core/Execution/Drivers/Ingress/Communication/Observability 
/// 相关服务注册合并到 Application 层，使 Host 层只需调用这一个方法即可完成所有服务注册。
/// 
/// **依赖关系**：
/// Host → Application → (Core/Execution/Drivers/Ingress/Communication/Observability)
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
    /// 5. 告警服务
    /// 6. 包裹追踪和日志清理服务
    /// 7. 配置仓储服务
    /// 8. 应用层服务
    /// 9. 分拣服务
    /// 10. 驱动器服务（根据运行模式选择硬件）
    /// 11. 节点健康服务
    /// 12. 传感器服务
    /// 13. RuleEngine 通信服务
    /// 14. 改口功能服务
    /// 15. 中段皮带 IO 联动服务
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

        // 5. 添加告警服务
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

        // 生产模式和性能测试模式：根据数据库配置动态注册驱动器
        // 注意：由于需要访问数据库才能确定厂商类型，这里使用工厂模式延迟解析
        {
            services.AddProductionModeDrivers(configuration);
        }

        // 11. 注册节点健康服务
        services.AddNodeHealthServices();

        // 12. 注册传感器服务
        services.AddSensorServices(configuration);

        // 13. 注册 RuleEngine 通信服务
        services.AddRuleEngineCommunication(configuration);
        services.AddUpstreamConnectionManagement(configuration);

        // 14. 注册改口功能服务（使用内存缓存，3分钟滑动过期）
        services.AddSingleton<IRoutePlanRepository, InMemoryRoutePlanRepository>();
        services.AddSingleton<IRouteReplanner, RouteReplanner>();

        // 15. 注册中段皮带 IO 联动服务
        // Middle conveyor services removed - functionality replaced by ConveyorSegmentConfiguration

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
            var logger = serviceProvider.GetRequiredService<ILogger<LiteDbConveyorSegmentRepository>>();
            var repository = new LiteDbConveyorSegmentRepository(fullDatabasePath, clock, logger);
            return repository;
        });

        // 注册落格回调配置仓储为单例
        services.AddSingleton<IChuteDropoffCallbackConfigurationRepository>(serviceProvider =>
        {
            var clock = serviceProvider.GetRequiredService<ISystemClock>();
            var repository = new LiteDbChuteDropoffCallbackConfigurationRepository(fullDatabasePath, clock);
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

        // 移除适配器：Execution 层直接依赖 Ingress 层的 IParcelDetectionService
        // PR-PHASE1: 删除纯转发适配器 SensorEventProviderAdapter
        services.AddSingleton<ICongestionDataCollector, CongestionDataCollector>();

        // 注册 UpstreamConnectionOptions（从配置绑定）
        services.Configure<UpstreamConnectionOptions>(configuration.GetSection(UpstreamConnectionOptions.SectionName));

        // 注册系统运行状态服务（必须，用于状态验证）
        // Note: ISystemStateManager 已在 Host 层注册，不在此处注册

        // 注册分拣异常处理器
        services.AddSingleton<ISortingExceptionHandler, SortingExceptionHandler>();

        // 注册 Position-Index 队列管理器（新的队列系统）
        services.AddSingleton<IPositionIndexQueueManager, PositionIndexQueueManager>();
        
        // Position 间隔追踪器（单例）
        services.Configure<Execution.Tracking.PositionIntervalTrackerOptions>(options =>
        {
            // 使用默认配置，也可以从 appsettings.json 读取
            options.WindowSize = 10;
            options.TimeoutMultiplier = 3.0;
            options.LostDetectionMultiplier = 1.5;
            options.MinSamplesForThreshold = 3;
            options.MaxReasonableIntervalMs = 60000;
            options.MonitoringIntervalMs = 60;
            // ParcelRecordCleanupThreshold 和 ParcelRecordRetentionCount 已移除
            // 自动清理已禁用，包裹记录仅在分拣完成时通过 ClearParcelTracking() 显式清理
        });
        services.AddSingleton<Execution.Tracking.IPositionIntervalTracker, Execution.Tracking.PositionIntervalTracker>();

        // 注册格口路径拓扑服务
        services.AddSingleton<IChutePathTopologyService, ChutePathTopologyService>();

        // 注册输送线段配置服务
        services.AddSingleton<IConveyorSegmentService, ConveyorSegmentService>();

        // 注册分拣编排服务（支持Client和Server模式通知）
        services.AddSingleton<ISortingOrchestrator>(sp =>
        {
            var parcelDetectionService = sp.GetRequiredService<IParcelDetectionService>();
            var upstreamClient = sp.GetRequiredService<IUpstreamRoutingClient>();
            var pathGenerator = sp.GetRequiredService<ISwitchingPathGenerator>();
            var pathExecutor = sp.GetRequiredService<ISwitchingPathExecutor>();
            var options = sp.GetRequiredService<IOptions<UpstreamConnectionOptions>>();
            var systemConfigService = sp.GetRequiredService<ISystemConfigService>();
            var clock = sp.GetRequiredService<ISystemClock>();
            var logger = sp.GetRequiredService<ILogger<SortingOrchestrator>>();
            var exceptionHandler = sp.GetRequiredService<ISortingExceptionHandler>();
            var systemStateManager = sp.GetRequiredService<ISystemStateManager>(); // 必需
            
            // 可选依赖
            var pathFailureHandler = sp.GetService<IPathFailureHandler>();
            var congestionDetector = sp.GetService<ICongestionDetector>();
            var congestionCollector = sp.GetService<ICongestionDataCollector>();
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
            var intervalTracker = sp.GetService<Execution.Tracking.IPositionIntervalTracker>();
            var callbackConfigRepository = sp.GetService<IChuteDropoffCallbackConfigurationRepository>();
            var alarmService = sp.GetService<AlarmService>();
            var statisticsService = sp.GetService<ISortingStatisticsService>();
            var routePlanRepository = sp.GetService<IRoutePlanRepository>();
            var upstreamServer = sp.GetService<Communication.Abstractions.IRuleEngineServer>(); // 服务端模式（可选）
            
            return new SortingOrchestrator(
                parcelDetectionService,
                upstreamClient,
                pathGenerator,
                pathExecutor,
                options,
                systemConfigService,
                clock,
                logger,
                exceptionHandler,
                systemStateManager, // 必需参数
                pathFailureHandler,
                congestionDetector,
                congestionCollector,
                traceSink,
                pathHealthChecker,
                timeoutCalculator,
                chuteSelectionService,
                queueManager,
                topologyRepository,
                segmentRepository,
                sensorConfigRepository,
                safeExecutor,
                intervalTracker,
                callbackConfigRepository,
                alarmService,
                statisticsService,
                routePlanRepository,
                upstreamServer);
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

    #region Runtime Profile Implementations

    /// <summary>
    /// 生产环境运行时配置文件
    /// </summary>
    private sealed class ProductionRuntimeProfile : IRuntimeProfile
    {
        public RuntimeMode Mode => RuntimeMode.Production;
        public bool IsPerformanceTestMode => false;
        public bool EnableIoOperations => true;
        public bool EnableUpstreamCommunication => true;
        public bool EnableHealthCheckTasks => true;
        public bool EnablePerformanceMonitoring => true;
        public string GetModeDescription() => "生产模式 - 使用真实硬件驱动，连接实际上游系统";
    }

    /// <summary>
    /// 性能测试模式运行时配置文件
    /// </summary>
    private sealed class PerformanceTestRuntimeProfile : IRuntimeProfile
    {
        public RuntimeMode Mode => RuntimeMode.PerformanceTest;
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
        
        // 注册面板输入读取器（使用硬件驱动）
        // 注意：面板配置在应用启动时读取一次，配置变更需要重启应用才能生效
        // 这包括 IO 位映射配置
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
                // 如果无法读取配置，视为严重配置错误
                throw new InvalidOperationException(
                    "无法从数据库读取面板配置，请检查数据库初始化和配置。", ex);
            }

            // 配置缺失时，抛出异常
            if (panelConfig == null)
            {
                throw new InvalidOperationException(
                    "未找到面板配置，无法初始化面板输入读取器。请先完成面板配置初始化。");
            }
                
            // 使用雷赛 IO 读取器 (removed simulation mode support)
            var logger = loggerFactory.CreateLogger<LeadshinePanelInputReader>();
            var inputPort = sp.GetRequiredService<IInputPort>();
            
            logger.LogInformation("使用雷赛硬件面板输入读取器");
            return new LeadshinePanelInputReader(
                logger,
                inputPort,
                panelConfigRepo,
                systemClock);
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
        
        // 重新注册为动态版本（使用装饰器模式支持延迟执行）
        // DI容器的Singleton注册已经确保工厂只会被调用一次
        services.AddSingleton<IIoLinkageDriver>(sp =>
        {
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger("DynamicIoLinkageDriver");
            
            IIoLinkageDriver innerDriver;
            
            try
            {
                // 检查是否为测试环境
                var isTestEnv = configuration.GetValue<bool>("IsTestEnvironment", false);
                if (isTestEnv)
                {
                    logger.LogInformation("检测到测试环境，使用 Leadshine 驱动器");
                    innerDriver = CreateLeadshineDriver(sp, logger);
                }
                else
                {
                    var driverRepo = sp.GetRequiredService<IDriverConfigurationRepository>();
                    
                    // 从数据库读取驱动器配置
                    var driverConfig = driverRepo.Get();
                    var vendorType = driverConfig.VendorType;
                    
                    logger.LogInformation(
                        "正在根据数据库配置选择 IO 联动驱动器: VendorType={VendorType}",
                        vendorType);
                    
                    // 根据厂商类型选择驱动器
                    innerDriver = vendorType switch
                    {
                        DriverVendorType.Leadshine => CreateLeadshineDriver(sp, logger),
                        DriverVendorType.Siemens => CreateS7IoLinkageDriver(sp, configuration, loggerFactory),
                        _ => CreateDefaultDriver(logger, vendorType)
                    };
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "创建 IO 联动驱动器失败，回退到 Leadshine 驱动器");
                innerDriver = CreateLeadshineDriver(sp, logger);
            }
            
            // 使用装饰器包装驱动器以支持延迟执行和状态检查
            var systemStateManager = sp.GetRequiredService<ISystemStateManager>();
            var safeExecutor = sp.GetRequiredService<ISafeExecutionService>();
            var decoratorLogger = loggerFactory.CreateLogger<DelayedIoLinkageDriverDecorator>();
            return new DelayedIoLinkageDriverDecorator(innerDriver, systemStateManager, safeExecutor, decoratorLogger);
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
        // 直接创建驱动器实例，不使用已废弃的工厂方法
        // AddLeadshineIo() 已经注册了 EMC 控制器和 IInputPort
        var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
        var driverLogger = loggerFactory.CreateLogger<LeadshineIoLinkageDriver>();
        var emcController = sp.GetRequiredService<IEmcController>();
        var inputPort = sp.GetRequiredService<IInputPort>();
        return new LeadshineIoLinkageDriver(driverLogger, emcController, inputPort);
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
