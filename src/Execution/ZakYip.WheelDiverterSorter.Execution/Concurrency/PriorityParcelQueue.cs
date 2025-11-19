using System.Collections.Concurrent;
using System.Threading.Channels;

namespace ZakYip.WheelDiverterSorter.Execution.Concurrency;

/// <summary>
/// 优先级包裹队列实现
/// </summary>
/// <remarks>
/// 使用Channel实现线程安全的优先级队列，支持批量处理相同目标的包裹
/// </remarks>
public class PriorityParcelQueue : IParcelQueue
{
    private readonly Channel<ParcelQueueItem> _channel;
    private readonly SemaphoreSlim _semaphore;
    private int _count;

    /// <summary>
    /// 初始化优先级包裹队列
    /// </summary>
    /// <param name="capacity">队列容量，默认无限制</param>
    public PriorityParcelQueue(int capacity = -1)
    {
        var options = capacity > 0
            ? new BoundedChannelOptions(capacity)
            {
                FullMode = BoundedChannelFullMode.Wait
            }
            : new UnboundedChannelOptions() as ChannelOptions;

        _channel = Channel.CreateUnbounded<ParcelQueueItem>(new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = false
        });

        _semaphore = new SemaphoreSlim(0);
        _count = 0;
    }

    /// <inheritdoc/>
    public int Count => _count;

    /// <inheritdoc/>
    public async Task EnqueueAsync(ParcelQueueItem item, CancellationToken cancellationToken = default)
    {
        if (item == null)
        {
            throw new ArgumentNullException(nameof(item));
        }

        await _channel.Writer.WriteAsync(item, cancellationToken).ConfigureAwait(false);
        Interlocked.Increment(ref _count);
        _semaphore.Release();
    }

    /// <inheritdoc/>
    public async Task<ParcelQueueItem> DequeueAsync(CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        
        if (_channel.Reader.TryRead(out var item))
        {
            Interlocked.Decrement(ref _count);
            return item;
        }

        // 不应该到达这里，因为信号量保证了有元素
        throw new InvalidOperationException("队列状态异常");
    }

    /// <inheritdoc/>
    public bool TryDequeue(out ParcelQueueItem? item)
    {
        if (_channel.Reader.TryRead(out item))
        {
            Interlocked.Decrement(ref _count);
            _semaphore.Wait(0); // 减少信号量计数
            return true;
        }

        item = null;
        return false;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ParcelQueueItem>> DequeueBatchAsync(
        int maxBatchSize,
        CancellationToken cancellationToken = default)
    {
        if (maxBatchSize <= 0)
        {
            throw new ArgumentException("批次大小必须大于0", nameof(maxBatchSize));
        }

        var batch = new List<ParcelQueueItem>();

        // 等待至少有一个元素
        var firstItem = await DequeueAsync(cancellationToken).ConfigureAwait(false);
        batch.Add(firstItem);

        var targetChute = firstItem.TargetChuteId;

        // 尝试获取更多相同目标的包裹
        while (batch.Count < maxBatchSize)
        {
            if (TryDequeue(out var item) && item != null)
            {
                // 如果目标格口相同，加入批次
                if (item.TargetChuteId == targetChute)
                {
                    batch.Add(item);
                }
                else
                {
                    // 目标格口不同，放回队列
                    await EnqueueAsync(item, cancellationToken).ConfigureAwait(false);
                    break;
                }
            }
            else
            {
                // 队列已空
                break;
            }
        }

        return batch.AsReadOnly();
    }
}
