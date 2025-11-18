namespace ZakYip.Sorting.Core.Overload;

/// <summary>
/// 策略配置 Profile，用于仿真中对比不同 Overload / 路由参数组合。
/// Strategy configuration profile for comparing different Overload/routing parameter combinations in simulation.
/// </summary>
public sealed record StrategyProfile
{
    /// <summary>
    /// Profile 名称（例如 "Baseline", "AggressiveOverload", "Conservative"）
    /// Profile name (e.g., "Baseline", "AggressiveOverload", "Conservative")
    /// </summary>
    public required string ProfileName { get; init; }

    /// <summary>
    /// 中文描述，方便报表展示
    /// Chinese description for report display
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// 复用现有 OverloadPolicyConfiguration
    /// Reuse existing OverloadPolicyConfiguration
    /// </summary>
    public required OverloadPolicyConfiguration OverloadPolicy { get; init; }

    /// <summary>
    /// 可选：后续扩展路由相关参数，例如 TTL 系数、提前量等
    /// Optional: Future extension for routing-related parameters, e.g., TTL factor, lead time, etc.
    /// 默认 1.0
    /// </summary>
    public decimal? RouteTimeBudgetFactor { get; init; } = 1.0m;
}
