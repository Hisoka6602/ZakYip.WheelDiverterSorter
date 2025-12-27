using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Execution;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;
using ZakYip.WheelDiverterSorter.Execution.Tracking;

namespace ZakYip.WheelDiverterSorter.Execution.Queues;

/// <summary>
/// Position-Index 队列管理器实现
/// </summary>
/// <remarks>
/// 使用 ConcurrentDictionary 和 ConcurrentQueue 确保线程安全。
/// 每个 positionIndex 有独立的 FIFO 队列，避免全局锁竞争。
/// 
/// <para><b>并发控制（PR-race-condition-fix）</b>：</para>
/// 使用细粒度锁（per-Position locks）保护队列操作，确保"清空→修改→放回"等复合操作的原子性。
/// 这解决了以下竞态条件：
/// <list type="bullet">
///   <item>与 DequeueTask 的竞态：防止队列清空时传感器触发返回null</item>
///   <item>与 EnqueueTask 的竞态：防止FIFO顺序被破坏</item>
///   <item>多个丢失事件竞态：防止同时处理多个丢失时漏修改任务</item>
/// </list>
/// </remarks>
public class PositionIndexQueueManager : IPositionIndexQueueManager
{
    private readonly ConcurrentDictionary<int, ConcurrentQueue<PositionQueueItem>> _queues = new();
    private readonly ConcurrentDictionary<int, DateTime> _lastEnqueueTimes = new();
    private readonly ConcurrentDictionary<int, DateTime> _lastDequeueTimes = new();
    
    // PR-race-condition-fix: 细粒度锁，每个Position独立，避免全局锁竞争
    private readonly ConcurrentDictionary<int, object> _queueLocks = new();
    
    private readonly ILogger<PositionIndexQueueManager> _logger;
    private readonly ISystemClock _clock;
    private readonly IPositionIntervalTracker? _intervalTracker;
    private readonly IParcelLossDetectionConfigurationRepository? _configRepository;

    public PositionIndexQueueManager(
        ILogger<PositionIndexQueueManager> logger,
        ISystemClock clock,
        IPositionIntervalTracker? intervalTracker = null,
        IParcelLossDetectionConfigurationRepository? configRepository = null)
    {
        _logger = logger;
        _clock = clock;
        _intervalTracker = intervalTracker;
        _configRepository = configRepository;
    }

    /// <inheritdoc/>
    public void EnqueueTask(int positionIndex, PositionQueueItem task)
    {
        // 应用丢失检测阈值（基于输送线配置）
        task = ApplyDynamicLostDetectionDeadline(task, positionIndex);
        
        var queueLock = _queueLocks.GetOrAdd(positionIndex, _ => new object());
        var queue = _queues.GetOrAdd(positionIndex, _ => new ConcurrentQueue<PositionQueueItem>());
        
        // PR-race-condition-fix: 使用锁保护，防止与 UpdateAffectedParcelsToStraight 竞态
        lock (queueLock)
        {
            queue.Enqueue(task);
            _lastEnqueueTimes[positionIndex] = _clock.LocalNow;
        }

        _logger.LogDebug(
            "任务已加入 Position {PositionIndex} 队列尾部: ParcelId={ParcelId}, DiverterId={DiverterId}, Action={Action}, QueueCount={QueueCount}",
            positionIndex, task.ParcelId, task.DiverterId, task.DiverterAction, queue.Count);
    }

    /// <inheritdoc/>
    public void EnqueuePriorityTask(int positionIndex, PositionQueueItem task)
    {
        // 应用丢失检测阈值（基于输送线配置）
        task = ApplyDynamicLostDetectionDeadline(task, positionIndex);
        
        var queueLock = _queueLocks.GetOrAdd(positionIndex, _ => new object());
        var queue = _queues.GetOrAdd(positionIndex, _ => new ConcurrentQueue<PositionQueueItem>());
        
        // PR-race-condition-fix: 使用锁保护整个重建队列过程
        lock (queueLock)
        {
            // ConcurrentQueue 不支持直接插入头部，需要重建队列
            // 1. 取出所有现有任务
            var existingTasks = new List<PositionQueueItem>();
            while (queue.TryDequeue(out var existingTask))
            {
                existingTasks.Add(existingTask);
            }
            
            // 2. 先加入优先任务
            queue.Enqueue(task);
            
            // 3. 再加入原有任务
            foreach (var existingTask in existingTasks)
            {
                queue.Enqueue(existingTask);
            }
            
            _lastEnqueueTimes[positionIndex] = _clock.LocalNow;
        }

        _logger.LogWarning(
            "优先任务已插入 Position {PositionIndex} 队列头部: ParcelId={ParcelId}, DiverterId={DiverterId}, Action={Action}, QueueCount={QueueCount}",
            positionIndex, task.ParcelId, task.DiverterId, task.DiverterAction, queue.Count);
    }

