using Microsoft.Extensions.DependencyInjection;
using ZakYip.WheelDiverterSorter.Execution.Monitoring;

namespace ZakYip.WheelDiverterSorter.Execution.Extensions;

/// <summary>
/// 包裹丢失监控服务扩展
/// Parcel loss monitoring service extensions for dependency injection
/// </summary>
public static class ParcelLossMonitoringServiceExtensions
{
    /// <summary>
    /// 添加包裹丢失监控服务
    /// Add parcel loss monitoring service to the service collection
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddParcelLossMonitoring(this IServiceCollection services)
    {
        // 注册包裹丢失监控服务为单例，以便可以在 SortingOrchestrator 中注入并订阅事件
        // Register as singleton so it can be injected into SortingOrchestrator
        services.AddSingleton<ParcelLossMonitoringService>();
        
        // 同时注册为后台服务，以便在应用启动时自动启动监控循环
        // Also register as hosted service to automatically start monitoring loop on app startup
        services.AddHostedService(sp => sp.GetRequiredService<ParcelLossMonitoringService>());

        return services;
    }
}
