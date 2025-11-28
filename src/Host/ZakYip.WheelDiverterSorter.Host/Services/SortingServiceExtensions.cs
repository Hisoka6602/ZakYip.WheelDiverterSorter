using Microsoft.Extensions.Caching.Memory;
using ZakYip.WheelDiverterSorter.Communication.Adapters;
using ZakYip.WheelDiverterSorter.Core.LineModel;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;
using ZakYip.WheelDiverterSorter.Core.LineModel.Orchestration;
using ZakYip.WheelDiverterSorter.Core.LineModel.Topology;
using ZakYip.WheelDiverterSorter.Core.Sorting.Orchestration;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Execution;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Drivers;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Execution;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Ingress;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Upstream;
using ZakYip.WheelDiverterSorter.Execution.Orchestration;
using ZakYip.WheelDiverterSorter.Ingress;
using ZakYip.WheelDiverterSorter.Ingress.Adapters;
using ZakYip.WheelDiverterSorter.Ingress.Services;

namespace ZakYip.WheelDiverterSorter.Host.Services;

/// <summary>
/// 分拣服务扩展方法
/// </summary>
/// <remarks>
/// 统一注册分拣相关的核心服务，包括路径生成、路径执行、分拣编排等
/// PR-1: Host 层只负责 DI 注册，实际实现位于 Execution/Core 层
/// </remarks>
public static class SortingServiceExtensions
{
    /// <summary>
    /// 注册分拣相关服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置对象</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddSortingServices(
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

        // 注册适配器：将 Communication/Ingress 层的具体实现适配为 Execution 层抽象
        // ISensorEventProvider: 将 IParcelDetectionService 适配为 Execution 层可用的接口
        services.AddSingleton<ISensorEventProvider, SensorEventProviderAdapter>();
        // IUpstreamRoutingClient: 将 IRuleEngineClient 适配为 Execution 层可用的接口
        services.AddSingleton<IUpstreamRoutingClient, UpstreamRoutingClientAdapter>();
        // ICongestionDataCollector: 使用 Host 层实现
        services.AddSingleton<ICongestionDataCollector, CongestionDataCollector>();

        // 注册 UpstreamConnectionOptions（从配置绑定）
        services.Configure<UpstreamConnectionOptions>(options =>
        {
            options.FallbackTimeoutSeconds = configuration.GetValue<decimal>("RuleEngine:ChuteAssignmentTimeout:FallbackTimeoutSeconds", 5m);
        });

        // PR-1: 注册分拣异常处理器（实现位于 Execution.Orchestration）
        services.AddSingleton<ISortingExceptionHandler, SortingExceptionHandler>();

        // PR-1: 注册分拣编排服务（接口位于 Core.Sorting.Orchestration，实现已移至 Execution.Orchestration）
        // SortingOrchestrator 现在只依赖抽象接口，不再直接依赖 Communication/Ingress 项目
        services.AddSingleton<ISortingOrchestrator, SortingOrchestrator>();

        // 注册路由-拓扑一致性检查器（编排层服务）
        services.AddSingleton<IRouteTopologyConsistencyChecker, RouteTopologyConsistencyChecker>();

        // 注册调试分拣服务
        services.AddSingleton<DebugSortService>();

        return services;
    }
}
