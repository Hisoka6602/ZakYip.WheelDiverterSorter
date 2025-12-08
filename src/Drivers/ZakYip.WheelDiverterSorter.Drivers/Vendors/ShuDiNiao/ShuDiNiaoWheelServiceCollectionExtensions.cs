using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware.Vendors;
using ZakYip.WheelDiverterSorter.Core.Hardware;
using ZakYip.WheelDiverterSorter.Core.LineModel;
using ZakYip.WheelDiverterSorter.Core.LineModel.Bindings;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;
using ZakYip.WheelDiverterSorter.Core.Hardware.Devices;
using ZakYip.WheelDiverterSorter.Core.Hardware.IoLinkage;
using ZakYip.WheelDiverterSorter.Core.Hardware.Mappings;
using ZakYip.WheelDiverterSorter.Core.Hardware.Ports;
using ZakYip.WheelDiverterSorter.Core.Hardware.Providers;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Drivers.Vendors.ShuDiNiao.Configuration;
using ZakYip.WheelDiverterSorter.Observability.Utilities;

namespace ZakYip.WheelDiverterSorter.Drivers.Vendors.ShuDiNiao;

/// <summary>
/// 数递鸟摆轮服务注册扩展方法
/// ShuDiNiao Wheel Diverter Service Collection Extensions
/// </summary>
/// <remarks>
/// 用于注册数递鸟摆轮相关实现到 DI 容器。
/// 使用方式：在 Program.cs 中调用 <c>builder.Services.AddShuDiNiaoWheelDiverter()</c>
/// 
/// 支持两种模式：
/// - 客户端模式（默认）：系统主动连接到摆轮设备
/// - 服务端模式：系统监听设备连接
/// 
/// 通过 ShuDiNiaoOptions.Mode 配置项控制模式选择。
/// </remarks>
public static class ShuDiNiaoWheelServiceCollectionExtensions
{
    /// <summary>
    /// 注册数递鸟摆轮实现（支持客户端和服务端模式）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <returns>服务集合</returns>
    /// <remarks>
    /// 注册以下服务：
    /// - <see cref="IWheelDiverterDriverManager"/> -> <see cref="ShuDiNiaoWheelDiverterDriverManager"/>（客户端模式）
    /// - <see cref="ShuDiNiaoWheelServer"/>（仅在服务端模式时注册）
    /// - 单个摆轮设备的 <see cref="IWheelDiverterDevice"/> 实现（通过 <see cref="ShuDiNiaoWheelDiverterDeviceAdapter"/>）
    /// 
    /// 模式选择通过配置项 "WheelDiverter:ShuDiNiao:Mode" 控制：
    /// - "Client"（默认）：使用客户端模式，系统主动连接设备
    /// - "Server"：使用服务端模式，系统监听设备连接
    /// </remarks>
    public static IServiceCollection AddShuDiNiaoWheelDiverter(this IServiceCollection services)
    {
        // 注册配置选项
        services.AddOptions<ShuDiNiaoOptions>()
            .BindConfiguration(ShuDiNiaoOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // 注册数递鸟摆轮驱动管理器（客户端模式）
        services.AddSingleton<IWheelDiverterDriverManager>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<ShuDiNiaoOptions>>().Value;
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger<ShuDiNiaoWheelDiverterDriverManager>();

            if (options.Mode == ShuDiNiaoMode.Server)
            {
                logger.LogWarning(
                    "数递鸟配置为服务端模式，但 IWheelDiverterDriverManager 仅支持客户端模式。" +
                    "若需使用服务端模式，请注入 ShuDiNiaoWheelServer 并手动启动。");
            }
            
            return new ShuDiNiaoWheelDiverterDriverManager(
                loggerFactory,
                logger,
                sp.GetRequiredService<ISystemClock>());
        });

        // 注册数递鸟摆轮服务器（仅在服务端模式时注册）
        services.AddSingleton<ShuDiNiaoWheelServer>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<ShuDiNiaoOptions>>().Value;
            
            // 仅在服务端模式时创建实例
            if (options.Mode != ShuDiNiaoMode.Server)
            {
                throw new InvalidOperationException(
                    "ShuDiNiaoWheelServer 仅在服务端模式 (Mode=Server) 下可用。" +
                    "当前模式为客户端模式，请使用 IWheelDiverterDriverManager。");
            }
            
            var logger = sp.GetRequiredService<ILogger<ShuDiNiaoWheelServer>>();
            var systemClock = sp.GetRequiredService<ISystemClock>();
            var safeExecutor = sp.GetRequiredService<ISafeExecutionService>();

            return new ShuDiNiaoWheelServer(
                options.ServerListenAddress,
                options.ServerListenPort,
                logger,
                systemClock,
                safeExecutor);
        });

        return services;
    }

    /// <summary>
    /// 注册数递鸟摆轮实现（仿真模式）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <returns>服务集合</returns>
    /// <remarks>
    /// 注册数递鸟摆轮的仿真实现，用于测试和开发环境。
    /// 使用相同的驱动管理器，但配置会通过配置仓储设置为仿真模式。
    /// 仿真模式始终使用客户端模式。
    /// </remarks>
    public static IServiceCollection AddShuDiNiaoWheelDiverterSimulated(this IServiceCollection services)
    {
        // 注册配置选项
        services.AddOptions<ShuDiNiaoOptions>()
            .BindConfiguration(ShuDiNiaoOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // 注册数递鸟摆轮驱动管理器（仿真模式 - 同样使用 ShuDiNiaoWheelDiverterDriverManager）
        // 仿真模式由配置中的 UseSimulation 属性控制
        services.AddSingleton<IWheelDiverterDriverManager>(sp =>
        {
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger<ShuDiNiaoWheelDiverterDriverManager>();
            
            return new ShuDiNiaoWheelDiverterDriverManager(
                loggerFactory,
                logger,
                sp.GetRequiredService<ISystemClock>());
        });

        return services;
    }
}
