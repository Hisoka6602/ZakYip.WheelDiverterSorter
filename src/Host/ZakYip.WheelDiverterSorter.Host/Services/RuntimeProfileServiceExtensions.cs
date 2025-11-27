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
    /// <remarks>
    /// 支持的格式:
    /// - 小写: production, simulation, performancetest
    /// - 带分隔符: performance_test, performance-test
    /// - 混合大小写: Production, Simulation, PerformanceTest
    /// 未知值默认返回 Production。
    /// </remarks>
    private static RuntimeMode ParseRuntimeMode(string? modeString)
    {
        if (string.IsNullOrWhiteSpace(modeString))
        {
            return RuntimeMode.Production; // 默认生产模式
        }

        // 标准化输入：移除空格，统一为小写
        var normalizedInput = NormalizeInput(modeString);

        return normalizedInput switch
        {
            "production" => RuntimeMode.Production,
            "simulation" => RuntimeMode.Simulation,
            "performancetest" => RuntimeMode.PerformanceTest,
            _ when TryParseAsDefinedEnum(modeString, out var parsed) => parsed,
            _ => RuntimeMode.Production // 未知值默认生产模式
        };
    }

    /// <summary>
    /// 标准化输入字符串：移除分隔符并转换为小写
    /// </summary>
    private static string NormalizeInput(string input)
    {
        return input.Trim()
            .Replace("_", "")
            .Replace("-", "")
            .ToLowerInvariant();
    }

    /// <summary>
    /// 尝试解析为已定义的枚举值
    /// 只接受字符串名称，不接受数字字符串
    /// </summary>
    private static bool TryParseAsDefinedEnum(string modeString, out RuntimeMode result)
    {
        // 首先检查是否为数字字符串，如果是则拒绝
        if (int.TryParse(modeString.Trim(), out _))
        {
            result = RuntimeMode.Production;
            return false;
        }

        // 尝试解析为枚举
        if (Enum.TryParse<RuntimeMode>(modeString, ignoreCase: true, out var parsed) &&
            Enum.IsDefined(parsed))
        {
            result = parsed;
            return true;
        }

        result = RuntimeMode.Production;
        return false;
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
