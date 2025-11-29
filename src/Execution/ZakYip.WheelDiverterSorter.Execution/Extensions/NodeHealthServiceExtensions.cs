using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ZakYip.WheelDiverterSorter.Core.LineModel.Runtime.Health;
using ZakYip.WheelDiverterSorter.Execution.Health;

namespace ZakYip.WheelDiverterSorter.Execution;

/// <summary>
/// 节点健康服务扩展
/// Node health service extensions for dependency injection
/// </summary>
public static class NodeHealthServiceExtensions
{
    /// <summary>
    /// 添加节点健康服务
    /// Add node health services to the service collection
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddNodeHealthServices(this IServiceCollection services)
    {
        // 注册节点健康注册表（单例）
        // Register node health registry as singleton
        services.TryAddSingleton<INodeHealthRegistry, NodeHealthRegistry>();

        // 注册路径健康检查器（单例）
        // Register path health checker as singleton
        services.TryAddSingleton<PathHealthChecker>();

        // 注册节点健康监控后台服务
        // Register node health monitor background service
        services.AddHostedService<NodeHealthMonitorService>();

        return services;
    }
}
