using ZakYip.WheelDiverterSorter.Core.Abstractions.Execution;

namespace ZakYip.WheelDiverterSorter.Execution.Queues;

/// <summary>
/// 队列状态信息
/// </summary>
public record class QueueStatus
{
    /// <summary>
    /// Position Index
    /// </summary>
    public required int PositionIndex { get; init; }
    
    /// <summary>
    /// 队列中任务数量
    /// </summary>
    public required int TaskCount { get; init; }
    
    /// <summary>
    /// 队列头部任务（如果有）
    /// </summary>
    public PositionQueueItem? HeadTask { get; init; }
    
    /// <summary>
    /// 最后入队时间
    /// </summary>
    public DateTime? LastEnqueueTime { get; init; }
    
    /// <summary>
    /// 最后出队时间
    /// </summary>
    public DateTime? LastDequeueTime { get; init; }
}
