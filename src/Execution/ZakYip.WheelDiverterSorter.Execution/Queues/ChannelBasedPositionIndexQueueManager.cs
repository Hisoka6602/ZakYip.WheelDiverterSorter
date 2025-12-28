using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Execution;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Execution.Tracking;

namespace ZakYip.WheelDiverterSorter.Execution.Queues;

/// <summary>
/// Channel-based Position-Index 队列管理器实现
/// </summary>
/// <remarks>
/// <para><b>架构设计（逻辑删除 + 消费端跳过）</b>：</para>
/// <list type="bullet">
///   <item><b>Channel&lt;long&gt;</b>：存储包裹 ParcelId，维护 FIFO 顺序（按第一次入队顺序）</item>
///   <item><b>ConcurrentDictionary&lt;long, PositionQueueItem&gt;</b>：存储每个包裹的最新任务数据（支持高频更新）</item>
///   <item><b>HashSet&lt;long&gt;</b>：追踪已入队的包裹ID，防止重复入队</item>
/// </list>
/// 
/// <para><b>核心优势</b>：</para>
/// <list type="bullet">
///   <item>Channel 在生产者/消费者模型下比手动信号量+ConcurrentQueue性能更好</item>
///   <item>支持有界队列做背压控制，避免内存失控</item>
///   <item>ConcurrentDictionary 支持无锁并发更新（同一包裹的任务可以被多次更新，不影响 FIFO 顺序）</item>
///   <item>分离顺序管理和数据存储，避免"清空→过滤→重建"的复杂锁竞争</item>
/// </list>
/// 
/// <para><b>逻辑删除机制（O(1) 性能，不破坏 FIFO）</b>：</para>
/// <list type="bullet">
///   <item><b>删除动作</b>：ConcurrentDictionary.TryRemove(key) 标记元素失效，不从 Channel 中移除</item>
///   <item><b>消费动作</b>：Channel.ReadAsync() 获取 key → TryRemove(key, out value) 成功则处理，失败则跳过</item>
///   <item><b>优势</b>：删除是 O(1)，不需要遍历或重建 Channel，吞吐量最高</item>
/// </list>
/// 
/// <para><b>并发控制</b>：</para>
/// <list type="bullet">
///   <item>Channel 自带线程安全保证 FIFO 顺序</item>
///   <item>ConcurrentDictionary 支持无锁并发读写</item>
///   <item>EnqueueTask：仅在首次入队时写入 Channel，后续更新仅修改 Dictionary</item>
///   <item>DequeueTask：Channel.ReadAsync（取ID）+ Dictionary.TryRemove（移除数据，失败则跳过已取消的任务）</item>
///   <item>ReplaceTasksInPlace：仅更新 Dictionary，不影响 Channel 中的 FIFO 顺序</item>
///   <item>RemoveAllTasksForParcel：逻辑删除（仅从 Dictionary 移除），消费端自动跳过</item>
/// </list>
/// 
/// <para><b>性能保证（&lt; 1ms 执行时间）</b>：</para>
/// <list type="bullet">
///   <item>EnqueueTask: O(1) - Channel.TryWrite + Dictionary赋值</item>
///   <item>DequeueTask: O(k) where k = 已删除任务数（通常很小，因为消费端会清理）</item>
///   <item>PeekTask: O(k) where k = 已删除任务数（跳过删除的任务找到第一个有效任务）</item>
///   <item>TryUpdateTask: O(1) - Dictionary.TryUpdate 原子操作</item>
///   <item>RemoveAllTasksForParcel: O(p) where p = Position数量（通常 &lt; 20）</item>
///   <item>⚠️ ClearAllQueues: O(p) - 仅在面板控制时调用，非热路径</item>
/// </list>
/// </remarks>
public class ChannelBasedPositionIndexQueueManager : IPositionIndexQueueManager
{
    // 每个 Position 的 Channel（存储包裹ID的FIFO队列）
    private readonly ConcurrentDictionary<int, Channel<long>> _orderChannels = new();
    
