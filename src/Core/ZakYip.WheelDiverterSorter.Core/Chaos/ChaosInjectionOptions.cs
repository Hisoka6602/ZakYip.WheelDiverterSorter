namespace ZakYip.WheelDiverterSorter.Core.Chaos;

/// <summary>
/// 混沌注入选项配置
/// Chaos injection options configuration
/// </summary>
/// <remarks>
/// PR-41: Configuration for chaos testing behavior
/// </remarks>
public class ChaosInjectionOptions
{
    /// <summary>
    /// 是否启用混沌测试（默认禁用）
    /// Enable chaos testing (default disabled)
    /// </summary>
    public bool Enabled { get; init; } = false;

    /// <summary>
    /// 通讯层配置
    /// Communication layer configuration
    /// </summary>
    public ChaosLayerOptions Communication { get; init; } = new();

    /// <summary>
    /// 驱动层配置
    /// Driver layer configuration
    /// </summary>
    public ChaosLayerOptions Driver { get; init; } = new();

    /// <summary>
    /// IO层配置
    /// IO layer configuration
    /// </summary>
    public ChaosLayerOptions Io { get; init; } = new();

    /// <summary>
    /// 随机种子（用于可重现的测试）
    /// Random seed for reproducible tests
    /// </summary>
    public int? Seed { get; init; }
}

/// <summary>
/// 混沌层级选项
/// Chaos layer options
/// </summary>
public class ChaosLayerOptions
{
    /// <summary>
    /// 异常注入概率（0.0 - 1.0）
    /// Exception injection probability (0.0 - 1.0)
    /// </summary>
    public double ExceptionProbability { get; init; } = 0.05;

    /// <summary>
    /// 延迟注入概率（0.0 - 1.0）
    /// Delay injection probability (0.0 - 1.0)
    /// </summary>
    public double DelayProbability { get; init; } = 0.1;

    /// <summary>
    /// 最小延迟（毫秒）
    /// Minimum delay in milliseconds
    /// </summary>
    public int MinDelayMs { get; init; } = 100;

    /// <summary>
    /// 最大延迟（毫秒）
    /// Maximum delay in milliseconds
    /// </summary>
    public int MaxDelayMs { get; init; } = 2000;

    /// <summary>
    /// 断连注入概率（0.0 - 1.0）
    /// Disconnect injection probability (0.0 - 1.0)
    /// </summary>
    public double DisconnectProbability { get; init; } = 0.02;

    /// <summary>
    /// 掉点/失联注入概率（0.0 - 1.0）
    /// Dropout injection probability (0.0 - 1.0)
    /// </summary>
    public double DropoutProbability { get; init; } = 0.03;
}

/// <summary>
/// 预定义的混沌配置方案
/// Predefined chaos configuration profiles
/// </summary>
public static class ChaosProfiles
{
    /// <summary>
    /// 轻度混沌（用于常规测试）
    /// Mild chaos (for regular testing)
    /// </summary>
    public static ChaosInjectionOptions Mild => new()
    {
        Enabled = true,
        Communication = new ChaosLayerOptions
        {
            ExceptionProbability = 0.01,
            DelayProbability = 0.05,
            MinDelayMs = 50,
            MaxDelayMs = 500,
            DisconnectProbability = 0.005
        },
        Driver = new ChaosLayerOptions
        {
            ExceptionProbability = 0.01,
            DelayProbability = 0.03,
            MinDelayMs = 50,
            MaxDelayMs = 300,
            DisconnectProbability = 0
        },
        Io = new ChaosLayerOptions
        {
            ExceptionProbability = 0,
            DelayProbability = 0,
            DropoutProbability = 0.01
        }
    };

    /// <summary>
    /// 中度混沌（用于压力测试）
    /// Moderate chaos (for stress testing)
    /// </summary>
    public static ChaosInjectionOptions Moderate => new()
    {
        Enabled = true,
        Communication = new ChaosLayerOptions
        {
            ExceptionProbability = 0.05,
            DelayProbability = 0.1,
            MinDelayMs = 100,
            MaxDelayMs = 1000,
            DisconnectProbability = 0.02
        },
        Driver = new ChaosLayerOptions
        {
            ExceptionProbability = 0.05,
            DelayProbability = 0.08,
            MinDelayMs = 100,
            MaxDelayMs = 800,
            DisconnectProbability = 0
        },
        Io = new ChaosLayerOptions
        {
            ExceptionProbability = 0,
            DelayProbability = 0,
            DropoutProbability = 0.03
        }
    };

    /// <summary>
    /// 高强度混沌（用于韧性极限测试）
    /// Heavy chaos (for resilience limit testing)
    /// </summary>
    public static ChaosInjectionOptions Heavy => new()
    {
        Enabled = true,
        Communication = new ChaosLayerOptions
        {
            ExceptionProbability = 0.1,
            DelayProbability = 0.2,
            MinDelayMs = 200,
            MaxDelayMs = 2000,
            DisconnectProbability = 0.05
        },
        Driver = new ChaosLayerOptions
        {
            ExceptionProbability = 0.1,
            DelayProbability = 0.15,
            MinDelayMs = 200,
            MaxDelayMs = 1500,
            DisconnectProbability = 0
        },
        Io = new ChaosLayerOptions
        {
            ExceptionProbability = 0,
            DelayProbability = 0,
            DropoutProbability = 0.08
        }
    };

    /// <summary>
    /// 禁用混沌（正常模式）
    /// Disabled chaos (normal mode)
    /// </summary>
    public static ChaosInjectionOptions Disabled => new()
    {
        Enabled = false
    };
}
