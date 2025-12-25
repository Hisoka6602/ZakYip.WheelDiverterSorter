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
    
    /// <summary>
    /// 丢失判定超时时间（毫秒）
    /// </summary>
    /// <remarks>
    /// 用于主动检测包裹丢失。如果包裹在此时间内未到达检查点（传感器），判定为丢失。
    /// 计算公式：LostDetectionTimeoutMs = TimeoutThresholdMs × 1.5（基于输送线配置）
    /// 此值大于 TimeoutThresholdMs，用于区分"超时"和"丢失"两种情况。
    /// ⚠️ 注意：不再基于中位数计算，所有判断均基于输送线配置。
    /// </remarks>
    public long? LostDetectionTimeoutMs { get; init; }
    
    /// <summary>
    /// 丢失判定截止时间
    /// </summary>
    /// <remarks>
    /// = ExpectedArrivalTime + LostDetectionTimeoutMs
    /// 后台监控服务使用此字段判断包裹是否丢失
    /// </remarks>
    public DateTime? LostDetectionDeadline { get; init; }
    
    /// <summary>
    /// 最早出队时间（用于提前触发检测）
    /// </summary>
    /// <remarks>
    /// 用于防止摆轮前传感器提前触发导致的错位问题。
    /// 计算公式：EarliestDequeueTime = Max(CreatedAt, ExpectedArrivalTime - TimeoutThresholdMs)
    /// 
    /// <para>触发检测逻辑：</para>
    /// <list type="bullet">
    ///   <item>传感器触发时，先窥视队列头部任务</item>
    ///   <item>若当前时间 &lt; EarliestDequeueTime，判定为提前触发</item>
    ///   <item>提前触发时：不出队、不执行动作、仅记录告警</item>
    ///   <item>正常触发时：执行正常的出队和摆轮动作流程</item>
    /// </list>
    /// 
    /// <para>此字段为可空类型，null 表示未启用提前触发检测（向后兼容）</para>
    /// </remarks>
    public DateTime? EarliestDequeueTime { get; init; }
    
    /// <summary>
    /// 超时检测开关（是否启用包裹延迟到达的超时处理）
    /// </summary>
    /// <remarks>
    /// <para>从 ConveyorSegmentConfiguration.EnableLossDetection 获取并在任务创建时固定。</para>
    /// <para>控制范围：</para>
    /// <list type="bullet">
    ///   <item><b>启用（true）</b>：包裹延迟到达时执行超时处理（回退动作、补偿任务插入、失败统计）</item>
    ///   <item><b>禁用（false）</b>：包裹延迟到达时仍按计划动作正常执行，不做超时判定</item>
    /// </list>
    /// <para>默认值为 false（禁用超时检测），确保配置缺失时不会误判超时。</para>
    /// </remarks>
    public bool EnableTimeoutDetection { get; init; } = false;
}