    // 每个 Position 的任务数据字典（ParcelId -> PositionQueueItem）
    private readonly ConcurrentDictionary<int, ConcurrentDictionary<long, PositionQueueItem>> _taskData = new();
    
    // 每个 Position 的已入队包裹ID集合（用于防止重复入队）
    private readonly ConcurrentDictionary<int, HashSet<long>> _queuedSets = new();
    
    // 每个 Position 的包裹ID顺序列表（用于 Peek 操作，避免 Channel 重建）
    // 使用 LinkedList 替代 List 实现 O(1) 头部删除
    private readonly ConcurrentDictionary<int, LinkedList<long>> _orderLists = new();
    
    // 每个 Position 的锁（用于保护 HashSet 和 List 的同步操作）
    private readonly ConcurrentDictionary<int, object> _positionLocks = new();
    
    // 时间戳记录
    private readonly ConcurrentDictionary<int, DateTime> _lastEnqueueTimes = new();
    private readonly ConcurrentDictionary<int, DateTime> _lastDequeueTimes = new();
    
    private readonly ILogger<ChannelBasedPositionIndexQueueManager> _logger;
    private readonly ISystemClock _clock;
    private readonly IPositionIntervalTracker? _intervalTracker;

    /// <summary>
    /// Channel 配置选项
    /// </summary>
    private readonly UnboundedChannelOptions _channelOptions = new()
    {
        // 单读单写模式（每个Position的Channel只有一个生产者和一个消费者）
        SingleReader = true,
        SingleWriter = false  // 可能有多个并发的EnqueueTask调用
    };

    public ChannelBasedPositionIndexQueueManager(
        ILogger<ChannelBasedPositionIndexQueueManager> logger,
        ISystemClock clock,
        IPositionIntervalTracker? intervalTracker = null)
    {
        _logger = logger;
        _clock = clock;
        _intervalTracker = intervalTracker;
    }

    /// <inheritdoc/>
    public void EnqueueTask(int positionIndex, PositionQueueItem task)
    {
        var positionLock = GetOrCreateLock(positionIndex);
        
        lock (positionLock)
        {
            // 1. 获取或创建数据结构
            var channel = GetOrCreateChannel(positionIndex);
            var taskDict = GetOrCreateTaskDictionary(positionIndex);
            var queuedSet = GetOrCreateQueuedSet(positionIndex);
            var orderList = GetOrCreateOrderList(positionIndex);
            
            // 2. 检查是否已入队
            var isNewParcel = !queuedSet.Contains(task.ParcelId);
            
            // 3. 存储/更新任务数据（无论是否新包裹都更新）
            taskDict[task.ParcelId] = task;
            
            // 4. 仅在首次入队时将包裹ID加入 Channel、Set 和 List
            if (isNewParcel)
            {
                // 加入 Channel（FIFO 队列）
                if (!channel.Writer.TryWrite(task.ParcelId))
                {
                    _logger.LogError(
                        "无法将包裹 {ParcelId} 加入 Position {PositionIndex} 的 Channel（不应发生）",
                        task.ParcelId, positionIndex);
                    taskDict.TryRemove(task.ParcelId, out _);
                    return;
                }
                
                // 标记已入队
                queuedSet.Add(task.ParcelId);
                
                // 加入顺序列表（用于 Peek）
                orderList.AddLast(task.ParcelId);
            }
            
            // 5. 更新时间戳
            _lastEnqueueTimes[positionIndex] = _clock.LocalNow;

            _logger.LogDebug(
                "任务已加入 Position {PositionIndex} 队列: ParcelId={ParcelId}, DiverterId={DiverterId}, Action={Action}, IsUpdate={IsUpdate}, QueueCount={QueueCount}",
                positionIndex, task.ParcelId, task.DiverterId, task.DiverterAction, !isNewParcel, taskDict.Count);
        }
    }

