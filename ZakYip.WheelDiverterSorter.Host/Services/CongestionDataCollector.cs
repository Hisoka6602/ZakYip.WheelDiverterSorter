using ZakYip.WheelDiverterSorter.Core.Sorting.Runtime;

namespace ZakYip.WheelDiverterSorter.Host.Services;

/// <summary>
/// 拥堵数据收集器：收集系统当前拥堵指标快照
/// </summary>
public class CongestionDataCollector
{
    private readonly List<(long ParcelId, DateTime EntryTime, DateTime? CompletionTime)> _parcelHistory = new();
    private readonly object _lock = new();
    private int _inFlightParcels = 0;
    private readonly TimeSpan _historyWindow = TimeSpan.FromSeconds(60);

    /// <summary>
    /// 记录包裹进入系统
    /// </summary>
    public void RecordParcelEntry(long parcelId, DateTime entryTime)
    {
        lock (_lock)
        {
            _parcelHistory.Add((parcelId, entryTime, null));
            _inFlightParcels++;
            CleanupOldHistory();
        }
    }

    /// <summary>
    /// 记录包裹完成分拣
    /// </summary>
    public void RecordParcelCompletion(long parcelId, DateTime completionTime, bool isSuccess)
    {
        lock (_lock)
        {
            var index = _parcelHistory.FindIndex(p => p.ParcelId == parcelId && !p.CompletionTime.HasValue);
            if (index >= 0)
            {
                var entry = _parcelHistory[index];
                _parcelHistory[index] = (entry.ParcelId, entry.EntryTime, completionTime);
                _inFlightParcels--;
            }
        }
    }

    /// <summary>
    /// 收集当前拥堵快照
    /// </summary>
    public CongestionSnapshot CollectSnapshot()
    {
        lock (_lock)
        {
            CleanupOldHistory();

            var now = DateTime.UtcNow;
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
    }

    /// <summary>
    /// 获取当前在途包裹数
    /// </summary>
    public int GetInFlightCount()
    {
        lock (_lock)
        {
            return _inFlightParcels;
        }
    }

    /// <summary>
    /// 清理过期历史记录
    /// </summary>
    private void CleanupOldHistory()
    {
        var cutoff = DateTime.UtcNow - _historyWindow - TimeSpan.FromMinutes(5); // 额外保留5分钟
        _parcelHistory.RemoveAll(p => p.EntryTime < cutoff);
    }
}
