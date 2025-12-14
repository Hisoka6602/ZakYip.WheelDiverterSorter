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
    /// <remarks>
    /// 此方法使用双重注册模式：同一实例同时注册为 Singleton 和 HostedService。
    /// 
    /// 原因说明：
    /// 1. Singleton 注册：使 SortingOrchestrator 可以通过构造函数注入并订阅 ParcelLostDetected 事件
    /// 2. HostedService 注册：确保服务在应用启动时自动启动后台监控循环
    /// 3. 使用 GetRequiredService：确保两个注册引用同一个实例，避免创建多个服务实例
    /// 
    /// 这是后台服务需要被其他组件注入时的标准模式。
    /// </remarks>
    public static IServiceCollection AddParcelLossMonitoring(this IServiceCollection services)
    {
        // 注册包裹丢失监控服务为单例，以便可以在 SortingOrchestrator 中注入并订阅事件
        // Register as singleton so it can be injected into SortingOrchestrator
        services.AddSingleton<ParcelLossMonitoringService>();
        
        // 同时注册为后台服务，以便在应用启动时自动启动监控循环
        // 使用 GetRequiredService 确保使用同一个实例
        // Also register as hosted service to automatically start monitoring loop on app startup
        // Use GetRequiredService to ensure the same instance is used
        services.AddHostedService(sp => sp.GetRequiredService<ParcelLossMonitoringService>());

        return services;
    }
}
