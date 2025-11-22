using Microsoft.Extensions.Caching.Memory;
using ZakYip.WheelDiverterSorter.Core.LineModel;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;
using ZakYip.WheelDiverterSorter.Core.LineModel.Orchestration;
using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using ZakYip.WheelDiverterSorter.Core.LineModel.Topology;
using ZakYip.WheelDiverterSorter.Execution;
using ZakYip.WheelDiverterSorter.Host.Application.Services;
using ZakYip.WheelDiverterSorter.Ingress;
using ZakYip.WheelDiverterSorter.Ingress.Services;

namespace ZakYip.WheelDiverterSorter.Host.Services;

/// <summary>
/// 分拣服务扩展方法
/// </summary>
/// <remarks>
/// 统一注册分拣相关的核心服务，包括路径生成、路径执行、分拣编排等
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
                var logger = serviceProvider.GetRequiredService<ILogger<CachedSwitchingPathGenerator>>();
                return new CachedSwitchingPathGenerator(innerGenerator, cache, logger);
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

        // 注册格口分配超时计算器
        services.AddSingleton<IChuteAssignmentTimeoutCalculator, ChuteAssignmentTimeoutCalculator>();

        // 注册 Application 层分拣编排服务
        services.AddSingleton<ISortingOrchestrator, SortingOrchestrator>();

        // 注册路由-拓扑一致性检查器（编排层服务）
        services.AddSingleton<IRouteTopologyConsistencyChecker, RouteTopologyConsistencyChecker>();

        // 注册调试分拣服务
        services.AddSingleton<DebugSortService>();

        return services;
    }

    /// <summary>
    /// 注册线体拓扑配置提供者
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置对象</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddTopologyConfiguration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 注册线体拓扑配置提供者
        services.AddSingleton<ILineTopologyConfigProvider>(serviceProvider =>
        {
            var topologyPath = configuration["TopologyConfiguration:FilePath"];
            if (!string.IsNullOrEmpty(topologyPath))
            {
                var fullPath = Path.IsPathRooted(topologyPath)
                    ? topologyPath
                    : Path.Combine(AppContext.BaseDirectory, topologyPath);
                return new JsonLineTopologyConfigProvider(fullPath);
            }
            // 使用默认拓扑配置
            return new DefaultLineTopologyConfigProvider();
        });

        return services;
    }
}
