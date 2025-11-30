using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ZakYip.WheelDiverterSorter.Core.Enums.System;
using ZakYip.WheelDiverterSorter.Host.Services.Workers;
using ZakYip.WheelDiverterSorter.Application.Extensions;

namespace ZakYip.WheelDiverterSorter.Host.Services.Extensions;

/// <summary>
/// WheelDiverterSorter Host 层服务集合扩展方法
/// Host layer thin wrapper for WheelDiverterSorter services
/// </summary>
/// <remarks>
/// PR-S6: 重命名为 WheelDiverterSorterHostServiceCollectionExtensions，
/// 与 Application 层的 WheelDiverterSorterServiceCollectionExtensions 区分开来。
/// 
/// PR-H1: Host 层依赖收缩 - 此类现在是 Application 层统一 DI 入口的薄包装。
/// 所有核心服务注册（Core/Execution/Drivers/Ingress/Communication/Observability/Simulation）
/// 现在由 Application 层的 WheelDiverterSorterServiceCollectionExtensions 处理。
/// 
/// PR-H2: Host 层继续瘦身 - 移除了所有业务接口、Commands、Pipeline、Repository 目录：
/// - Commands 目录已删除，改口功能由 Application 层 IChangeParcelChuteService 提供
/// - Pipeline 目录已删除，上游适配器已移至 Execution 层
/// - Application/Services 目录已删除，业务服务由 Application 层统一提供
/// 
/// Host 层只负责注册：
/// - 健康检查和系统状态管理（Host 特定）
/// - 健康状态提供器（Host 特定实现）
/// - 后台工作服务（Host 特定）
/// 
/// **依赖关系**：
/// Host → Application → (Core/Execution/Drivers/Ingress/Communication/Observability/Simulation)
/// 
/// **使用方法**：
/// <code>
/// builder.Services.AddWheelDiverterSorterHost(builder.Configuration);
/// </code>
/// </remarks>
public static class WheelDiverterSorterHostServiceCollectionExtensions
{
    /// <summary>
    /// 注册 WheelDiverterSorter Host 层服务
    /// Registers Host layer specific services for the WheelDiverterSorter system
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置对象</param>
    /// <returns>服务集合</returns>
    /// <remarks>
    /// 该方法按以下顺序注册服务：
    /// 1. Application 层的所有基础服务（通过 AddWheelDiverterSorter 调用）
    /// 2. 健康检查和系统状态管理（Host 特定）
    /// 3. 健康状态提供器（Host 特定实现）
    /// 4. 后台工作服务（Host 特定）
    /// </remarks>
    public static IServiceCollection AddWheelDiverterSorterHost(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 1. 调用 Application 层的统一 DI 入口注册所有基础服务
        // 这会注册所有 Core/Execution/Drivers/Ingress/Communication/Observability/Simulation 相关服务
        services.AddWheelDiverterSorter(configuration);

        // 2. 注册健康检查和系统状态管理（Host 特定）
        var enableHealthCheck = configuration.GetValue<bool>("HealthCheck:Enabled", true);
        if (enableHealthCheck)
        {
            services.AddHealthCheckServices();
            services.AddSystemStateManagement(
                SystemState.Booting,
                enableSelfTest: true);
        }
        else
        {
            services.AddSystemStateManagement(SystemState.Ready);
        }

        // 3. 注册健康状态提供器（Host 特定实现）
        services.AddSingleton<Observability.Runtime.Health.IHealthStatusProvider,
            Health.HostHealthStatusProvider>();

        // 4. 注册后台工作服务（Host 特定）
        services.AddHostedService<AlarmMonitoringWorker>();
        services.AddHostedService<RouteTopologyConsistencyCheckWorker>();

        return services;
    }
}