    /// <inheritdoc/>
    public void EnqueuePriorityTask(int positionIndex, PositionQueueItem task)
    {
        var positionLock = GetOrCreateLock(positionIndex);
        
        lock (positionLock)
        {
            // Channel 不支持优先级插入头部，需要重建队列
            var channel = GetOrCreateChannel(positionIndex);
            var taskDict = GetOrCreateTaskDictionary(positionIndex);
            var queuedSet = GetOrCreateQueuedSet(positionIndex);
            var orderList = GetOrCreateOrderList(positionIndex);
            
            // 1. 创建新的临时 Channel
            var newChannel = Channel.CreateUnbounded<long>(_channelOptions);
            
            // 2. 先写入优先任务的ID
            newChannel.Writer.TryWrite(task.ParcelId);
            
            // 3. 将旧 Channel 中的所有ID复制到新 Channel
            var reader = channel.Reader;
            while (reader.TryRead(out var existingParcelId))
            {
                newChannel.Writer.TryWrite(existingParcelId);
            }
            
            // 4. 替换旧 Channel
            _orderChannels[positionIndex] = newChannel;
            
            // 5. 更新 Set 和 List（插入头部）
            queuedSet.Add(task.ParcelId);
            orderList.AddFirst(task.ParcelId);
            
            // 6. 存储任务数据
            taskDict[task.ParcelId] = task;
            
            // 7. 更新时间戳
            _lastEnqueueTimes[positionIndex] = _clock.LocalNow;

            _logger.LogWarning(
                "优先任务已插入 Position {PositionIndex} 队列头部: ParcelId={ParcelId}, DiverterId={DiverterId}, Action={Action}, QueueCount={QueueCount}",
                positionIndex, task.ParcelId, task.DiverterId, task.DiverterAction, taskDict.Count);
        }
    }

    /// <inheritdoc/>
    public PositionQueueItem? DequeueTask(int positionIndex)
    {
        if (!_orderChannels.TryGetValue(positionIndex, out var channel))
        {
            _logger.LogWarning("Position {PositionIndex} 队列不存在，无法取出任务", positionIndex);
            return null;
        }

        if (!_taskData.TryGetValue(positionIndex, out var taskDict))
        {
            _logger.LogWarning("Position {PositionIndex} 任务字典不存在，无法取出任务", positionIndex);
            return null;
        }

        var positionLock = GetOrCreateLock(positionIndex);

        // 逻辑删除 + 消费端跳过模式：
        // 循环读取 Channel 直到找到有效任务（未被逻辑删除的）
        // ⚠️ 性能保护：最多跳过100个已删除任务，避免极端场景下循环过久
        const int MaxSkipCount = 100;
        int skippedCount = 0;
        
        while (channel.Reader.TryRead(out var parcelId))
        {
            // ✅ 原子性保证：在锁内完成 taskDict.TryRemove + queuedSet.Remove + orderList.Remove
            // 确保在 EnqueueTask 和 DequeueTask 并发时，数据结构保持一致性
            // 防止"另一个传感器在出队和入队之间触发导致顺序错乱"
            PositionQueueItem? task = null;
            bool removed = false;
            
            lock (positionLock)
            {
                // 尝试从 Dictionary 中移除任务数据
                removed = taskDict.TryRemove(parcelId, out task);
                
                if (removed)
                {
                    // 成功移除 = 有效任务，同时更新 Set 和 List
                    if (_queuedSets.TryGetValue(positionIndex, out var queuedSet))
                    {
                        queuedSet.Remove(parcelId); // 允许该 key 未来重新入队
                    }
                    
                    if (_orderLists.TryGetValue(positionIndex, out var orderList))
                    {
                        // ✅ O(1) 头部删除（LinkedList.RemoveFirst）
                        // 理论上 parcelId 应该在列表头部（FIFO 顺序）
                        if (orderList.Count > 0 && orderList.First?.Value == parcelId)
                        {
                            orderList.RemoveFirst(); // O(1)
                        }
                        else
                        {
                            // 降级到查找删除（O(n)，但极少发生）
                            orderList.Remove(parcelId);
                        }
                    }
                }
            }
            
            // 在锁外处理返回值和日志，避免锁持有时间过长
            if (removed && task != null)
            {
                _lastDequeueTimes[positionIndex] = _clock.LocalNow;

                if (skippedCount > 0)
                {
                    _logger.LogDebug(
                        "任务已从 Position {PositionIndex} 队列取出（跳过{SkippedCount}个已删除任务）: ParcelId={ParcelId}, DiverterId={DiverterId}, Action={Action}, RemainingCount={RemainingCount}",
                        positionIndex, skippedCount, task.ParcelId, task.DiverterId, task.DiverterAction, taskDict.Count);
                }
                else
                {
                    _logger.LogDebug(
                        "任务已从 Position {PositionIndex} 队列取出: ParcelId={ParcelId}, DiverterId={DiverterId}, Action={Action}, RemainingCount={RemainingCount}",
                        positionIndex, task.ParcelId, task.DiverterId, task.DiverterAction, taskDict.Count);
                }

                return task;
            }
            else
            {
                // 失败 = 已被逻辑删除（取消或已处理），跳过继续读下一个
                skippedCount++;
                
                // 性能保护：防止极端场景（例如队列中全是已删除任务）
                if (skippedCount >= MaxSkipCount)
                {
                    _logger.LogWarning(
                        "Position {PositionIndex} 队列跳过了{SkippedCount}个已删除任务，停止继续搜索（可能存在大量垃圾数据）",
                        positionIndex, skippedCount);
                    break;
                }
                
                continue;
            }
        }

        // Channel 已空或所有任务都被逻辑删除
        _logger.LogDebug("Position {PositionIndex} 队列为空，无任务可取（跳过了{SkippedCount}个已删除任务）", 
            positionIndex, skippedCount);
        return null;
    }

