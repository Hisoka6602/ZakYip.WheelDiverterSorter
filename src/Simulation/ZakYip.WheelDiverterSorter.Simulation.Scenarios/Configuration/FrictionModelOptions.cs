namespace ZakYip.WheelDiverterSorter.Simulation.Scenarios.Configuration;

/// <summary>
/// 摩擦模型配置选项
/// </summary>
/// <remarks>
/// 定义包裹在传送带上由于摩擦因子导致的速度差异。
/// 摩擦因子应用于理想到达时间，模拟包裹在不同段上的实际运行时间差异。
/// </remarks>
public record class FrictionModelOptions
{
    /// <summary>
    /// 最小摩擦因子（例如 0.8 表示比理想时间快 20%）
    /// </summary>
    public decimal MinFactor { get; init; } = 0.8m;

    /// <summary>
    /// 最大摩擦因子（例如 1.2 表示比理想时间慢 20%）
    /// </summary>
    public decimal MaxFactor { get; init; } = 1.2m;

    /// <summary>
    /// 是否使用确定性序列（固定随机种子）
    /// </summary>
    /// <remarks>
    /// true: 使用固定种子，每次运行产生相同的摩擦因子序列
    /// false: 使用真随机，每次运行产生不同的摩擦因子
    /// </remarks>
    public bool IsDeterministic { get; init; } = true;

    /// <summary>
    /// 随机数种子（仅在 IsDeterministic=true 时使用）
    /// </summary>
    public int? Seed { get; init; } = 42;
}
