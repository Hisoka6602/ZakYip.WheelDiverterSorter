using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZakYip.WheelDiverterSorter.Core.Utilities;

namespace ZakYip.WheelDiverterSorter.Execution.Tracking;

/// <summary>
/// Position 间隔追踪器实现
/// </summary>
/// <remarks>
/// <para>基于滑动窗口记录每个 position 的最近 N 次触发间隔，计算中位数（抗异常值）。</para>
/// <para>⚠️ 重要变更：中位数值<b>仅用于观测统计</b>，不用于任何分拣逻辑判断。</para>
/// <para>所有超时判断、丢失判定均基于输送线配置（ConveyorSegmentConfiguration）。</para>
/// <para>实现特性：</para>
/// <list type="bullet">
///   <item>使用滑动窗口记录每个 position 的最近 N 次触发间隔</item>
///   <item>计算中位数（抗异常值）用于观测</item>
///   <item>仅使用内存缓存，无数据库依赖</item>
///   <item>内存开销：每个 position 约 80 字节</item>
/// </list>
/// </remarks>
public sealed class PositionIntervalTracker : IPositionIntervalTracker
{
    private readonly ISystemClock _clock;
    private readonly ILogger<PositionIntervalTracker> _logger;
    private readonly PositionIntervalTrackerOptions _options;
    
    // 每个 position 的间隔历史记录
    private readonly ConcurrentDictionary<int, CircularBuffer<double>> _intervalHistory;
    
    // 每个包裹在各个position的时间戳
    // Key: parcelId, Value: Dictionary<positionIndex, arrivedAt>
    private readonly ConcurrentDictionary<long, ConcurrentDictionary<int, DateTime>> _parcelPositionTimes;
    
    // 每个 position 的最后更新时间
    private readonly ConcurrentDictionary<int, DateTime> _lastUpdatedTimes;
    
    // 最后一次包裹记录时间
    private DateTime? _lastParcelRecordTime;
    private readonly object _lastRecordTimeLock = new();