    /// <inheritdoc/>
    public PositionQueueItem? PeekTask(int positionIndex)
    {
        var positionLock = GetOrCreateLock(positionIndex);
        
        lock (positionLock)
        {
            if (!_orderLists.TryGetValue(positionIndex, out var orderList))
            {
                return null;
            }

            if (!_taskData.TryGetValue(positionIndex, out var taskDict))
            {
                return null;
            }

            // 从顺序列表中找到第一个有效任务（在 taskDict 中存在的）
            // ⚠️ 性能保护：最多检查前10个元素，避免遍历大量已删除任务
            const int MaxCheckCount = 10;
            int checkedCount = 0;
            
            foreach (var parcelId in orderList)
            {
                if (taskDict.TryGetValue(parcelId, out var task))
                {
                    return task; // 返回第一个有效任务
                }
                
                checkedCount++;
                if (checkedCount >= MaxCheckCount)
                {
                    _logger.LogWarning(
                        "Position {PositionIndex} PeekTask 检查了 {CheckedCount} 个元素仍未找到有效任务",
                        positionIndex, checkedCount);
                    break;
                }
            }

            return null;
        }
    }

    /// <inheritdoc/>
    public PositionQueueItem? PeekNextTask(int positionIndex)
    {
        var positionLock = GetOrCreateLock(positionIndex);
        
        lock (positionLock)
        {
            if (!_orderLists.TryGetValue(positionIndex, out var orderList))
            {
                return null;
            }

            if (!_taskData.TryGetValue(positionIndex, out var taskDict))
            {
                return null;
            }

            // 从顺序列表中找到前两个有效任务
            // ⚠️ 性能保护：最多检查前10个元素
            const int MaxCheckCount = 10;
            int checkedCount = 0;
            int validTasks = 0;
            
            foreach (var parcelId in orderList)
            {
                if (taskDict.ContainsKey(parcelId))
                {
                    validTasks++;
                    if (validTasks == 2)
                    {
                        // 返回第二个有效任务
                        return taskDict.TryGetValue(parcelId, out var task) ? task : null;
                    }
                }
                
                checkedCount++;
                if (checkedCount >= MaxCheckCount)
                {
                    _logger.LogWarning(
                        "Position {PositionIndex} PeekNextTask 检查了 {CheckedCount} 个元素仍未找到第二个有效任务",
                        positionIndex, checkedCount);
                    break;
                }
            }

            return null; // 少于2个有效任务
        }
    }

