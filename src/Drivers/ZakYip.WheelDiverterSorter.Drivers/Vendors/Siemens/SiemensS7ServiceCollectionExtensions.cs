using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
    /// <param name="options">S7 配置选项</param>
    /// <returns>服务集合</returns>
    /// <remarks>
    /// 注册以下服务：
    /// - <see cref="S7Connection"/> (用于 PLC 连接管理)
    /// - <see cref="S7InputPort"/> / <see cref="S7OutputPort"/> (用于 IO 端口操作)
    /// - <see cref="S7IoLinkageDriver"/> (用于 IO 联动控制)
    /// - <see cref="S7ConveyorDriveController"/> (用于传送带驱动控制)
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

        // 注册 IO 联动驱动
        services.AddSingleton<IIoLinkageDriver>(sp =>
        {
            var connection = sp.GetRequiredService<S7Connection>();
            var logger = sp.GetRequiredService<ILogger<S7IoLinkageDriver>>();
            return new S7IoLinkageDriver(connection, logger);
        });

        // 注册传送带驱动控制器
        // 注意：这里使用默认配置，实际使用时应从配置文件读取参数
        services.AddSingleton<IConveyorDriveController>(sp =>
        {
            var connection = sp.GetRequiredService<S7Connection>();
            var logger = sp.GetRequiredService<ILogger<S7ConveyorDriveController>>();
            return new S7ConveyorDriveController(
                connection,
                segmentId: "MainConveyor",
                startControlBit: 0,  // 启动控制位
                stopControlBit: 1,   // 停止控制位
                speedRegister: 100,  // 速度寄存器地址
                logger);
        });

        return services;
    }
}