    public PositionIntervalTracker(
        ISystemClock clock,
        ILogger<PositionIntervalTracker> logger,
        IOptions<PositionIntervalTrackerOptions> options)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        
        _intervalHistory = new ConcurrentDictionary<int, CircularBuffer<double>>();
        _parcelPositionTimes = new ConcurrentDictionary<long, ConcurrentDictionary<int, DateTime>>();
        _lastUpdatedTimes = new ConcurrentDictionary<int, DateTime>();
    }

    /// <inheritdoc/>
    public void RecordInterval(int positionIndex, double intervalMs)
    {
        if (intervalMs < 0)
        {
            _logger.LogWarning(
                "忽略无效的间隔值: PositionIndex={PositionIndex}, IntervalMs={IntervalMs}",
                positionIndex, intervalMs);
            return;
        }
        
        var buffer = _intervalHistory.GetOrAdd(
            positionIndex,
            _ => new CircularBuffer<double>(_options.WindowSize));
        
        buffer.Add(intervalMs);
        _lastUpdatedTimes[positionIndex] = _clock.LocalNow;
        
        _logger.LogDebug(
            "记录 Position {PositionIndex} 触发间隔: {IntervalMs}ms",
            positionIndex, intervalMs);
    }

    /// <inheritdoc/>
    public void RecordParcelPosition(long parcelId, int positionIndex, DateTime arrivedAt)
    {
        // 拒绝无效的包裹ID（ParcelId <= 0 表示非真实包裹）
        if (parcelId <= 0)
        {
            _logger.LogWarning(
                "拒绝记录无效的包裹ID {ParcelId} 在 Position {PositionIndex}。" +
                "ParcelId <= 0 通常表示 WheelFront/ChuteLock 等非包裹创建传感器触发，不应进入位置追踪。",
                parcelId,
                positionIndex);
            return;
        }
        
        // 更新最后包裹记录时间
        lock (_lastRecordTimeLock)
        {
            _lastParcelRecordTime = arrivedAt;
        }
        
        // 获取或创建该包裹的位置时间记录
        var positionTimes = _parcelPositionTimes.GetOrAdd(
            parcelId,
            _ => new ConcurrentDictionary<int, DateTime>());
        
        // 记录当前位置的到达时间
        positionTimes[positionIndex] = arrivedAt;
        
        // 如果不是入口位置(position 0)，计算与前一个position的间隔
        if (positionIndex > 0)
        {
            int previousPosition = positionIndex - 1;
            
            // 尝试获取前一个position的时间
            if (positionTimes.TryGetValue(previousPosition, out var previousTime))
            {
                // 计算物理运输间隔：arrivedAt 和 previousTime 都是传感器实际触发时间
                // 因此 intervalMs 反映的是包裹从前一位置物理移动到当前位置的真实耗时
                var intervalMs = (arrivedAt - previousTime).TotalMilliseconds;
                
                // 只记录有效的间隔
                if (intervalMs > 0 && intervalMs < _options.MaxReasonableIntervalMs)
                {
                    RecordInterval(positionIndex, intervalMs);
                    
                    _logger.LogDebug(
                        "包裹 {ParcelId} 从 Position {PrevPos} 到 Position {CurrPos} 物理运输间隔: {IntervalMs}ms " +
                        "(传感器触发时间差，非处理耗时)",
                        parcelId, previousPosition, positionIndex, intervalMs);
                }
                else
                {
                    _logger.LogWarning(
                        "包裹 {ParcelId} 从 Position {PrevPos} 到 Position {CurrPos} 物理运输间隔异常: {IntervalMs}ms",
                        parcelId, previousPosition, positionIndex, intervalMs);
                }
            }
            else
            {
                _logger.LogDebug(
                    "包裹 {ParcelId} Position {PosIndex}: 未找到前一位置时间，跳过间隔计算",
                    parcelId, positionIndex);
            }
        }
        else
        {
            _logger.LogDebug(
                "包裹 {ParcelId} 到达入口位置 Position {PosIndex}（起点），不计算间隔",
                parcelId, positionIndex);
        }
        
        // FIX: 基于字典大小的自动清理机制（防止内存泄漏导致性能劣化）
        // 
        // 问题背景：
        // - 之前的性能异常主要原因：Position 0 使用 _clock.LocalNow（处理时间）而非传感器 DetectedAt（检测时间），
        //   在线路高负载 + 线程池饱和时导致 Position 0→1 间隔被严重放大（时间戳错位），
        //   这一问题已在 POSITION_INTERVAL_FIX_SUMMARY.md 中修复和说明。
        // - 字典增长 / ConcurrentDictionary 在数据量大 + 高并发时会增加锁竞争，但这是次要因素，
        //   本段清理逻辑仅作为防御性措施，避免在极端情况下因长期不清理导致不必要的内存与竞争开销。
        // - 历史观测：在旧实现（使用处理时间作为 Position 0 基准）下，当字典从 11 个包裹增加到 33 个包裹时，
        //   曾观察到 Position 0→1 间隔从约 3.3s 增长到约 7.1s；该现象主要由时间戳错位放大，
        //   字典大小仅是放大的辅助因素，而非根本原因。
        //
        // 清理策略：
        // - 仅在字典大小超过 MaxParcelTrackingSize 时触发清理
        // - 删除最旧的包裹记录（基于 ParcelId 即时间戳）
        // - 清理量：删除超出限制的数量（通常1-2个包裹）
        // - 风险：极端情况下可能删除仍在途的慢速包裹记录，但概率极低（需要包裹在线体停留超长时间）
        //
        // 注意：正常情况下 CleanupParcelMemory 会及时清理，此机制仅作为防御性措施
        if (_options.MaxParcelTrackingSize > 0 && _parcelPositionTimes.Count > _options.MaxParcelTrackingSize)
        {
            CleanupOldestParcelRecords(_parcelPositionTimes.Count - _options.MaxParcelTrackingSize);
        }
    }

    /// <inheritdoc/>
    public (int PositionIndex, double? MedianIntervalMs, int SampleCount, double? MinIntervalMs, double? MaxIntervalMs, DateTime? LastUpdatedAt)? GetStatistics(int positionIndex)
    {
        if (!_intervalHistory.TryGetValue(positionIndex, out var buffer) || buffer.Count == 0)
        {
            return null;
        }
        
        // 优化：在锁外获取数据快照，减少锁持有时间
        double[] intervals;
        int count;
        
        // 使用 buffer 内部的 ToArray 方法（已有锁保护）
        intervals = buffer.ToArray();
        count = intervals.Length;
        
        if (count == 0)
        {
            return null;
        }
        
        // 优化：在锁外执行统计计算（CPU密集型操作）
        // 使用单次排序同时获取 min, max, median，避免多次遍历
        Array.Sort(intervals);
        var min = intervals[0];
        var max = intervals[count - 1];
        var median = count % 2 == 0
            ? (intervals[count / 2 - 1] + intervals[count / 2]) / 2.0
            : intervals[count / 2];
        
        _lastUpdatedTimes.TryGetValue(positionIndex, out var lastUpdated);
        
        return (
            positionIndex,
            median,
            count,
            min,
            max,
            lastUpdated == default ? null : lastUpdated
        );
    }

    /// <inheritdoc/>
    public IReadOnlyList<(int PositionIndex, double? MedianIntervalMs, int SampleCount, double? MinIntervalMs, double? MaxIntervalMs, DateTime? LastUpdatedAt)> GetAllStatistics()
    {
        return _intervalHistory.Keys
            .Where(k => k > 0) // 排除 positionIndex = 0 (入口位置没有间隔数据)
            .OrderBy(k => k)
            .Select(GetStatistics)
            .Where(stat => stat != null)
            .Select(stat => stat!.Value)
            .ToList();
    }

    /// <inheritdoc/>
    public void ClearStatistics(int positionIndex)
    {
        _intervalHistory.TryRemove(positionIndex, out _);
        _lastUpdatedTimes.TryRemove(positionIndex, out _);
        
        _logger.LogInformation("已清空 Position {PositionIndex} 的统计数据", positionIndex);
    }

    /// <inheritdoc/>
    public void ClearAllStatistics()
    {
        _intervalHistory.Clear();
        _lastUpdatedTimes.Clear();
        
        // 清空包裹位置追踪记录（停止/急停/复位时，所有在途包裹已不在系统中）
        var parcelCount = _parcelPositionTimes.Count;
        _parcelPositionTimes.Clear();
        
        _logger.LogInformation(
            "已清空所有 Position 的统计数据和 {ParcelCount} 个包裹的位置追踪记录",
            parcelCount);
    }

    /// <inheritdoc/>
    public void ClearParcelTracking(long parcelId)
    {
        if (_parcelPositionTimes.TryRemove(parcelId, out _))
        {
            _logger.LogInformation(
                "[位置追踪清理] 已清除包裹 {ParcelId} 的所有位置追踪记录",
                parcelId);
        }
    }

    /// <inheritdoc/>
    public DateTime? GetLastParcelRecordTime()
    {
        lock (_lastRecordTimeLock)
        {
            return _lastParcelRecordTime;
        }
    }

    /// <inheritdoc/>
    public bool ShouldAutoClear(int autoClearIntervalMs)
    {
        // 如果设置为0，表示不自动清空
        if (autoClearIntervalMs <= 0)
        {
            return false;
        }

        lock (_lastRecordTimeLock)
        {
            // 如果从未记录过包裹，不需要清空
            if (!_lastParcelRecordTime.HasValue)
            {
                return false;
            }

            // 检查是否超过了自动清空间隔
            var elapsed = (_clock.LocalNow - _lastParcelRecordTime.Value).TotalMilliseconds;
            return elapsed >= autoClearIntervalMs;
        }
    }

    // 注意：CalculateMedian 方法已移除
    // 优化：统计计算已内联到 GetStatistics 方法中，使用 Array.Sort 一次性完成排序
    // 避免 LINQ OrderBy().ToArray() 产生的额外分配和性能开销
    
    /// <summary>
    /// 清理最旧的包裹位置追踪记录
    /// </summary>
    /// <param name="count">要清理的记录数量</param>
    /// <remarks>
    /// 基于 ParcelId（时间戳）删除最旧的记录，防止字典无限增长导致性能劣化。
    /// 仅在字典大小超过 MaxParcelTrackingSize 时由 RecordParcelPosition 自动调用。
    /// 使用批次清理策略处理并发写入场景。
    /// </remarks>
    private void CleanupOldestParcelRecords(int count)
    {
        if (count <= 0)
        {
            return;
        }

        // 为了应对并发写入，这里按批次清理，并在每批后重新检查当前大小。
        // 防止在获取快照和实际删除之间有大量新记录写入，导致清理后仍然超过上限。
        var totalRemoved = 0;
        var iteration = 0;
        const int maxCleanupIterations = 5;

        while (iteration < maxCleanupIterations)
        {
            iteration++;

            var currentSize = _parcelPositionTimes.Count;
            var targetSize = _options.MaxParcelTrackingSize;

            if (currentSize <= targetSize)
            {
                break;
            }

            // 需要清理的数量：当前超出的部分和调用者建议的数量取较小值
            var overflow = currentSize - targetSize;
            var batchSize = Math.Min(overflow, count);
            if (batchSize <= 0)
            {
                break;
            }

            // 使用 Array.Sort 而非 LINQ OrderBy，避免在热路径中产生额外分配
            var keysArray = _parcelPositionTimes.Keys.ToArray();
            if (keysArray.Length == 0)
            {
                break;
            }
            
            Array.Sort(keysArray);
            var takeCount = Math.Min(batchSize, keysArray.Length);

            var removedThisBatch = 0;
            for (int i = 0; i < takeCount; i++)
            {
                var parcelId = keysArray[i];
                if (_parcelPositionTimes.TryRemove(parcelId, out _))
                {
                    removedThisBatch++;
                    _logger.LogDebug(
                        "[自动清理] 字典大小超限，清除最旧包裹 {ParcelId} 的位置追踪记录",
                        parcelId);
                }
            }

            if (removedThisBatch == 0)
            {
                // 没有真正删除任何记录，避免在高并发场景下出现无意义的自旋
                break;
            }

            totalRemoved += removedThisBatch;
        }

        if (totalRemoved > 0)
        {
            _logger.LogInformation(
                "[自动清理] 清理了 {Count} 个最旧包裹的位置追踪记录，当前字典大小: {CurrentSize}",
                totalRemoved,
                _parcelPositionTimes.Count);
        }
    }
}

