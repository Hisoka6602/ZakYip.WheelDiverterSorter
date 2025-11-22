// This file is maintained for backward compatibility.
// The enum has been moved to ZakYip.WheelDiverterSorter.Core.Enums.Simulation namespace.

// Re-export the enum from Core.Enums.Simulation for backward compatibility
global using ParcelGenerationMode = ZakYip.WheelDiverterSorter.Core.Enums.Simulation.ParcelGenerationMode;

using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Simulation.Configuration;

namespace ZakYip.WheelDiverterSorter.Simulation.Scenarios;

/// <summary>
/// 仿真场景定义
/// </summary>
/// <remarks>
/// 定义一个完整的仿真场景，包括配置参数、拓扑结构、厂商驱动选择和预期验证条件。
/// 此场景定义支持序列化为 JSON/YAML，便于在不同测试环境中复用。
/// </remarks>
public record class SimulationScenario
{
    /// <summary>
    /// 场景名称
    /// </summary>
    public required string ScenarioName { get; init; }

    /// <summary>
    /// 场景描述
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// 仿真配置选项
    /// </summary>
    public required SimulationOptions Options { get; init; }

    /// <summary>
    /// 厂商驱动选择（用于测试不同厂商实现）
    /// </summary>
    /// <remarks>
    /// 默认为 Simulated，可选 Leadshine、Siemens 等。
    /// 在无硬件环境下应强制使用 Simulated。
    /// </remarks>
    public VendorId VendorId { get; init; } = VendorId.Simulated;

    /// <summary>
    /// 线体拓扑配置（节点数量、格口数量等）
    /// </summary>
    /// <remarks>
    /// 如果为 null，则使用系统默认拓扑配置。
    /// </remarks>
    public SimulationTopology? Topology { get; init; }

    /// <summary>
    /// 包裹生成配置
    /// </summary>
    public ParcelGenerationConfig? ParcelGeneration { get; init; }

    /// <summary>
    /// 故障注入配置
    /// </summary>
    public FaultInjectionConfig? FaultInjection { get; init; }

    /// <summary>
    /// 包裹期望列表（用于验证）
    /// </summary>
    /// <remarks>
    /// 如果为空，则只验证全局不变量（例如 SortedToWrongChuteCount == 0）
    /// </remarks>
    public IReadOnlyList<ParcelExpectation>? Expectations { get; init; }
}

/// <summary>
/// 仿真拓扑配置
/// </summary>
public record class SimulationTopology
{
    /// <summary>
    /// 摆轮节点数量
    /// </summary>
    public int DiverterCount { get; init; } = 5;

    /// <summary>
    /// 格口数量
    /// </summary>
    public int ChuteCount { get; init; } = 5;

    /// <summary>
    /// 线体总长度（毫米）
    /// </summary>
    public decimal TotalLineLengthMm { get; init; } = 10000;

    /// <summary>
    /// 各格口的输送带长度（毫米）
    /// </summary>
    /// <remarks>
    /// 如果为 null，则使用默认的随机长度。
    /// 键为格口ID，值为对应的输送带长度。
    /// </remarks>
    public IDictionary<int, decimal>? ChuteBeltLengths { get; init; }
}

/// <summary>
/// 包裹生成配置
/// </summary>
public record class ParcelGenerationConfig
{
    /// <summary>
    /// 生成模式
    /// </summary>
    public ParcelGenerationMode Mode { get; init; } = ParcelGenerationMode.UniformInterval;

    /// <summary>
    /// 随机种子（用于可重现的测试）
    /// </summary>
    public int? RandomSeed { get; init; }

    /// <summary>
    /// 包裹到达频率（包裹/秒）
    /// </summary>
    public decimal? ArrivalRatePerSecond { get; init; }

    /// <summary>
    /// 包裹队列长度（用于批量生成模式）
    /// </summary>
    public int? QueueLength { get; init; }
}

/// <summary>
/// 故障注入配置
/// </summary>
public record class FaultInjectionConfig
{
    /// <summary>
    /// 是否注入指令丢失故障
    /// </summary>
    public bool InjectCommandLoss { get; init; }

    /// <summary>
    /// 指令丢失概率（0.0-1.0）
    /// </summary>
    public decimal CommandLossProbability { get; init; } = 0.0m;

    /// <summary>
    /// 是否注入上游延迟故障
    /// </summary>
    public bool InjectUpstreamDelay { get; init; }

    /// <summary>
    /// 上游延迟时间范围（毫秒）
    /// </summary>
    public (int Min, int Max)? UpstreamDelayRangeMs { get; init; }

    /// <summary>
    /// 是否注入节点故障
    /// </summary>
    public bool InjectNodeFailure { get; init; }

    /// <summary>
    /// 节点故障列表（摆轮ID）
    /// </summary>
    public IReadOnlyList<int>? FailedDiverterIds { get; init; }

    /// <summary>
    /// 是否注入传感器故障
    /// </summary>
    public bool InjectSensorFailure { get; init; }

    /// <summary>
    /// 传感器故障概率（0.0-1.0）
    /// </summary>
    public decimal SensorFailureProbability { get; init; } = 0.0m;
}
