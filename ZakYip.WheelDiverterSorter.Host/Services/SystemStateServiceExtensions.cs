using Microsoft.Extensions.DependencyInjection;
using ZakYip.WheelDiverterSorter.Host.StateMachine;

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
    /// <param name="initialState">初始状态（默认为Ready）</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddSystemStateManagement(
        this IServiceCollection services,
        SystemState initialState = SystemState.Ready)
    {
        // 注册系统状态管理器为单例
        services.AddSingleton<ISystemStateManager>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<SystemStateManager>>();
            return new SystemStateManager(logger, initialState);
        });

        return services;
    }
}