    /// <inheritdoc/>
    public PositionQueueItem? DequeueTask(int positionIndex)
    {
        if (!_queues.TryGetValue(positionIndex, out var queue))
        {
            _logger.LogWarning("Position {PositionIndex} 队列不存在，无法取出任务", positionIndex);
            return null;
        }

        var queueLock = _queueLocks.GetOrAdd(positionIndex, _ => new object());
        
        // PR-race-condition-fix: 使用锁保护，防止与 UpdateAffectedParcelsToStraight 竞态
        lock (queueLock)
        {
            if (queue.TryDequeue(out var task))
            {
                _lastDequeueTimes[positionIndex] = _clock.LocalNow;

                _logger.LogDebug(
                    "任务已从 Position {PositionIndex} 队列取出: ParcelId={ParcelId}, DiverterId={DiverterId}, Action={Action}, RemainingCount={RemainingCount}",
                    positionIndex, task.ParcelId, task.DiverterId, task.DiverterAction, queue.Count);

                return task;
            }
        }

        _logger.LogDebug("Position {PositionIndex} 队列为空，无任务可取", positionIndex);
        return null;
    }

    /// <inheritdoc/>
    public PositionQueueItem? PeekTask(int positionIndex)
    {
        if (!_queues.TryGetValue(positionIndex, out var queue))
        {
            return null;
        }

        queue.TryPeek(out var task);
        return task;
    }

    /// <inheritdoc/>
    public PositionQueueItem? PeekNextTask(int positionIndex)
    {
        if (!_queues.TryGetValue(positionIndex, out var queue))
        {
            return null;
        }

        // ConcurrentQueue 不支持直接访问第二个元素
        // 需要转换为数组后访问
        var tasks = queue.ToArray();
        return tasks.Length >= 2 ? tasks[1] : null;
    }

    /// <inheritdoc/>
    public void ClearAllQueues()
    {
        var clearedCount = 0;
        var positionsCleaned = new List<int>();

        foreach (var (positionIndex, queue) in _queues)
        {
            var queueLock = _queueLocks.GetOrAdd(positionIndex, _ => new object());
            
            // PR-race-condition-fix: 使用锁保护队列清空操作
            lock (queueLock)
            {
                var count = queue.Count;
                
                // 清空队列
                while (queue.TryDequeue(out _))
                {
                    clearedCount++;
                }

                if (count > 0)
                {
                    positionsCleaned.Add(positionIndex);
                    _logger.LogDebug(
                        "Position {PositionIndex} 队列已清空，移除 {Count} 个任务",
                        positionIndex, count);
                }
            }
        }

        // 清空时间记录
        _lastEnqueueTimes.Clear();
        _lastDequeueTimes.Clear();

        _logger.LogInformation(
            "所有队列已清空，总计移除 {TotalCount} 个任务，涉及 {PositionCount} 个 Position: [{Positions}]",
            clearedCount, positionsCleaned.Count, string.Join(", ", positionsCleaned));
    }

    /// <inheritdoc/>
    public QueueStatus GetQueueStatus(int positionIndex)
    {
        var queue = _queues.GetOrAdd(positionIndex, _ => new ConcurrentQueue<PositionQueueItem>());
        
        queue.TryPeek(out var headTask);
        
        _lastEnqueueTimes.TryGetValue(positionIndex, out var lastEnqueue);
        _lastDequeueTimes.TryGetValue(positionIndex, out var lastDequeue);

        return new QueueStatus
        {
            PositionIndex = positionIndex,
            TaskCount = queue.Count,
            HeadTask = headTask,
            LastEnqueueTime = lastEnqueue == default ? null : lastEnqueue,
            LastDequeueTime = lastDequeue == default ? null : lastDequeue
        };
    }

    /// <inheritdoc/>
    public Dictionary<int, QueueStatus> GetAllQueueStatuses()
    {
        var statuses = new Dictionary<int, QueueStatus>();

        foreach (var positionIndex in _queues.Keys)
        {
            statuses[positionIndex] = GetQueueStatus(positionIndex);
        }

        return statuses;
    }

    /// <inheritdoc/>
    public int GetQueueCount(int positionIndex)
    {
        if (!_queues.TryGetValue(positionIndex, out var queue))
        {
            return 0;
        }

        return queue.Count;
    }

    /// <inheritdoc/>
    public bool IsQueueEmpty(int positionIndex)
    {
        if (!_queues.TryGetValue(positionIndex, out var queue))
        {
            return true;
        }

        return queue.IsEmpty;
    }
    
