using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZakYip.WheelDiverterSorter.Core.Hardware;
using ZakYip.WheelDiverterSorter.Core.Hardware.IoLinkage;
using ZakYip.WheelDiverterSorter.Drivers.Vendors.Siemens.Configuration;

namespace ZakYip.WheelDiverterSorter.Drivers.Vendors.Siemens;

/// <summary>
/// 西门子 S7 PLC 服务注册扩展方法
/// Siemens S7 PLC Service Collection Extensions
/// </summary>
/// <remarks>
/// 用于注册西门子 S7 PLC 相关实现到 DI 容器。
/// 使用方式：在 Program.cs 中调用 <c>builder.Services.AddSiemensS7()</c>
/// </remarks>
public static class SiemensS7ServiceCollectionExtensions
{
    /// <summary>
    /// 注册西门子 S7 PLC 实现
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configureOptions">S7配置选项配置委托</param>
    /// <returns>服务集合</returns>
    /// <remarks>
    /// 注册以下服务：
    /// - <see cref="S7Connection"/> (用于 PLC 连接管理，支持热更新)
    /// - <see cref="S7InputPort"/> / <see cref="S7OutputPort"/> (用于 IO 端口操作)
    /// - <see cref="S7IoLinkageDriver"/> (用于 IO 联动控制)
    /// 
    /// 注意：根据 TD-037 解决方案，Siemens S7 **不支持摆轮驱动**。
    /// 摆轮功能请使用 Leadshine 或 ShuDiNiao 厂商驱动。
    /// 
    /// 皮带控制现由 IO 联动系统统一处理，不再需要专门的皮带驱动接口。
    /// 
    /// 支持配置热更新：通过 IOptionsMonitor 监听配置变更，自动重连 PLC。
    /// </remarks>
    public static IServiceCollection AddSiemensS7(
        this IServiceCollection services, 
        Action<S7Options> configureOptions)
    {
        // 配置 S7Options 支持热更新
        services.Configure(configureOptions);

        // 注册 S7 连接管理器（使用 IOptionsMonitor 支持热更新）
        services.AddSingleton<S7Connection>();

        // 注册 IO 联动驱动
        services.AddSingleton<IIoLinkageDriver>(sp =>
        {
            var connection = sp.GetRequiredService<S7Connection>();
            var logger = sp.GetRequiredService<ILogger<S7IoLinkageDriver>>();
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            return new S7IoLinkageDriver(connection, logger, loggerFactory);
        });

        return services;
    }

    /// <summary>
}
