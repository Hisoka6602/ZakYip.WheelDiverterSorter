using Microsoft.Extensions.DependencyInjection;

namespace ZakYip.WheelDiverterSorter.Observability;

/// <summary>
/// Observability服务扩展
/// Extension methods for registering observability services
/// </summary>
public static class ObservabilityServiceExtensions
{
    /// <summary>
    /// 添加Prometheus指标服务
    /// Add Prometheus metrics services to the service collection
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddPrometheusMetrics(this IServiceCollection services)
    {
        services.AddSingleton<PrometheusMetrics>();
        return services;
    }
}
