using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
    /// <param name="options">S7 配置选项</param>
    /// <returns>服务集合</returns>
    /// <remarks>
    /// 注册以下服务：
    /// - <see cref="S7Connection"/> (用于 PLC 连接管理)
    /// - <see cref="S7InputPort"/> / <see cref="S7OutputPort"/> (用于 IO 端口操作)
    /// 
    /// 注意：根据 TD-037 解决方案，Siemens S7 **不支持摆轮驱动**。
    /// 摆轮功能请使用 Leadshine 或 ShuDiNiao 厂商驱动。
    /// </remarks>
    public static IServiceCollection AddSiemensS7(this IServiceCollection services, S7Options options)
    {
        // 注册 S7 连接管理器
        services.AddSingleton(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<S7Connection>>();
            return new S7Connection(logger, options);
        });

        // TODO: 添加 IO 联动驱动注册 (IIoLinkageDriver)
        // TODO: 添加传送带驱动注册 (IConveyorDriveController)

        return services;
    }
}
