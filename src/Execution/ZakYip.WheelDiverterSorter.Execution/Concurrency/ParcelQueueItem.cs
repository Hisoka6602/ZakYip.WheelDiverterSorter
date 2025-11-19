namespace ZakYip.WheelDiverterSorter.Execution.Concurrency;

/// <summary>
/// 包裹队列项
/// </summary>
public class ParcelQueueItem
{
    /// <summary>
    /// 包裹ID
    /// </summary>
    public required string ParcelId { get; init; }

    /// <summary>
    /// 目标格口ID
    /// </summary>
    public required string TargetChuteId { get; init; }

    /// <summary>
    /// 优先级（数值越小优先级越高）
    /// </summary>
    public int Priority { get; init; } = 0;

    /// <summary>
    /// 入队时间
    /// </summary>
    public DateTimeOffset EnqueuedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 任务完成源，用于通知结果
    /// </summary>
    public TaskCompletionSource<string>? CompletionSource { get; init; }
}
