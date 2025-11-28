using Microsoft.Extensions.DependencyInjection;
using ZakYip.WheelDiverterSorter.Application.Services;

namespace ZakYip.WheelDiverterSorter.Application;

/// <summary>
/// Application 层服务注册扩展方法
/// </summary>
/// <remarks>
/// 提供 Application 层所有服务的统一注册入口。
/// Host 层通过调用 AddWheelDiverterApplication() 注册所有应用服务。
/// </remarks>
public static class ApplicationServiceExtensions
{
    /// <summary>
    /// 注册 Application 层的所有服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <returns>服务集合</returns>
    /// <remarks>
    /// 该方法注册以下服务：
    /// - 系统配置服务 (ISystemConfigService)
    /// - 日志配置服务 (ILoggingConfigService)
    /// - 通信配置服务 (ICommunicationConfigService)
    /// - IO联动配置服务 (IIoLinkageConfigService)
    /// - 仿真模式提供者 (ISimulationModeProvider)
    /// - 调试分拣服务 (IDebugSortService)
    /// - 改口服务 (IChangeParcelChuteService)
    /// - 性能指标服务 (SorterMetrics)
    /// - 通信统计服务 (CommunicationStatsService)
    /// </remarks>
    public static IServiceCollection AddWheelDiverterApplication(this IServiceCollection services)
    {
        // 注册配置服务
        services.AddScoped<ISystemConfigService, SystemConfigService>();
        services.AddScoped<ILoggingConfigService, LoggingConfigService>();
        services.AddScoped<ICommunicationConfigService, CommunicationConfigService>();
        services.AddScoped<IIoLinkageConfigService, IoLinkageConfigService>();
        
        // 注册仿真模式提供者
        services.AddScoped<ISimulationModeProvider, SimulationModeProvider>();
        
        // 注册调试分拣服务
        services.AddSingleton<IDebugSortService, DebugSortService>();
        
        // 注册改口服务
        services.AddSingleton<IChangeParcelChuteService, ChangeParcelChuteService>();
        
        // 注册性能指标服务
        services.AddSingleton<SorterMetrics>();
        
        // 注册通信统计服务
        services.AddSingleton<CommunicationStatsService>();
        
        return services;
    }
}
