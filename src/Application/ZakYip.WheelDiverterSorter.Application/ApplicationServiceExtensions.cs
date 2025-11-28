using Microsoft.Extensions.DependencyInjection;
using ZakYip.WheelDiverterSorter.Application.Services;

namespace ZakYip.WheelDiverterSorter.Application;

/// <summary>
/// Application 层服务注册扩展方法
/// </summary>
public static class ApplicationServiceExtensions
{
    /// <summary>
    /// 注册 Application 层的所有服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddWheelDiverterApplication(this IServiceCollection services)
    {
        // 注册配置服务
        services.AddScoped<ISystemConfigService, SystemConfigService>();
        services.AddScoped<ILoggingConfigService, LoggingConfigService>();
        
        // 注册仿真模式提供者
        services.AddScoped<ISimulationModeProvider, SimulationModeProvider>();
        
        // 注册性能指标服务
        services.AddSingleton<SorterMetrics>();
        
        // 注册通信统计服务
        services.AddSingleton<CommunicationStatsService>();
        
        return services;
    }
}