    /// <inheritdoc/>
    public int RemoveAllTasksForParcel(long parcelId)
    {
        int totalRemoved = 0;
        var affectedPositions = new List<int>();

        foreach (var (positionIndex, queue) in _queues)
        {
            var queueLock = _queueLocks.GetOrAdd(positionIndex, _ => new object());
            
            // PR-race-condition-fix: 使用锁保护整个"清空→过滤→放回"操作
            lock (queueLock)
            {
                // 临时存储不属于该包裹的任务
                var remainingTasks = new List<PositionQueueItem>();
                int removedFromThisQueue = 0;

                // 遍历队列，过滤掉指定包裹的任务
                while (queue.TryDequeue(out var task))
                {
                    if (task.ParcelId == parcelId)
                    {
                        removedFromThisQueue++;
                        totalRemoved++;
                    }
                    else
                    {
                        remainingTasks.Add(task);
                    }
                }

                // 将保留的任务放回队列
                foreach (var task in remainingTasks)
                {
                    queue.Enqueue(task);
                }

                if (removedFromThisQueue > 0)
                {
                    affectedPositions.Add(positionIndex);
                    _logger.LogDebug(
                        "[包裹丢失清理] Position {PositionIndex} 移除了包裹 {ParcelId} 的 {Count} 个任务",
                        positionIndex, parcelId, removedFromThisQueue);
                }
            }
        }

        if (totalRemoved > 0)
        {
            _logger.LogWarning(
                "[包裹丢失清理] 已从所有队列移除包裹 {ParcelId} 的共 {TotalCount} 个任务，" +
                "涉及 {PositionCount} 个 Position: [{Positions}]",
                parcelId, totalRemoved, affectedPositions.Count, string.Join(", ", affectedPositions));
        }
        else
        {
            _logger.LogDebug(
                "[包裹丢失清理] 未找到包裹 {ParcelId} 的任务（可能已执行完成）",
                parcelId);
        }

        return totalRemoved;
    }
    
    /// <inheritdoc/>
    public List<long> UpdateAffectedParcelsToStraight(DateTime lostParcelCreatedAt, DateTime detectionTime)
    {
        // 使用 HashSet 提高去重性能 O(1) vs O(n)
        var affectedParcelIdsSet = new HashSet<long>();
        var affectedPositions = new Dictionary<int, int>(); // positionIndex -> 修改数量

        foreach (var (positionIndex, queue) in _queues)
        {
            var queueLock = _queueLocks.GetOrAdd(positionIndex, _ => new object());
            
            // PR-race-condition-fix: 使用锁保护整个"清空→修改→放回"操作
            // 这是最关键的修复点，解决与 EnqueueTask/DequeueTask 的竞态条件
            lock (queueLock)
            {
                // 临时存储队列中的任务
                var tempTasks = new List<PositionQueueItem>();
                int modifiedCount = 0;

                // 遍历队列，识别并修改受影响的包裹任务
                while (queue.TryDequeue(out var task))
                {
                    // 判断该包裹是否受影响：
                    // 1. 包裹创建时间在丢失包裹创建时间之后
                    // 2. 包裹创建时间在丢失检测时间之前或等于检测时间
                    if (task.CreatedAt > lostParcelCreatedAt && task.CreatedAt <= detectionTime)
                    {
                        // 如果任务方向不是直行，则修改为直行
                        if (task.DiverterAction != DiverterDirection.Straight)
                        {
                            // 创建新任务，方向改为直行
                            // 同时清除丢失检测截止时间，防止误判为丢失
                            var modifiedTask = task with 
                            { 
                                DiverterAction = DiverterDirection.Straight,
                                LostDetectionDeadline = null,  // 清除丢失检测截止时间
                                LostDetectionTimeoutMs = null  // 清除丢失检测超时时间
                            };
                            tempTasks.Add(modifiedTask);
                            
                            // 记录受影响的包裹ID（去重）- O(1) 性能
                            affectedParcelIdsSet.Add(task.ParcelId);
                            
                            modifiedCount++;
                            
                            _logger.LogDebug(
                                "[包裹丢失影响] Position {PositionIndex} 包裹 {ParcelId} 的任务方向从 {OldAction} 改为 Straight，已清除丢失检测截止时间",
                                positionIndex, task.ParcelId, task.DiverterAction);
                        }
                        else
                        {
                            // 已经是直行，但仍需清除丢失检测截止时间（因为受到影响）
                            var modifiedTask = task with 
                            { 
                                LostDetectionDeadline = null,
                                LostDetectionTimeoutMs = null
                            };
                            tempTasks.Add(modifiedTask);
                            
                            // 即使是直行，也要记录为受影响（时间可能需要调整）
                            affectedParcelIdsSet.Add(task.ParcelId);
                            modifiedCount++;
                            
                            _logger.LogDebug(
                                "[包裹丢失影响] Position {PositionIndex} 包裹 {ParcelId} 已是直行，但清除丢失检测截止时间",
                                positionIndex, task.ParcelId);
                        }
                    }
                    else
                    {
                        // 不受影响的任务保持原样
                        tempTasks.Add(task);
                    }
                }

                // 将所有任务放回队列（保持原顺序）
                foreach (var task in tempTasks)
                {
                    queue.Enqueue(task);
                }

                if (modifiedCount > 0)
                {
                    affectedPositions[positionIndex] = modifiedCount;
                }
            }
        }

        if (affectedParcelIdsSet.Count > 0)
        {
            _logger.LogWarning(
                "[包裹丢失影响] 共 {ParcelCount} 个包裹受影响，任务方向已改为直行: [{ParcelIds}]，" +
                "涉及 {PositionCount} 个 Position: {Positions}",
                affectedParcelIdsSet.Count,
                string.Join(", ", affectedParcelIdsSet),
                affectedPositions.Count,
                string.Join(", ", affectedPositions.Select(kvp => $"P{kvp.Key}({kvp.Value}个任务)")));
        }
        else
        {
            _logger.LogDebug("[包裹丢失影响] 无其他包裹受影响");
        }

        // 转换为 List 返回
        return affectedParcelIdsSet.ToList();
    }