/// <summary>
/// Position 间隔追踪器配置选项
/// </summary>
public sealed class PositionIntervalTrackerOptions
{
    /// <summary>
    /// 监控间隔（毫秒）
    /// </summary>
    /// <remarks>
    /// 包裹丢失监控服务扫描队列的时间间隔
    /// 默认值：60ms
    /// 推荐范围：50-500ms
    /// 值越小检测越及时，但CPU占用越高
    /// </remarks>
    public int MonitoringIntervalMs { get; set; } = 60;
    
    /// <summary>
    /// 历史窗口大小（保留最近 N 个间隔样本）
    /// </summary>
    /// <remarks>
    /// 默认值：10
    /// 推荐范围：10-20
    /// </remarks>
    public int WindowSize { get; set; } = 10;
    
    /// <summary>
    /// 超时系数（应用于中位数）
    /// </summary>
    /// <remarks>
    /// 动态阈值 = 中位数 * 超时系数
    /// 默认值：3.0
    /// 推荐范围：2.5-3.5
    /// </remarks>
    public double TimeoutMultiplier { get; set; } = 3.0;
    
    
    /// <summary>
    /// 计算动态阈值所需的最小样本数
    /// </summary>
    /// <remarks>
    /// 样本数少于此值时，不计算动态阈值
    /// 默认值：3
    /// </remarks>
    public int MinSamplesForThreshold { get; set; } = 3;
    
