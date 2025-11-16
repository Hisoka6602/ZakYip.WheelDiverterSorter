namespace ZakYip.WheelDiverterSorter.Simulation.Configuration;

/// <summary>
/// 仿真配置选项
/// </summary>
/// <remarks>
/// 定义仿真运行的各种参数，包括包裹数量、线速、分拣模式等
/// </remarks>
public record class SimulationOptions
{
    /// <summary>
    /// 要仿真的包裹数量
    /// </summary>
    public required int ParcelCount { get; init; }

    /// <summary>
    /// 线速（毫米/秒）
    /// </summary>
    public required decimal LineSpeedMmps { get; init; }

    /// <summary>
    /// 包裹到达间隔
    /// </summary>
    public required TimeSpan ParcelInterval { get; init; }

    /// <summary>
    /// 分拣模式
    /// </summary>
    /// <remarks>
    /// 可选值：Formal（正式模式，从RuleEngine获取）、FixedChute（固定格口）、RoundRobin（轮询）
    /// </remarks>
    public required string SortingMode { get; init; }

    /// <summary>
    /// 固定格口ID列表（仅在FixedChute模式下使用）
    /// </summary>
    public IReadOnlyList<long>? FixedChuteIds { get; init; }

    /// <summary>
    /// 异常格口ID（包裹分拣失败时的备用格口）
    /// </summary>
    public long ExceptionChuteId { get; init; } = 999;

    /// <summary>
    /// 是否启用随机摩擦模拟
    /// </summary>
    public bool IsEnableRandomFriction { get; init; }

    /// <summary>
    /// 是否启用随机掉包模拟
    /// </summary>
    public bool IsEnableRandomDropout { get; init; }

    /// <summary>
    /// 摩擦模型配置
    /// </summary>
    public FrictionModelOptions FrictionModel { get; init; } = new();

    /// <summary>
    /// 掉包模型配置
    /// </summary>
    public DropoutModelOptions DropoutModel { get; init; } = new();

    /// <summary>
    /// 是否启用随机故障注入（已废弃，使用 FrictionModel 和 DropoutModel 代替）
    /// </summary>
    [Obsolete("使用 IsEnableRandomFriction 和 IsEnableRandomDropout 代替")]
    public bool IsEnableRandomFaultInjection { get; init; }

    /// <summary>
    /// 故障注入概率（已废弃，使用 DropoutModel.DropoutProbabilityPerSegment 代替）
    /// </summary>
    [Obsolete("使用 DropoutModel.DropoutProbabilityPerSegment 代替")]
    public double FaultInjectionProbability { get; init; } = 0.0;

    /// <summary>
    /// 是否打印详细日志
    /// </summary>
    public bool IsEnableVerboseLogging { get; init; } = true;

    /// <summary>
    /// 是否暂停等待用户按键继续
    /// </summary>
    public bool IsPauseAtEnd { get; init; } = true;
}