    /// <inheritdoc/>
    public void ClearAllQueues()
    {
        var clearedCount = 0;
        var positionsCleaned = new List<int>();

        foreach (var positionIndex in _orderChannels.Keys.ToList())
        {
            var positionLock = GetOrCreateLock(positionIndex);
            
            lock (positionLock)
            {
                var count = GetQueueCount(positionIndex);
                
                if (count > 0)
                {
                    // 创建新的空 Channel 替换旧的
                    _orderChannels[positionIndex] = Channel.CreateUnbounded<long>(_channelOptions);
                    
                    // 清空任务字典
                    if (_taskData.TryGetValue(positionIndex, out var taskDict))
                    {
                        taskDict.Clear();
                    }
                    
                    // 清空已入队集合
                    if (_queuedSets.TryGetValue(positionIndex, out var queuedSet))
                    {
                        queuedSet.Clear();
                    }
                    
                    // 清空顺序列表
                    if (_orderLists.TryGetValue(positionIndex, out var orderList))
                    {
                        orderList.Clear();
                    }
                    
                    clearedCount += count;
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
        var taskDict = _taskData.GetOrAdd(positionIndex, _ => new ConcurrentDictionary<long, PositionQueueItem>());
        var headTask = PeekTask(positionIndex);
        
        _lastEnqueueTimes.TryGetValue(positionIndex, out var lastEnqueue);
        _lastDequeueTimes.TryGetValue(positionIndex, out var lastDequeue);

        return new QueueStatus
        {
            PositionIndex = positionIndex,
            TaskCount = taskDict.Count,
            HeadTask = headTask,
            LastEnqueueTime = lastEnqueue == default ? null : lastEnqueue,
            LastDequeueTime = lastDequeue == default ? null : lastDequeue
        };
    }

    /// <inheritdoc/>
    public Dictionary<int, QueueStatus> GetAllQueueStatuses()
    {
        var statuses = new Dictionary<int, QueueStatus>();

        foreach (var positionIndex in _orderChannels.Keys)
        {
            statuses[positionIndex] = GetQueueStatus(positionIndex);
        }

        return statuses;
    }

    /// <inheritdoc/>
    public int GetQueueCount(int positionIndex)
    {
        if (!_taskData.TryGetValue(positionIndex, out var taskDict))
        {
            return 0;
        }

        return taskDict.Count;
    }

    /// <inheritdoc/>
    public bool IsQueueEmpty(int positionIndex)
    {
        if (!_taskData.TryGetValue(positionIndex, out var taskDict))
        {
            return true;
        }

        return taskDict.IsEmpty;
    }
    
    /// <inheritdoc/>
    public int RemoveAllTasksForParcel(long parcelId)
    {
        int totalRemoved = 0;
        var affectedPositions = new List<int>();

        foreach (var positionIndex in _orderChannels.Keys.ToList())
        {
            if (!_taskData.TryGetValue(positionIndex, out var taskDict))
            {
                continue;
            }

            // 逻辑删除：仅从 Dictionary 中移除，不从 Channel 中移除
            // 消费端（DequeueTask）会自动跳过已删除的任务
            if (taskDict.TryRemove(parcelId, out _))
            {
                totalRemoved++;
                affectedPositions.Add(positionIndex);
                
                // 同时从 queuedSet 和 orderList 中移除，允许该 key 未来重新入队
                var positionLock = GetOrCreateLock(positionIndex);
                lock (positionLock)
                {
                    if (_queuedSets.TryGetValue(positionIndex, out var queuedSet))
                    {
                        queuedSet.Remove(parcelId);
                    }
                    
                    // 🔧 修复内存泄漏：清理 _orderLists 中的已删除包裹ID
                    if (_orderLists.TryGetValue(positionIndex, out var orderList))
                    {
                        orderList.Remove(parcelId);
                    }
                }
                
                _logger.LogDebug(
                    "[包裹丢失清理-逻辑删除] Position {PositionIndex} 移除了包裹 {ParcelId} 的任务",
                    positionIndex, parcelId);
            }
        }

        if (totalRemoved > 0)
        {
            _logger.LogWarning(
                "[包裹丢失清理-逻辑删除] 已从所有队列移除包裹 {ParcelId} 的共 {TotalCount} 个任务，" +
                "涉及 {PositionCount} 个 Position: [{Positions}]（消费端将自动跳过）",
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
        var affectedParcelIdsSet = new HashSet<long>();
        var affectedPositions = new Dictionary<int, int>(); // positionIndex -> 修改数量

        foreach (var positionIndex in _orderChannels.Keys.ToList())
        {
            if (!_taskData.TryGetValue(positionIndex, out var taskDict))
            {
                continue;
            }

            int modifiedCount = 0;

            // 遍历任务字典，识别并修改受影响的包裹任务
            foreach (var kvp in taskDict)
            {
                var task = kvp.Value;
                
                // 判断该包裹是否受影响
                if (task.CreatedAt > lostParcelCreatedAt && task.CreatedAt <= detectionTime)
                {
                    // 如果任务方向不是直行，则修改为直行
                    if (task.DiverterAction != DiverterDirection.Straight)
                    {
                        var modifiedTask = task with 
                        { 
                            DiverterAction = DiverterDirection.Straight,
                            LostDetectionDeadline = null,
                            LostDetectionTimeoutMs = null
                        };
                        
                        // 原地更新字典中的任务
                        taskDict[task.ParcelId] = modifiedTask;
                        affectedParcelIdsSet.Add(task.ParcelId);
                        modifiedCount++;
                        
                        _logger.LogDebug(
                            "[包裹丢失影响] Position {PositionIndex} 包裹 {ParcelId} 的任务方向从 {OldAction} 改为 Straight，已清除丢失检测截止时间",
                            positionIndex, task.ParcelId, task.DiverterAction);
                    }
                    else
                    {
                        // 已经是直行，但仍需清除丢失检测截止时间
                        var modifiedTask = task with 
                        { 
                            LostDetectionDeadline = null,
                            LostDetectionTimeoutMs = null
                        };
                        
                        taskDict[task.ParcelId] = modifiedTask;
                        affectedParcelIdsSet.Add(task.ParcelId);
                        modifiedCount++;
                        
                        _logger.LogDebug(
                            "[包裹丢失影响] Position {PositionIndex} 包裹 {ParcelId} 已是直行，但清除丢失检测截止时间",
                            positionIndex, task.ParcelId);
                    }
                }
            }

            if (modifiedCount > 0)
            {
                affectedPositions[positionIndex] = modifiedCount;
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

        return affectedParcelIdsSet.ToList();
    }
    
    /// <inheritdoc/>
    public TaskReplacementResult ReplaceTasksInPlace(long parcelId, List<PositionQueueItem> newTasks)
    {
        var result = new TaskReplacementResult();
        
        // 按 PositionIndex 对新任务进行分组
        var newTasksByPosition = newTasks.ToDictionary(t => t.PositionIndex);
        
        // 遍历所有需要替换的Position
        foreach (var newTask in newTasks)
        {
            var positionIndex = newTask.PositionIndex;
            
            // 检查该Position的任务字典是否存在
            if (!_taskData.TryGetValue(positionIndex, out var taskDict))
            {
                _logger.LogWarning(
                    "[原地替换-跳过] Position {PositionIndex} 的任务字典不存在，包裹 {ParcelId} 的任务无法替换",
                    positionIndex, parcelId);
                result.SkippedPositions.Add(positionIndex);
                continue;
            }
            
            // 检查该包裹的任务是否存在
            if (!taskDict.TryGetValue(parcelId, out var existingTask))
            {
                _logger.LogWarning(
                    "[原地替换-未找到] Position {PositionIndex} 队列中未找到包裹 {ParcelId} 的任务（可能已出队执行）",
                    positionIndex, parcelId);
                result.NotFoundPositions.Add(positionIndex);
                continue;
            }
            
            // 原地替换任务数据（保留原任务的大部分字段，只更新关键字段）
            var replacedTask = existingTask with
            {
                DiverterAction = newTask.DiverterAction,
                ExpectedArrivalTime = newTask.ExpectedArrivalTime,
                TimeoutThresholdMs = newTask.TimeoutThresholdMs,
                FallbackAction = newTask.FallbackAction,
                LostDetectionTimeoutMs = newTask.LostDetectionTimeoutMs,
                LostDetectionDeadline = newTask.LostDetectionDeadline,
                EarliestDequeueTime = newTask.EarliestDequeueTime
            };
            
            // 更新字典中的任务
            taskDict[parcelId] = replacedTask;
            result.ReplacedPositions.Add(positionIndex);
            
            _logger.LogDebug(
                "[原地替换] Position {PositionIndex} 包裹 {ParcelId} 的任务已替换: {OldAction} → {NewAction}",
                positionIndex, parcelId, existingTask.DiverterAction, newTask.DiverterAction);
        }
        
        // 幽灵任务清理：逻辑删除不在新路径中的Position的该包裹任务
        int totalRemovedCount = 0;
        foreach (var positionIndex in _orderChannels.Keys.ToList())
        {
            // 如果这个Position在新任务列表中，已经处理过了（替换），跳过
            if (newTasksByPosition.ContainsKey(positionIndex))
            {
                continue;
            }
            
            if (!_taskData.TryGetValue(positionIndex, out var taskDict))
            {
                continue;
            }
            
            // 逻辑删除：仅从 Dictionary 中移除，不从 Channel 中移除
            if (taskDict.TryRemove(parcelId, out _))
            {
                totalRemovedCount++;
                result.RemovedPositions.Add(positionIndex);
                
                // 从 queuedSet 和 orderList 中移除
                var positionLock = GetOrCreateLock(positionIndex);
                lock (positionLock)
                {
                    if (_queuedSets.TryGetValue(positionIndex, out var queuedSet))
                    {
                        queuedSet.Remove(parcelId);
                    }
                    
                    // 🔧 修复内存泄漏：清理 _orderLists 中的已删除包裹ID
                    if (_orderLists.TryGetValue(positionIndex, out var orderList))
                    {
                        orderList.Remove(parcelId);
                    }
                }
                
                _logger.LogDebug(
                    "[幽灵任务清理-逻辑删除] Position {PositionIndex} 移除了包裹 {ParcelId} 的旧任务（新路径不经过此Position）",
                    positionIndex, parcelId);
            }
        }
        
        // 统计结果
        result = result with 
        { 
            ReplacedCount = result.ReplacedPositions.Count,
            RemovedCount = totalRemovedCount
        };
        
        // 记录汇总日志
        if (result.IsFullySuccessful)
        {
            if (totalRemovedCount > 0)
            {
                _logger.LogInformation(
                    "[原地替换-成功] 包裹 {ParcelId} 的任务已成功处理，" +
                    "替换 {ReplacedCount} 个 Position: [{ReplacedPositions}]，" +
                    "逻辑删除 {RemovedCount} 个幽灵任务（Position: [{RemovedPositions}]）",
                    parcelId,
                    result.ReplacedCount,
                    string.Join(", ", result.ReplacedPositions),
                    result.RemovedCount,
                    string.Join(", ", result.RemovedPositions));
            }
            else
            {
                _logger.LogInformation(
                    "[原地替换-成功] 包裹 {ParcelId} 的所有任务已成功替换，共 {Count} 个 Position: [{Positions}]",
                    parcelId,
                    result.ReplacedCount,
                    string.Join(", ", result.ReplacedPositions));
            }
        }
        else if (result.IsPartiallySuccessful)
        {
            _logger.LogWarning(
                "[原地替换-部分成功] 包裹 {ParcelId} 部分任务替换成功: " +
                "成功 {SuccessCount} 个 Position [{SuccessPositions}], " +
                "未找到 {NotFoundCount} 个 [{NotFoundPositions}], " +
                "跳过 {SkippedCount} 个 [{SkippedPositions}], " +
                "逻辑删除幽灵任务 {RemovedCount} 个 [{RemovedPositions}]",
                parcelId,
                result.ReplacedCount, string.Join(", ", result.ReplacedPositions),
                result.NotFoundPositions.Count, string.Join(", ", result.NotFoundPositions),
                result.SkippedPositions.Count, string.Join(", ", result.SkippedPositions),
                result.RemovedCount, string.Join(", ", result.RemovedPositions));
        }
        else
        {
            _logger.LogError(
                "[原地替换-失败] 包裹 {ParcelId} 的任务替换完全失败，未找到任何任务（所有 {Count} 个 Position 都失败）",
                parcelId,
                newTasks.Count);
        }
        
        return result;
    }

    /// <summary>
    /// 获取或创建指定 Position 的锁
    /// </summary>
    private object GetOrCreateLock(int positionIndex)
    {
        return _positionLocks.GetOrAdd(positionIndex, _ => new object());
    }

    /// <summary>
    /// 获取或创建指定 Position 的 Channel
    /// </summary>
    private Channel<long> GetOrCreateChannel(int positionIndex)
    {
        return _orderChannels.GetOrAdd(positionIndex, _ => Channel.CreateUnbounded<long>(_channelOptions));
    }

    /// <summary>
    /// 获取或创建指定 Position 的任务字典
    /// </summary>
    private ConcurrentDictionary<long, PositionQueueItem> GetOrCreateTaskDictionary(int positionIndex)
    {
        return _taskData.GetOrAdd(positionIndex, _ => new ConcurrentDictionary<long, PositionQueueItem>());
    }

    /// <summary>
    /// 获取或创建指定 Position 的已入队包裹ID集合
    /// </summary>
    private HashSet<long> GetOrCreateQueuedSet(int positionIndex)
    {
        return _queuedSets.GetOrAdd(positionIndex, _ => new HashSet<long>());
    }

    /// <summary>
    /// 获取或创建指定 Position 的顺序列表（LinkedList 实现 O(1) 头部删除）
    /// </summary>
    private LinkedList<long> GetOrCreateOrderList(int positionIndex)
    {
        return _orderLists.GetOrAdd(positionIndex, _ => new LinkedList<long>());
    }

    /// <inheritdoc/>
    public bool TryUpdateTask(int positionIndex, long parcelId, Func<PositionQueueItem, PositionQueueItem> updateFunc)
    {
        if (!_taskData.TryGetValue(positionIndex, out var taskDict))
        {
            return false;
        }

        // 尝试获取当前值
        if (!taskDict.TryGetValue(parcelId, out var existingTask))
        {
            // 任务不存在
            return false;
        }

        // 执行更新函数
        var updatedTask = updateFunc(existingTask);

        // 原子更新：仅当值未被其他线程修改时才更新
        if (taskDict.TryUpdate(parcelId, updatedTask, existingTask))
        {
            _logger.LogDebug(
                "[原地更新] Position {PositionIndex} 包裹 {ParcelId} 的任务已更新",
                positionIndex, parcelId);
            return true;
        }

        // 值被其他线程修改了，当前策略是只尝试一次，失败即返回 false
        _logger.LogWarning(
            "[原地更新-竞争] Position {PositionIndex} 包裹 {ParcelId} 的任务更新失败（被其他线程修改）",
            positionIndex, parcelId);

        return false;
    }
}
