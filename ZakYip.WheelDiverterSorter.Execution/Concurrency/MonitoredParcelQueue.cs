using ZakYip.WheelDiverterSorter.Observability;

namespace ZakYip.WheelDiverterSorter.Execution.Concurrency;

/// <summary>
/// 包裹队列监控装饰器
/// Monitors queue length and reports to AlarmService
/// </summary>
public class MonitoredParcelQueue : IParcelQueue
{
    private readonly IParcelQueue _innerQueue;
    private readonly AlarmService _alarmService;

    public MonitoredParcelQueue(IParcelQueue innerQueue, AlarmService alarmService)
    {
        _innerQueue = innerQueue ?? throw new ArgumentNullException(nameof(innerQueue));
        _alarmService = alarmService ?? throw new ArgumentNullException(nameof(alarmService));
    }

    public int Count => _innerQueue.Count;

    public async Task EnqueueAsync(ParcelQueueItem item, CancellationToken cancellationToken = default)
    {
        await _innerQueue.EnqueueAsync(item, cancellationToken);
        _alarmService.UpdateQueueLength(_innerQueue.Count);
    }

    public async Task<ParcelQueueItem> DequeueAsync(CancellationToken cancellationToken = default)
    {
        var item = await _innerQueue.DequeueAsync(cancellationToken);
        _alarmService.UpdateQueueLength(_innerQueue.Count);
        return item;
    }

    public bool TryDequeue(out ParcelQueueItem? item)
    {
        var result = _innerQueue.TryDequeue(out item);
        if (result)
        {
            _alarmService.UpdateQueueLength(_innerQueue.Count);
        }
        return result;
    }

    public async Task<IReadOnlyList<ParcelQueueItem>> DequeueBatchAsync(
        int maxBatchSize,
        CancellationToken cancellationToken = default)
    {
        var batch = await _innerQueue.DequeueBatchAsync(maxBatchSize, cancellationToken);
        _alarmService.UpdateQueueLength(_innerQueue.Count);
        return batch;
    }
}
