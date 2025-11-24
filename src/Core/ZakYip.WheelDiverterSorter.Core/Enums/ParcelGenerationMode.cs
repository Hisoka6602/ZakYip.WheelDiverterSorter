using System.ComponentModel;

namespace ZakYip.WheelDiverterSorter.Core.Enums;

/// <summary>
/// 包裹生成模式
/// </summary>
public enum ParcelGenerationMode
{
    /// <summary>
    /// 均匀间隔：包裹按固定间隔到达
    /// </summary>
    [Description("均匀间隔")]
    UniformInterval,

    /// <summary>
    /// 泊松分布：模拟真实的随机到达
    /// </summary>
    [Description("泊松分布")]
    PoissonDistribution,

    /// <summary>
    /// 批量：一次性生成所有包裹
    /// </summary>
    [Description("批量")]
    Batch,

    /// <summary>
    /// 高密度：模拟拥堵场景
    /// </summary>
    [Description("高密度")]
    HighDensity
}
