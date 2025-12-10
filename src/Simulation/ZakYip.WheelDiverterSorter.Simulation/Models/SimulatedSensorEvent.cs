namespace ZakYip.WheelDiverterSorter.Simulation.Models;

/// <summary>
/// 仿真层传感器事件模型
/// Simulated sensor event for simulation scenarios
/// PR-PERF-EVENTS01: 转换为 sealed record class 以优化性能（包含 string 引用类型）
/// </summary>
/// <remarks>
/// PR-S6: 从 SensorEvent 重命名为 SimulatedSensorEvent，与 Ingress/Models/SensorEvent 区分开来。
/// 此类型表示仿真层的模拟传感器触发事件，而非真实入口层的传感器事件。
/// </remarks>
public sealed record class SimulatedSensorEvent
{
    /// <summary>
    /// 传感器ID
    /// </summary>
    public required long SensorId { get; init; }

    /// <summary>
    /// 触发时间
    /// </summary>
    public required DateTimeOffset TriggerTime { get; init; }

    /// <summary>
    /// 段名称
    /// </summary>
    public required string SegmentName { get; init; }

    /// <summary>
    /// 摩擦因子（用于调试）
    /// </summary>
    public decimal FrictionFactor { get; init; } = 1.0m;
}
