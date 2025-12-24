using System.Collections.Concurrent;
using ZakYip.WheelDiverterSorter.Core.Sorting.Runtime;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Execution;

namespace ZakYip.WheelDiverterSorter.Application.Services.Metrics;

/// <summary>
/// 拥堵数据收集器：收集系统当前拥堵指标快照
/// </summary>
/// <remarks>
/// 使用 ConcurrentDictionary 替代 ConcurrentBag，支持高并发添加、查询和高效删除旧数据，
/// 避免长时间运行时的内存泄漏问题。
/// <para>
/// 技术债 TD-040 最初被标记为"已解决"，理由是"当前使用 ConcurrentBag 的实现已满足性能需求"，
/// 但后续在长时间运行场景中暴露出严重的内存泄漏问题，证明该判断不准确。
/// 本实现通过切换为 ConcurrentDictionary 并实现真正的清理逻辑，
/// 最终按 TD-040 的建议完成了技术债闭环；相关状态与详细说明已同步更新到
/// <c>docs/RepositoryStructure.md</c> 与 <c>docs/TechnicalDebtLog.md</c> 中。
/// </para>
/// </remarks>
public class CongestionDataCollector : ICongestionDataCollector
{
    private const int CleanupIntervalMinutes = 5;
    private const int HistoryWindowSeconds = 60;
    
    private readonly ISystemClock _clock;
    // 使用 ConcurrentDictionary 存储包裹历史记录，键为 ParcelId
    private readonly ConcurrentDictionary<long, ParcelHistoryRecord> _parcelHistory = new();
    private int _inFlightParcels = 0; // 使用 Interlocked 操作
    private readonly TimeSpan _historyWindow = TimeSpan.FromSeconds(HistoryWindowSeconds);
    private DateTime _lastCleanupTime = DateTime.MinValue;
    private readonly object _cleanupLock = new();

    public CongestionDataCollector(ISystemClock clock)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _lastCleanupTime = _clock.LocalNow;
    }

    /// <summary>
    /// 记录包裹进入系统
    /// </summary>
    public void RecordParcelEntry(long parcelId, DateTime entryTime)
    {
        // 包裹重新进入时，视为新的生命周期，重置完成时间为 null
        _parcelHistory.AddOrUpdate(
            parcelId,
            new ParcelHistoryRecord(parcelId, entryTime, null),
            (_, existing) => new ParcelHistoryRecord(parcelId, entryTime, null)
        );
        
        // 使用 Interlocked 原子递增
        Interlocked.Increment(ref _inFlightParcels);
        
        // 定期清理旧数据
        CleanupOldHistoryIfNeeded();
    }

    /// <summary>
    /// 记录包裹完成分拣
    /// </summary>
    public void RecordParcelCompletion(long parcelId, DateTime completionTime, bool isSuccess)
    {
        // 更新包裹完成时间
        _parcelHistory.AddOrUpdate(
            parcelId,
            // 如果包裹不存在（理论上不应该发生），创建新记录
            new ParcelHistoryRecord(parcelId, completionTime, completionTime),
            // 如果存在，更新完成时间
            (_, existing) => new ParcelHistoryRecord(existing.ParcelId, existing.EntryTime, completionTime)
        );
        
        // 使用 Interlocked 原子递减
        Interlocked.Decrement(ref _inFlightParcels);
    }

    /// <summary>
    /// 收集当前拥堵快照
    /// </summary>
    public CongestionSnapshot CollectSnapshot()
    {
        var now = _clock.LocalNow;
        var cutoffTime = now - _historyWindow;
        
        // Single pass through history to collect all data
        var recentCount = 0;
        var completedCount = 0;
        var totalLatency = 0.0;
        var maxLatency = 0.0;
        
        foreach (var entry in _parcelHistory.Values)
        {
            if (entry.EntryTime >= cutoffTime)
            {
                recentCount++;
                
                if (entry.CompletionTime.HasValue)
                {
                    completedCount++;
                    var latency = (entry.CompletionTime.Value - entry.EntryTime).TotalMilliseconds;
                    totalLatency += latency;
                    if (latency > maxLatency)
                    {
                        maxLatency = latency;
                    }
                }
            }
        }

        var averageLatency = completedCount > 0 ? totalLatency / completedCount : 0;
        
        // 简化的失败率计算（实际应根据具体业务逻辑）
        var failureRatio = 0.0; // 默认为0，实际应由业务逻辑提供

        return new CongestionSnapshot
        {
            InFlightParcels = _inFlightParcels,
            AverageLatencyMs = averageLatency,
            MaxLatencyMs = maxLatency,
            FailureRatio = failureRatio,
            TimeWindowSeconds = (int)_historyWindow.TotalSeconds,
            TotalSampledParcels = recentCount
        };
    }

    /// <summary>
    /// 获取当前在途包裹数
    /// </summary>
    public int GetInFlightCount()
    {
        // volatile read (Interlocked.CompareExchange with 0)
        return Interlocked.CompareExchange(ref _inFlightParcels, 0, 0);
    }

    /// <summary>
    /// 定期清理过期历史记录（如果需要）
    /// </summary>
    private void CleanupOldHistoryIfNeeded()
    {
        var now = _clock.LocalNow;
        
        // 每隔 CleanupIntervalMinutes 分钟执行一次清理
        if ((now - _lastCleanupTime).TotalMinutes < CleanupIntervalMinutes)
        {
            return;
        }
        
        // 使用锁防止多个线程同时执行清理
        lock (_cleanupLock)
        {
            // Double-check pattern: 再次检查是否需要清理
            if ((now - _lastCleanupTime).TotalMinutes < CleanupIntervalMinutes)
            {
                return;
            }
            
            _lastCleanupTime = now;
            
            // 清理超过时间窗口 + 额外 CleanupIntervalMinutes 分钟的记录
            var cutoffTime = now - _historyWindow - TimeSpan.FromMinutes(CleanupIntervalMinutes);
            
            var keysToRemove = _parcelHistory
                .Where(kvp => kvp.Value.EntryTime < cutoffTime)
                .Select(kvp => kvp.Key)
                .ToList();
            
            foreach (var key in keysToRemove)
            {
                _parcelHistory.TryRemove(key, out _);
            }
        }
    }

    /// <summary>
    /// 包裹历史记录
    /// </summary>
    private readonly record struct ParcelHistoryRecord
    {
        public long ParcelId { get; init; }
        public DateTime EntryTime { get; init; }
        public DateTime? CompletionTime { get; init; }

        public ParcelHistoryRecord(long parcelId, DateTime entryTime, DateTime? completionTime)
        {
            ParcelId = parcelId;
            EntryTime = entryTime;
            CompletionTime = completionTime;
        }
    }
}
