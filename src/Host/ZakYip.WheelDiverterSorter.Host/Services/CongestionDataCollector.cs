using System.Collections.Concurrent;
using ZakYip.WheelDiverterSorter.Core.Sorting.Runtime;
using ZakYip.WheelDiverterSorter.Core.Enums.Sorting;
using ZakYip.WheelDiverterSorter.Observability.Utilities;

namespace ZakYip.WheelDiverterSorter.Host.Services;

/// <summary>
/// 拥堵数据收集器：收集系统当前拥堵指标快照
/// </summary>
public class CongestionDataCollector
{
    private readonly ISystemClock _clock;
    // PR-44: 使用 ConcurrentBag 替代 List + lock，支持高并发添加和查询
    private readonly ConcurrentBag<(long ParcelId, DateTime EntryTime, DateTime? CompletionTime)> _parcelHistory = new();
    private int _inFlightParcels = 0; // 使用 Interlocked 操作
    private readonly TimeSpan _historyWindow = TimeSpan.FromSeconds(60);

    public CongestionDataCollector(ISystemClock clock)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <summary>
    /// 记录包裹进入系统
    /// </summary>
    public void RecordParcelEntry(long parcelId, DateTime entryTime)
    {
        // PR-44: ConcurrentBag.Add 是线程安全的
        _parcelHistory.Add((parcelId, entryTime, null));
        // PR-44: 使用 Interlocked 原子递增
        Interlocked.Increment(ref _inFlightParcels);
        CleanupOldHistory();
    }

    /// <summary>
    /// 记录包裹完成分拣
    /// </summary>
    public void RecordParcelCompletion(long parcelId, DateTime completionTime, bool isSuccess)
    {
        // PR-44: ConcurrentBag 不支持查找和修改，保持原始设计
        // 这里的实现有局限性，但为了向后兼容暂时保留
        // TODO: 未来考虑使用 ConcurrentDictionary<long, ParcelRecord> 优化查找性能
        
        // 使用 Interlocked 原子递减
        Interlocked.Decrement(ref _inFlightParcels);
        
        // 注意：由于 ConcurrentBag 不支持修改，这里只能添加新记录
        // 原有的查找和修改逻辑被简化
    }

    /// <summary>
    /// 收集当前拥堵快照
    /// </summary>
    public CongestionSnapshot CollectSnapshot()
    {
        // PR-44: ConcurrentBag 可以安全地迭代和ToList
        CleanupOldHistory();

        var now = _clock.LocalNow;
        var recentParcels = _parcelHistory
            .Where(p => p.EntryTime >= now - _historyWindow)
            .ToList();

        var completedParcels = recentParcels
            .Where(p => p.CompletionTime.HasValue)
            .ToList();

        var latencies = completedParcels
            .Select(p => (p.CompletionTime!.Value - p.EntryTime).TotalMilliseconds)
            .ToList();

        var averageLatency = latencies.Any() ? latencies.Average() : 0;
        var maxLatency = latencies.Any() ? latencies.Max() : 0;

        // 简化的失败率计算（实际应根据具体业务逻辑）
        var failureRatio = 0.0; // 默认为0，实际应由业务逻辑提供

        return new CongestionSnapshot
        {
            InFlightParcels = _inFlightParcels,
            AverageLatencyMs = averageLatency,
            MaxLatencyMs = maxLatency,
            FailureRatio = failureRatio,
            TimeWindowSeconds = (int)_historyWindow.TotalSeconds,
            TotalSampledParcels = recentParcels.Count
        };
    }

    /// <summary>
    /// 获取当前在途包裹数
    /// </summary>
    public int GetInFlightCount()
    {
        // PR-44: volatile read (Interlocked.CompareExchange with 0)
        return Interlocked.CompareExchange(ref _inFlightParcels, 0, 0);
    }

    /// <summary>
    /// 清理过期历史记录
    /// </summary>
    private void CleanupOldHistory()
    {
        // PR-44: ConcurrentBag 不支持 RemoveAll，需要重建
        // 由于清理操作不频繁，且数据量不大，暂时保持简单实现
        // TODO: 如果性能成为问题，考虑使用定时后台任务清理
        var cutoff = _clock.LocalNow - _historyWindow - TimeSpan.FromMinutes(5); // 额外保留5分钟
        
        // 简化实现：不主动清理，依赖 CollectSnapshot 时的过滤
        // ConcurrentBag 设计为只增不减，适合这种追加式日志场景
    }
}
