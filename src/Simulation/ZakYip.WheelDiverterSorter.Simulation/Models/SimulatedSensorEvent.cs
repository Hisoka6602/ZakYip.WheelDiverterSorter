namespace ZakYip.WheelDiverterSorter.Simulation.Models;

/// <summary>
/// 仿真层传感器事件模型
/// Simulated sensor event for simulation scenarios
/// </summary>
/// <remarks>
/// PR-S6: 从 SensorEvent 重命名为 SimulatedSensorEvent，与 Ingress/Models/SensorEvent 区分开来。
/// 此类型表示仿真层的模拟传感器触发事件，而非真实入口层的传感器事件。
/// </remarks>
public class SimulatedSensorEvent
{
    /// <summary>
    /// 传感器ID
    /// </summary>
    public required string SensorId { get; set; }

    /// <summary>
    /// 触发时间
    /// </summary>
    public DateTimeOffset TriggerTime { get; set; }

    /// <summary>
    /// 段名称
    /// </summary>
    public required string SegmentName { get; set; }

    /// <summary>
    /// 摩擦因子（用于调试）
    /// </summary>
    public decimal FrictionFactor { get; set; } = 1.0m;
}
