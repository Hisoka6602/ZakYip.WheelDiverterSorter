using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using ZakYip.WheelDiverterSorter.Core.Events.Monitoring;
using ZakYip.WheelDiverterSorter.Core.LineModel.Tracing;
using ZakYip.WheelDiverterSorter.Observability.Tracing;
using ZakYip.WheelDiverterSorter.Observability.ConfigurationAudit;

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

    /// <summary>
    /// 添加告警服务
    /// Add alarm service to the service collection
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddAlarmService(this IServiceCollection services)
    {
        services.AddSingleton<AlarmService>();
        return services;
    }

    /// <summary>
    /// 添加告警接收器（IAlertSink）
    /// Add alert sinks to the service collection
    /// 默认注册 FileAlertSink、LogAlertSink 和 AlertHistoryService
    /// Registers FileAlertSink, LogAlertSink and AlertHistoryService by default
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddAlertSinks(this IServiceCollection services)
    {
        // 注册所有 IAlertSink 实现
        services.AddSingleton<IAlertSink, FileAlertSink>();
        services.AddSingleton<IAlertSink, LogAlertSink>();
        services.AddSingleton<AlertHistoryService>(); // 单独注册以便可以直接注入
        services.AddSingleton<IAlertSink>(sp => sp.GetRequiredService<AlertHistoryService>()); // 同时注册为 IAlertSink
        return services;
    }

    /// <summary>
    /// 添加包裹生命周期日志记录服务
    /// Add parcel lifecycle logging service to the service collection
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddParcelLifecycleLogger(this IServiceCollection services)
    {
        services.AddSingleton<IParcelLifecycleLogger, ParcelLifecycleLogger>();
        return services;
    }

    /// <summary>
    /// 添加包裹追踪日志服务
    /// Add parcel trace logging service to the service collection
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddParcelTraceLogging(this IServiceCollection services)
    {
        services.AddSingleton<IParcelTraceSink, FileBasedParcelTraceSink>();
        return services;
    }

    /// <summary>
    /// 添加日志清理服务
    /// Add log cleanup service to the service collection
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddLogCleanup(this IServiceCollection services)
    {
        services.AddSingleton<ILogCleanupPolicy, DefaultLogCleanupPolicy>();
        services.AddHostedService<LogCleanupHostedService>();
        return services;
    }

    /// <summary>
    /// 添加配置审计日志服务
    /// Add configuration audit logger to the service collection
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddConfigurationAuditLogger(this IServiceCollection services)
    {
        services.AddSingleton<IConfigurationAuditLogger, ConfigurationAuditLogger>();
        return services;
    }
}
