using ZakYip.WheelDiverterSorter.Simulation.Configuration;

namespace ZakYip.WheelDiverterSorter.Simulation.Scenarios;

/// <summary>
/// 仿真场景定义
/// </summary>
/// <remarks>
/// 定义一个完整的仿真场景，包括配置参数和预期验证条件
/// </remarks>
public record class SimulationScenario
{
    /// <summary>
    /// 场景名称
    /// </summary>
    public required string ScenarioName { get; init; }

    /// <summary>
    /// 仿真配置选项
    /// </summary>
    public required SimulationOptions Options { get; init; }

    /// <summary>
    /// 包裹期望列表（用于验证）
    /// </summary>
    /// <remarks>
    /// 如果为空，则只验证全局不变量（例如 SortedToWrongChuteCount == 0）
    /// </remarks>
    public IReadOnlyList<ParcelExpectation>? Expectations { get; init; }
}
