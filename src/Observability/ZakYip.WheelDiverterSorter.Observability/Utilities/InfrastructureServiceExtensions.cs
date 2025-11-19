using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ZakYip.WheelDiverterSorter.Observability.Utilities;

/// <summary>
/// 基础设施服务扩展方法
/// Infrastructure service extension methods
/// </summary>
public static class InfrastructureServiceExtensions
{
    /// <summary>
    /// 添加基础设施服务（包括安全执行器、系统时钟、日志去重）
    /// Add infrastructure services (including safe executor, system clock, log deduplicator)
    /// </summary>
    /// <param name="services">服务集合 - Service collection</param>
    /// <returns>服务集合 - Service collection</returns>
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        // Register system clock as singleton
        services.TryAddSingleton<ISystemClock, LocalSystemClock>();

        // Register log deduplicator as singleton
        services.TryAddSingleton<ILogDeduplicator>(sp =>
        {
            var systemClock = sp.GetRequiredService<ISystemClock>();
            return new LogDeduplicator(systemClock, windowDurationSeconds: 1);
        });

        // Register safe execution service as singleton
        services.TryAddSingleton<ISafeExecutionService, SafeExecutionService>();

        return services;
    }
}
