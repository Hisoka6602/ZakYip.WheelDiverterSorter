// This file is maintained for backward compatibility.
// The enum has been moved to ZakYip.WheelDiverterSorter.Core.Enums.Simulation namespace.

// Re-export the enum from Core.Enums.Simulation for backward compatibility
global using IoBehaviorMode = ZakYip.WheelDiverterSorter.Core.Enums.IoBehaviorMode;

namespace ZakYip.WheelDiverterSorter.Simulation.Configuration;

/// <summary>
/// 传感器故障仿真配置选项
/// </summary>
/// <remarks>
/// 用于仿真传感器故障和抖动场景
/// PR-40: 增强 IO 行为模型，支持理想模式和混沌模式
/// </remarks>
public sealed record class SensorFaultOptions
{
    /// <summary>
    /// 是否启用摆轮前传感器故障仿真
    /// </summary>
    /// <remarks>
    /// 当启用时，摆轮前传感器将不产生任何触发事件
    /// </remarks>
    public bool IsPreDiverterSensorFault { get; init; }

    /// <summary>
    /// 传感器故障开始时间偏移（相对仿真开始时间）
    /// </summary>
    /// <remarks>
    /// 如果为 null，则从仿真开始即处于故障状态
    /// </remarks>
    public TimeSpan? FaultStartOffset { get; init; }

    /// <summary>
    /// 传感器故障持续时间
    /// </summary>
    /// <remarks>
    /// 如果为 null，则故障持续到仿真结束
    /// </remarks>
    public TimeSpan? FaultDuration { get; init; }

    /// <summary>
    /// 是否启用传感器抖动仿真
    /// </summary>
    /// <remarks>
    /// 当启用时，传感器将在短时间内多次触发
    /// </remarks>
    public bool IsEnableSensorJitter { get; init; }

    /// <summary>
    /// 传感器抖动触发次数
    /// </summary>
    /// <remarks>
    /// 在抖动间隔内触发的次数（包括正常触发）
    /// </remarks>
    public int JitterTriggerCount { get; init; } = 3;

    /// <summary>
    /// 传感器抖动间隔时间（毫秒）
    /// </summary>
    /// <remarks>
    /// 在此时间内产生多次触发，默认50ms
    /// </remarks>
    public int JitterIntervalMs { get; init; } = 50;

    /// <summary>
    /// 抖动概率（0.0-1.0）
    /// </summary>
    /// <remarks>
    /// 每个包裹触发传感器时发生抖动的概率，默认0表示不随机抖动
    /// </remarks>
    public decimal JitterProbability { get; init; } = 0.0m;

    /// <summary>
    /// IO 行为模式
    /// </summary>
    /// <remarks>
    /// PR-40: 支持理想模式和混沌模式
    /// </remarks>
    public IoBehaviorMode BehaviorMode { get; init; } = IoBehaviorMode.Ideal;

    /// <summary>
    /// 传感器延迟范围（毫秒）
    /// </summary>
    /// <remarks>
    /// PR-40: 混沌模式下，传感器响应的随机延迟范围
    /// </remarks>
    public (int Min, int Max)? SensorDelayRangeMs { get; init; }

    /// <summary>
    /// 传感器丢失概率（0.0-1.0）
    /// </summary>
    /// <remarks>
    /// PR-40: 混沌模式下，传感器事件偶发丢失的概率
    /// </remarks>
    public decimal SensorLossProbability { get; init; } = 0.0m;

    /// <summary>
    /// 是否启用去抖策略
    /// </summary>
    /// <remarks>
    /// PR-40: 用于验证去抖逻辑的有效性
    /// </remarks>
    public bool IsEnableDebouncing { get; init; } = true;

    /// <summary>
    /// 去抖时间窗口（毫秒）
    /// </summary>
    /// <remarks>
    /// PR-40: 在此时间窗口内的连续触发将被视为单次触发
    /// </remarks>
    public int DebounceWindowMs { get; init; } = 100;
}

