namespace ZakYip.WheelDiverterSorter.Execution.Concurrency;

/// <summary>
/// 包裹队列接口
/// </summary>
/// <remarks>
/// 支持优先级排序的包裹队列，用于管理待分拣包裹
/// </remarks>
public interface IParcelQueue
{
    /// <summary>
    /// 当前队列中的包裹数量
    /// </summary>
    int Count { get; }

    /// <summary>
    /// 将包裹入队
    /// </summary>
    /// <param name="item">包裹队列项</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task EnqueueAsync(ParcelQueueItem item, CancellationToken cancellationToken = default);

    /// <summary>
    /// 从队列中取出包裹
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>包裹队列项，如果队列为空则等待</returns>
    Task<ParcelQueueItem> DequeueAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 尝试从队列中取出包裹（非阻塞）
    /// </summary>
    /// <param name="item">输出的包裹队列项</param>
    /// <returns>如果成功取出返回true，否则返回false</returns>
    bool TryDequeue(out ParcelQueueItem? item);

    /// <summary>
    /// 批量获取具有相同目标格口的包裹
    /// </summary>
    /// <param name="maxBatchSize">最大批次大小</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>相同目标格口的包裹列表</returns>
    Task<IReadOnlyList<ParcelQueueItem>> DequeueBatchAsync(int maxBatchSize, CancellationToken cancellationToken = default);
}
