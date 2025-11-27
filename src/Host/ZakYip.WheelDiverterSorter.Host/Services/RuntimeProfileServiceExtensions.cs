using ZakYip.WheelDiverterSorter.Core.Enums.System;
using ZakYip.WheelDiverterSorter.Core.LineModel.Runtime;
using ZakYip.WheelDiverterSorter.Host.Services.RuntimeProfiles;

namespace ZakYip.WheelDiverterSorter.Host.Services;

/// <summary>
/// 运行时配置文件服务注册扩展
/// Runtime profile service registration extensions
/// </summary>
public static class RuntimeProfileServiceExtensions
{
    /// <summary>
    /// 配置节名称
    /// Configuration section name
    /// </summary>
    public const string ConfigSectionName = "Runtime:Mode";

    /// <summary>
    /// 添加运行时配置文件服务
    /// Add runtime profile services based on configuration
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置对象</param>
    /// <returns>服务集合</returns>
    /// <remarks>
    /// 从配置 "Runtime:Mode" 读取运行模式，支持的值：
    /// - Production（默认）
    /// - Simulation
    /// - PerformanceTest
    /// </remarks>
    public static IServiceCollection AddRuntimeProfile(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 读取运行模式配置
        var modeString = configuration.GetValue<string>(ConfigSectionName);
        var mode = ParseRuntimeMode(modeString);

        // 根据模式注册对应的 IRuntimeProfile 实现
        services.AddSingleton<IRuntimeProfile>(sp => CreateRuntimeProfile(mode));

        return services;
    }

    /// <summary>
    /// 解析运行模式字符串
    /// Parse runtime mode from string
    /// </summary>
    private static RuntimeMode ParseRuntimeMode(string? modeString)
    {
        if (string.IsNullOrWhiteSpace(modeString))
        {
            return RuntimeMode.Production; // 默认生产模式
        }

        return modeString.Trim().ToLowerInvariant() switch
        {
            "production" => RuntimeMode.Production,
            "simulation" => RuntimeMode.Simulation,
            "performancetest" or "performance_test" or "performance-test" => RuntimeMode.PerformanceTest,
            _ when Enum.TryParse<RuntimeMode>(modeString, ignoreCase: true, out var parsed) => parsed,
            _ => RuntimeMode.Production // 未知值默认生产模式
        };
    }

    /// <summary>
    /// 根据模式创建对应的 IRuntimeProfile 实例
    /// Create the corresponding IRuntimeProfile instance based on mode
    /// </summary>
    private static IRuntimeProfile CreateRuntimeProfile(RuntimeMode mode)
    {
        return mode switch
        {
            RuntimeMode.Production => new ProductionRuntimeProfile(),
            RuntimeMode.Simulation => new SimulationRuntimeProfile(),
            RuntimeMode.PerformanceTest => new PerformanceTestRuntimeProfile(),
            _ => new ProductionRuntimeProfile()
        };
    }
}
