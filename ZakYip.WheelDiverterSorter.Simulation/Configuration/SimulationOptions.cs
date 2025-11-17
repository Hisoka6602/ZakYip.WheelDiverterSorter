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

    /// <summary>
    /// 是否启用长跑模式
    /// </summary>
    /// <remarks>
    /// 长跑模式用于验证高负载、摩擦抖动、随机掉包情况下系统的稳定性
    /// </remarks>
    public bool IsLongRunMode { get; init; }

    /// <summary>
    /// 长跑持续时间（仅在长跑模式下有效）
    /// </summary>
    /// <remarks>
    /// 如果设置，仿真将在达到此持续时间后退出
    /// </remarks>
    public TimeSpan? LongRunDuration { get; init; }

    /// <summary>
    /// 长跑最大包裹数（仅在长跑模式下有效）
    /// </summary>
    /// <remarks>
    /// 如果设置，仿真将在处理此数量的包裹后退出
    /// </remarks>
    public int? MaxLongRunParcels { get; init; }

    /// <summary>
    /// 指标推送间隔（秒）
    /// </summary>
    /// <remarks>
    /// 在长跑模式下，每隔此时间输出一次统计信息
    /// </remarks>
    public int MetricsPushIntervalSeconds { get; init; } = 60;

    /// <summary>
    /// 错分时是否快速失败
    /// </summary>
    /// <remarks>
    /// 如果为 true，当检测到错分时将立即退出程序（Environment.Exit(1)）
    /// </remarks>
    public bool FailFastOnMisSort { get; init; }

    /// <summary>
    /// 最小空间安全间隔（头距，单位：mm）
    /// </summary>
    /// <remarks>
    /// 用于检测高密度包裹场景。当两件包裹在入口处的空间间隔小于此值时，
    /// 后续包裹将被视为高密度包裹，并根据 DenseParcelStrategy 进行处理。
    /// 如果为 null，则不进行空间间隔检查。
    /// </remarks>
    public decimal? MinSafeHeadwayMm { get; init; }

    /// <summary>
    /// 最小时间安全间隔（单位：毫秒）
    /// </summary>
    /// <remarks>
    /// 用于检测高密度包裹场景。当两件包裹在入口处的时间间隔小于此值时，
    /// 后续包裹将被视为高密度包裹，并根据 DenseParcelStrategy 进行处理。
    /// 如果为 null，则不进行时间间隔检查。
    /// </remarks>
    public TimeSpan? MinSafeHeadwayTime { get; init; }

    /// <summary>
    /// 高密度包裹策略：遇到间隔过近时如何处置
    /// </summary>
    /// <remarks>
    /// 默认策略为 RouteToException，将违反最小安全头距的包裹路由到异常格口。
    /// </remarks>
    public DenseParcelStrategy DenseParcelStrategy { get; init; } = DenseParcelStrategy.RouteToException;

    /// <summary>
    /// 是否启用上游动态改口仿真
    /// </summary>
    public bool IsEnableUpstreamChuteChange { get; init; }

    /// <summary>
    /// 每个包裹被发起改口请求的概率（0.0-1.0）
    /// </summary>
    public decimal UpstreamChuteChangeProbability { get; init; } = 0.0m;

    /// <summary>
    /// 改口请求最早可能时间（相对入口时间偏移）
    /// </summary>
    public TimeSpan? MinChuteChangeOffset { get; init; }

    /// <summary>
    /// 改口请求最晚可能时间（相对预计分拣完成时间偏移）
    /// </summary>
    public TimeSpan? MaxChuteChangeOffset { get; init; }

    /// <summary>
    /// 改口请求在"过晚"时的处理策略
    /// </summary>
    /// <remarks>
    /// 如果为true，过晚的改口请求会被标记为异常；
    /// 如果为false，仅记录日志但不影响正常流程
    /// </remarks>
    public bool ShouldTreatLateChangeAsException { get; init; }

    /// <summary>
    /// 传感器故障仿真配置
    /// </summary>
    /// <remarks>
    /// 用于仿真传感器故障和抖动场景
    /// </remarks>
    public SensorFaultOptions SensorFault { get; init; } = new();
}
