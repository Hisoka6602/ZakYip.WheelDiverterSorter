using Microsoft.Extensions.DependencyInjection;
using ZakYip.WheelDiverterSorter.Execution.SelfTest;
using ZakYip.WheelDiverterSorter.Host.StateMachine;
using ZakYip.WheelDiverterSorter.Core.Utilities;

namespace ZakYip.WheelDiverterSorter.Host.Services;

/// <summary>
/// 系统状态管理服务扩展
/// </summary>
public static class SystemStateServiceExtensions
{
    /// <summary>
    /// 添加系统状态管理服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="initialState">初始状态（默认为Ready，如果启用自检则为Booting）</param>
    /// <param name="enableSelfTest">是否启用启动自检（默认false，保持向后兼容）</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddSystemStateManagement(
        this IServiceCollection services,
        SystemState initialState = SystemState.Ready,
        bool enableSelfTest = false)
    {
        if (enableSelfTest)
        {
            // 启用自检模式：注册SystemStateManager和装饰器
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
        }
        else
        {
            // 传统模式：直接注册SystemStateManager（向后兼容）
            services.AddSingleton<ISystemStateManager>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<SystemStateManager>>();
                var clock = sp.GetRequiredService<ISystemClock>();
                return new SystemStateManager(logger, clock, initialState);
            });
        }

        return services;
    }
}
