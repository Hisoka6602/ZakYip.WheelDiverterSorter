using Microsoft.Extensions.DependencyInjection;
using ZakYip.WheelDiverterSorter.Core.LineModel.Orchestration;

namespace ZakYip.WheelDiverterSorter.Host.Services;

/// <summary>
/// 路由-拓扑一致性检查服务扩展
/// </summary>
public static class ConsistencyCheckServiceExtensions
{
    /// <summary>
    /// 添加路由-拓扑一致性检查服务
    /// </summary>
    public static IServiceCollection AddConsistencyCheckServices(this IServiceCollection services)
    {
        // 注册一致性检查器
        services.AddSingleton<IRouteTopologyConsistencyChecker, RouteTopologyConsistencyChecker>();

        // 注册启动时一致性检查服务
        services.AddHostedService<StartupConsistencyCheckService>();

        return services;
    }
}