    /// <summary>
    /// 丢失判定系数（应用于中位数）
    /// </summary>
    /// <remarks>
    /// 丢失阈值 = 中位数 * 丢失判定系数
    /// 用于判断包裹是否物理丢失（不在分拣设备上）
    /// 默认值：1.5
    /// 推荐范围：1.5-2.5
    /// 超过此阈值判定为包裹丢失，需要从队列删除
    /// </remarks>
    public double LostDetectionMultiplier { get; set; } = 1.5;
    
    /// <summary>
    /// 最大合理间隔（毫秒）
    /// </summary>
    /// <remarks>
    /// 超过此值的间隔被视为异常，不计入统计
    /// 默认值：60000ms (60秒)
    /// </remarks>
    public double MaxReasonableIntervalMs { get; set; } = 60000;
    
    /// <summary>
    /// 包裹位置追踪字典的最大容量
    /// </summary>
    /// <remarks>
    /// 当 _parcelPositionTimes 字典大小超过此值时，自动清理最旧的记录。
    /// 默认值：50（足够容纳高速分拣场景下的在途包裹）
    /// 推荐范围：30-100
    /// 设置为 0 表示不限制大小（不推荐，可能导致内存泄漏）
    /// </remarks>
    public int MaxParcelTrackingSize { get; set; } = 50;
}
