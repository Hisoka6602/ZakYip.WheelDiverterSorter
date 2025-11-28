using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Hardware;
using ZakYip.WheelDiverterSorter.Core.LineModel;
using ZakYip.WheelDiverterSorter.Core.LineModel.Bindings;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Drivers;

namespace ZakYip.WheelDiverterSorter.Drivers.Vendors.ShuDiNiao;

/// <summary>
/// 数递鸟摆轮服务注册扩展方法
/// ShuDiNiao Wheel Diverter Service Collection Extensions
/// </summary>
/// <remarks>
/// 用于注册数递鸟摆轮相关实现到 DI 容器。
/// 使用方式：在 Program.cs 中调用 <c>builder.Services.AddShuDiNiaoWheelDiverter()</c>
/// </remarks>
public static class ShuDiNiaoWheelServiceCollectionExtensions
{
    /// <summary>
    /// 注册数递鸟摆轮实现
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <returns>服务集合</returns>
    /// <remarks>
    /// 注册以下服务：
    /// - <see cref="IWheelDiverterDriverManager"/> -> <see cref="ShuDiNiaoWheelDiverterDriverManager"/>
    /// - 单个摆轮设备的 <see cref="IWheelDiverterDevice"/> 实现（通过 <see cref="ShuDiNiaoWheelDiverterDeviceAdapter"/>）
    /// </remarks>
    public static IServiceCollection AddShuDiNiaoWheelDiverter(this IServiceCollection services)
    {
        // 注册数递鸟摆轮驱动管理器
        services.AddSingleton<IWheelDiverterDriverManager>(sp =>
        {
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger<ShuDiNiaoWheelDiverterDriverManager>();
            
            return new ShuDiNiaoWheelDiverterDriverManager(
                loggerFactory,
                logger);
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
    /// </remarks>
    public static IServiceCollection AddShuDiNiaoWheelDiverterSimulated(this IServiceCollection services)
    {
        // 注册数递鸟摆轮驱动管理器（仿真模式 - 同样使用 ShuDiNiaoWheelDiverterDriverManager）
        // 仿真模式由配置中的 UseSimulation 属性控制
        services.AddSingleton<IWheelDiverterDriverManager>(sp =>
        {
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger<ShuDiNiaoWheelDiverterDriverManager>();
            
            return new ShuDiNiaoWheelDiverterDriverManager(
                loggerFactory,
                logger);
        });

        return services;
    }
}
