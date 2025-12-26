using Microsoft.Extensions.DependencyInjection;
using ZakYip.WheelDiverterSorter.Host.StateMachine;
using ZakYip.WheelDiverterSorter.Communication.Health;
using ZakYip.WheelDiverterSorter.Core.LineModel.Runtime.Health;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Execution.SelfTest;
using ZakYip.WheelDiverterSorter.Application.Services;
using ZakYip.WheelDiverterSorter.Application.Services.Config;
using ZakYip.WheelDiverterSorter.Application.Services.Caching;
using ZakYip.WheelDiverterSorter.Application.Services.Health;
using ZakYip.WheelDiverterSorter.Application.Services.Sorting;
using ZakYip.WheelDiverterSorter.Application.Services.Metrics;
using ZakYip.WheelDiverterSorter.Application.Services.Topology;
using ZakYip.WheelDiverterSorter.Application.Services.Debug;
using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using ZakYip.WheelDiverterSorter.Host.Services.Workers;
using ZakYip.WheelDiverterSorter.Core.Enums.System;

namespace ZakYip.WheelDiverterSorter.Host.Services.Extensions;

/// <summary>
/// 健康检查服务注册扩展
/// </summary>
public static class HealthCheckServiceExtensions
{
    /// <summary>
    /// 注册健康检查和自检服务
    /// </summary>
    public static IServiceCollection AddHealthCheckServices(this IServiceCollection services)
    {
        // 注册配置验证器
        services.AddSingleton<IConfigValidator, DefaultConfigValidator>();

        // 注册自检协调器
        services.AddSingleton<ISelfTestCoordinator, SystemSelfTestCoordinator>();

        // 注册上游健康检查器（可选，如果有RuleEngine客户端）
        // 注意：RuleEngineUpstreamHealthChecker需要IRuleEngineClient，
        // 但这是可选的，所以我们通过工厂方法创建
        services.AddSingleton<IUpstreamHealthChecker>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<RuleEngineUpstreamHealthChecker>>();
            var clock = sp.GetRequiredService<ISystemClock>();
            var client = sp.GetService<ZakYip.WheelDiverterSorter.Core.Abstractions.Upstream.IUpstreamRoutingClient>();
            return new RuleEngineUpstreamHealthChecker(client, "Default", logger, clock);
        });

        // 注册驱动器自检（暂时为空列表，可以在实际使用时添加）
        // 注意：实际的驱动器自检实现需要在Drivers层注册
        // services.AddSingleton<IDriverSelfTest, RelayWheelDiverterSelfTest>();

        // 使用装饰器模式包装SystemStateManager，添加BootAsync功能
        services.AddSingleton<SystemStateManager>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<SystemStateManager>>();
            var clock = sp.GetRequiredService<ISystemClock>();
            return new SystemStateManager(logger, clock, SystemState.Booting);
        });

        services.AddSingleton<ISystemStateManager>(sp =>
        {
            var inner = sp.GetRequiredService<SystemStateManager>();
            var coordinator = sp.GetService<ISelfTestCoordinator>();
            var clock = sp.GetRequiredService<ISystemClock>();
            var logger = sp.GetRequiredService<ILogger<SystemStateManagerWithBoot>>();
            return new SystemStateManagerWithBoot(inner, coordinator, clock, logger);
        });

        // 注册启动自检服务
        services.AddHostedService<BootHostedService>();
        
        // 注册摆轮初始化服务
        services.AddHostedService<WheelDiverterInitHostedService>();
        
        // 注册系统状态与摆轮协调服务（监听状态变化，在进入Running时设置摆轮为直行）
        services.AddHostedService<SystemStateWheelDiverterCoordinator>();

        // 注册运行前健康检查服务
        services.AddSingleton<IPreRunHealthCheckService, PreRunHealthCheckService>();

        return services;
    }
}
