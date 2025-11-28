using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Execution;

namespace ZakYip.WheelDiverterSorter.Execution.PathExecution;

/// <summary>
/// 路径执行服务扩展方法
/// </summary>
public static class PathExecutionServiceExtensions
{
    /// <summary>
    /// 添加统一的路径执行服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <returns>服务集合</returns>
    /// <remarks>
    /// <para>此方法会注册 IPathExecutionService，将路径执行、失败处理和指标采集统一封装。</para>
    /// <para>依赖项（需要提前注册）：</para>
    /// <list type="bullet">
    /// <item>ISwitchingPathExecutor - 路径执行器（真实驱动或仿真驱动）</item>
    /// <item>IPathFailureHandler - 路径失败处理器</item>
    /// <item>ISystemClock - 系统时钟</item>
    /// <item>PrometheusMetrics（可选）- 指标服务</item>
    /// </list>
    /// </remarks>
    public static IServiceCollection AddPathExecutionService(this IServiceCollection services)
    {
        services.TryAddSingleton<IPathExecutionService>(sp =>
        {
            var pathExecutor = sp.GetRequiredService<ISwitchingPathExecutor>();
            var pathFailureHandler = sp.GetRequiredService<IPathFailureHandler>();
            var clock = sp.GetRequiredService<Core.Utilities.ISystemClock>();
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<PathExecutionService>>();
            var metrics = sp.GetService<Observability.PrometheusMetrics>();

            return new PathExecutionService(pathExecutor, pathFailureHandler, clock, logger, metrics);
        });

        return services;
    }
}
