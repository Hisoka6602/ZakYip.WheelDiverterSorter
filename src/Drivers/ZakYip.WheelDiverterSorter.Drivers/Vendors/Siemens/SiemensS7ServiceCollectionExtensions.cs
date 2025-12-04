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
    /// - <see cref="S7ConveyorDriveController"/> (用于传送带驱动控制)
    /// 
    /// 注意：根据 TD-037 解决方案，Siemens S7 **不支持摆轮驱动**。
    /// 摆轮功能请使用 Leadshine 或 ShuDiNiao 厂商驱动。
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

        // 注册传送带驱动控制器
        // 注意：这里使用默认配置，实际使用时应从配置文件读取参数
        services.AddSingleton<IConveyorDriveController>(sp =>
        {
            var connection = sp.GetRequiredService<S7Connection>();
            var logger = sp.GetRequiredService<ILogger<S7ConveyorDriveController>>();
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            return new S7ConveyorDriveController(
                connection,
                segmentId: "MainConveyor",
                startControlBit: 0,  // 启动控制位
                stopControlBit: 1,   // 停止控制位
                speedRegister: 100,  // 速度寄存器地址
                logger,
                loggerFactory);
        });

        return services;
    }

    /// <summary>
    /// 注册西门子 S7 PLC 实现（已过时，请使用 AddSiemensS7(Action&lt;S7Options&gt;) 重载）
    /// </summary>
    [Obsolete("此方法不支持配置热更新，请使用 AddSiemensS7(Action<S7Options>) 重载")]
    public static IServiceCollection AddSiemensS7(this IServiceCollection services, S7Options options)
    {
        return services.AddSiemensS7(opts =>
        {
            opts.IpAddress = options.IpAddress;
            opts.Rack = options.Rack;
            opts.Slot = options.Slot;
            opts.CpuType = options.CpuType;
            opts.ConnectionTimeout = options.ConnectionTimeout;
            opts.ReadWriteTimeout = options.ReadWriteTimeout;
            opts.MaxReconnectAttempts = options.MaxReconnectAttempts;
            opts.ReconnectDelay = options.ReconnectDelay;
            opts.EnableHealthCheck = options.EnableHealthCheck;
            opts.HealthCheckIntervalSeconds = options.HealthCheckIntervalSeconds;
            opts.FailureThreshold = options.FailureThreshold;
            opts.EnablePerformanceMetrics = options.EnablePerformanceMetrics;
            opts.UseExponentialBackoff = options.UseExponentialBackoff;
            opts.MaxBackoffDelay = options.MaxBackoffDelay;
        });
    }
}
