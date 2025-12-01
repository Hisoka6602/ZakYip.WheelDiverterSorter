namespace ZakYip.WheelDiverterSorter.Simulation.Scenarios;

/// <summary>
/// 包裹期望定义
/// </summary>
/// <remarks>
/// 定义对特定包裹的期望验证条件
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
    /// 自定义断言谓词键
    /// </summary>
    /// <remarks>
    /// 用于更复杂的验证逻辑。运行器可以通过此键查找注册的自定义验证函数。
    /// 如果为null则使用默认验证。
    /// </remarks>
    public string? CustomValidatorKey { get; init; }
}