    /// <summary>
    /// 应用丢失检测截止时间（基于输送线配置）
    /// </summary>
    /// <param name="task">原始任务</param>
    /// <param name="positionIndex">位置索引</param>
    /// <returns>更新后的任务</returns>
    /// <remarks>
    /// <para>首先检查配置是否启用包裹丢失检测（IsEnabled）。</para>
    /// <para>如果禁用，则清除任务的所有丢失检测字段，阻止任何形式的丢失判定。</para>
    /// <para>如果启用，则使用任务中的 TimeoutThresholdMs（来自 ConveyorSegmentConfiguration.TimeToleranceMs）。</para>
    /// <para>丢失检测阈值 = TimeoutThresholdMs × 1.5，用于区分超时和丢失两种情况。</para>
    /// <para>⚠️ 重要：不再使用中位数计算，所有判断均基于输送线配置。</para>
    /// </remarks>
    private PositionQueueItem ApplyDynamicLostDetectionDeadline(PositionQueueItem task, int positionIndex)
    {
        // 检查是否启用了包裹丢失检测
        bool isDetectionEnabled = true; // 默认启用（安全策略）
        if (_configRepository != null)
        {
            try
            {
                var config = _configRepository.Get();
                isDetectionEnabled = config.IsEnabled;
            }
            catch (Exception ex)
            {
                // 配置读取失败，使用默认值（启用）
                _logger.LogWarning(ex, "读取包裹丢失检测配置失败，使用默认值（启用）");
            }
        }
        
        // 如果检测被禁用，清除所有丢失检测相关字段
        if (!isDetectionEnabled)
        {
            _logger.LogDebug(
                "[检测已禁用] Position {PositionIndex} 包裹 {ParcelId}: 包裹丢失检测已禁用，清除丢失判定字段",
                positionIndex, task.ParcelId);
            
            return task with
            {
                LostDetectionTimeoutMs = null,
                LostDetectionDeadline = null
            };
        }
        
        // 检测已启用，使用输送线配置（TimeoutThresholdMs）计算丢失检测阈值
        // 如果任务已有 ExpectedArrivalTime 和 TimeoutThresholdMs，计算丢失检测截止时间
        if (task.ExpectedArrivalTime != default && task.TimeoutThresholdMs > 0)
        {
            // 使用 TimeoutThresholdMs × 1.5 作为丢失检测阈值
            // 这确保丢失判定比超时判定更宽松，用于区分"超时"和"物理丢失"两种情况
            var lostDetectionThreshold = task.TimeoutThresholdMs * 1.5;
            var newDeadline = task.ExpectedArrivalTime.AddMilliseconds(lostDetectionThreshold);
            
            _logger.LogDebug(
                "[配置阈值] Position {PositionIndex} 包裹 {ParcelId}: 使用输送线配置阈值 {ThresholdMs}ms (TimeoutThreshold × 1.5)，截止时间 {Deadline:HH:mm:ss.fff}",
                positionIndex, task.ParcelId, lostDetectionThreshold, newDeadline);
            
            return task with
            {
                LostDetectionTimeoutMs = (long)lostDetectionThreshold,
                LostDetectionDeadline = newDeadline
            };
        }
        
        // 如果无法计算（缺少必要字段），返回原任务
        return task;
    }
}
