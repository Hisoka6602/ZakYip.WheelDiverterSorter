using ZakYip.WheelDiverterSorter.Core.Sorting.Overload;
using ZakYip.WheelDiverterSorter.Core.Enums;

namespace ZakYip.WheelDiverterSorter.Simulation.Strategies;

/// <summary>
/// 策略实验配置
/// Strategy experiment configuration
/// </summary>
public sealed record StrategyExperimentConfig
{
    /// <summary>
    /// 每个策略下仿真包裹数
    /// Number of parcels to simulate per strategy
    /// </summary>
    public required int ParcelCount { get; init; }

    /// <summary>
    /// 放包间隔
    /// Release interval between parcels
    /// </summary>
    public required TimeSpan ReleaseInterval { get; init; }

    /// <summary>
    /// 固定随机种子，保证各策略一致
    /// Fixed random seed to ensure consistency across strategies
    /// </summary>
    public required int RandomSeed { get; init; }

    /// <summary>
    /// 待测试的策略 Profiles
    /// Strategy profiles to test
    /// </summary>
    public required IReadOnlyList<StrategyProfile> Profiles { get; init; }

    /// <summary>
    /// 输出目录
    /// Output directory for reports
    /// </summary>
    public string OutputDirectory { get; init; } = "./reports/strategy";
}
