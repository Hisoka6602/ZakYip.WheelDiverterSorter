// PR-RS14: This file re-exports ParcelExpectation from Simulation.Scenarios for backward compatibility.
// New code should use ZakYip.WheelDiverterSorter.Simulation.Scenarios directly.

using ZakYip.WheelDiverterSorter.Simulation.Results;

namespace ZakYip.WheelDiverterSorter.Simulation.Scenarios;

/// <summary>
/// 包裹期望定义（向后兼容类型，保留 AssertPredicate 支持）
/// </summary>
/// <remarks>
/// PR-RS14: 此类型扩展自 Simulation.Scenarios 共享库的定义，
/// 添加了对 SimulatedParcelResultEventArgs 的断言支持。
/// 新代码如果需要基本期望定义，可直接使用 Simulation.Scenarios.ParcelExpectation。
/// </remarks>
public record struct ParcelExpectation
{
    /// <summary>
    /// 包裹序号（场景内的索引，从0开始）
    /// </summary>
    public long Index { get; init; }

    /// <summary>
    /// 期望的目标格口ID
    /// </summary>
    /// <remarks>
    /// 如果为null，表示不关心目标格口，只验证状态
    /// </remarks>
    public long? ExpectedTargetChuteId { get; init; }

    /// <summary>
    /// 自定义断言谓词
    /// </summary>
    /// <remarks>
    /// 用于更复杂的验证逻辑，如果为null则使用默认验证。
    /// 此属性保留在 Simulation 项目中，因为它依赖于 SimulatedParcelResultEventArgs。
    /// </remarks>
    public Func<SimulatedParcelResultEventArgs, bool>? AssertPredicate { get; init; }
}
