using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Execution;

namespace ZakYip.WheelDiverterSorter.Execution.Concurrency;

/// <summary>
/// 并发控制服务扩展
/// </summary>
public static class ConcurrencyServiceExtensions
{
    /// <summary>
    /// 添加并发控制服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddConcurrencyControl(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 注册并发配置
        services.Configure<ConcurrencyOptions>(
            options => configuration.GetSection(ConcurrencyOptions.SectionName).Bind(options));

        return services;
    }

    /// <summary>
    /// 使用并发控制包装现有的路径执行器
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <returns>服务集合</returns>
    /// <remarks>
    /// 此方法会装饰已注册的ISwitchingPathExecutor服务，
    /// 添加并发控制功能而不改变原有实现
    /// </remarks>
    public static IServiceCollection DecorateWithConcurrencyControl(
        this IServiceCollection services)
    {
        // 使用装饰器模式包装现有执行器
        services.Decorate<ISwitchingPathExecutor>((inner, sp) =>
        {
            var options = sp.GetRequiredService<IOptions<ConcurrencyOptions>>();
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ConcurrentSwitchingPathExecutor>>();
            var clock = sp.GetRequiredService<Core.Utilities.ISystemClock>();

            return new ConcurrentSwitchingPathExecutor(inner, options, logger, clock);
        });

        return services;
    }
}
