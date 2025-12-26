using Microsoft.Extensions.DependencyInjection;
using ZakYip.WheelDiverterSorter.Application.Services;
using ZakYip.WheelDiverterSorter.Application.Services.Config;
using ZakYip.WheelDiverterSorter.Application.Services.Caching;
using ZakYip.WheelDiverterSorter.Application.Services.Sorting;
using ZakYip.WheelDiverterSorter.Application.Services.Metrics;
using ZakYip.WheelDiverterSorter.Application.Services.Topology;
using ZakYip.WheelDiverterSorter.Application.Services.WheelDiverter;

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
    /// - 统一滑动配置缓存 (ISlidingConfigCache) - 1小时滑动过期，支持热更新
    /// - 系统配置服务 (ISystemConfigService)
    /// - 日志配置服务 (ILoggingConfigService)
    /// - 通信配置服务 (ICommunicationConfigService)
    /// - IO联动配置服务 (IIoLinkageConfigService)
    /// - 厂商配置服务 (IVendorConfigService)
    /// - 改口服务 (IChangeParcelChuteService)
    /// - 性能指标服务 (SorterMetrics)
    /// - 通信统计服务 (CommunicationStatsService)
    /// </remarks>
    public static IServiceCollection AddWheelDiverterApplication(this IServiceCollection services)
    {
        // 注册统一滑动配置缓存（1小时滑动过期，支持热更新）
        services.AddSingleton<ISlidingConfigCache, SlidingConfigCache>();
        
        // 注册配置服务（单例模式，提高性能并确保状态一致性）
        services.AddSingleton<ISystemConfigService, SystemConfigService>();
        services.AddSingleton<ILoggingConfigService, LoggingConfigService>();
        services.AddSingleton<ICommunicationConfigService, CommunicationConfigService>();
        services.AddSingleton<IIoLinkageConfigService, IoLinkageConfigService>();
        services.AddSingleton<IVendorConfigService, VendorConfigService>();
        services.AddSingleton<IConveyorSegmentService, ConveyorSegmentService>();
        
        // 注册改口服务
        services.AddSingleton<IChangeParcelChuteService, ChangeParcelChuteService>();
        
        // 注册性能指标服务
        services.AddSingleton<SorterMetrics>();
        
        // 注册通信统计服务（单例模式，确保计数器全局唯一）
        // 同时注册为 IMessageStatsCallback 以供 Communication 层使用（无需适配器）
        services.AddSingleton<ICommunicationStatsService, CommunicationStatsService>();
        services.AddSingleton<ZakYip.WheelDiverterSorter.Communication.Abstractions.IMessageStatsCallback>(
            sp => sp.GetRequiredService<ICommunicationStatsService>());
        
        // 注:分拣统计服务已移至 Observability 层注册
        
        // 注册拓扑相关服务（单例模式）
        services.AddSingleton<IChutePathTopologyService, ChutePathTopologyService>();
        
        // 注册摆轮连接管理服务
        services.AddSingleton<IWheelDiverterConnectionService, WheelDiverterConnectionService>();
        
        return services;
    }
}
