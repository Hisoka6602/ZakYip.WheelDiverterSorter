using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;

namespace ZakYip.WheelDiverterSorter.Core.Abstractions.Execution;

/// <summary>
/// Position-Index 队列中的任务项
/// </summary>
/// <remarks>
/// 每个任务代表一个包裹在特定 positionIndex 上需要执行的摆轮动作。
/// 包含理论到达时间和超时阈值，用于超时检测。
/// </remarks>
public record class PositionQueueItem
{
    /// <summary>
    /// 包裹ID（长整型）
    /// </summary>
    /// <example>20251212001</example>
    public required long ParcelId { get; init; }
    
    /// <summary>
    /// 摆轮ID
    /// </summary>
    /// <example>1</example>
    public required long DiverterId { get; init; }
    
    /// <summary>
    /// 摆轮动作（Left/Right/Straight）
    /// </summary>
    public required DiverterDirection DiverterAction { get; init; }
    
    /// <summary>
    /// 期望到达时间（理论到达时间）
    /// </summary>
    /// <remarks>
    /// 基于包裹创建时间和前序线段的理论传输时间计算。
    /// 用于超时检测。
    /// </remarks>
    public required DateTime ExpectedArrivalTime { get; init; }
    
    /// <summary>
    /// 超时阈值（毫秒）
    /// </summary>
    /// <remarks>
    /// 从 ConveyorSegmentConfiguration.TimeToleranceMs 获取。
    /// 超时判定：当前时间 > ExpectedArrivalTime + TimeoutThreshold
    /// </remarks>
    public required long TimeoutThresholdMs { get; init; }
    
    /// <summary>
    /// 异常回退动作（默认 Straight）
    /// </summary>
    /// <remarks>
    /// 当检测到超时时，使用此动作代替原计划动作。
    /// 默认为 Straight 以避免阻塞后续包裹。
    /// </remarks>
    public DiverterDirection FallbackAction { get; init; } = DiverterDirection.Straight;
    
    /// <summary>
    /// 任务创建时间
    /// </summary>
    public DateTime CreatedAt { get; init; }
    
    /// <summary>
    /// Position Index（摆轮位置索引）
    /// </summary>
    /// <remarks>
    /// 对应 DiverterPathNode.PositionIndex
    /// </remarks>
    public required int PositionIndex { get; init; }
}
