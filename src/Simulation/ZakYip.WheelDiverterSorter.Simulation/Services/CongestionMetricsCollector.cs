using System.Collections.Concurrent;
using ZakYip.WheelDiverterSorter.Core.Sorting.Models;
using ZakYip.WheelDiverterSorter.Core.Enums;

namespace ZakYip.WheelDiverterSorter.Simulation.Services;

/// <summary>
/// 拥堵指标收集器
/// Collects metrics for congestion detection
/// </summary>
public class CongestionMetricsCollector
{
    private readonly ConcurrentQueue<ParcelMetric> _recentParcels = new();
    private readonly TimeSpan _timeWindow;
    private int _inFlightCount;

    public CongestionMetricsCollector(TimeSpan timeWindow)
    {
        _timeWindow = timeWindow;
    }

    /// <summary>
    /// 记录包裹开始处理
    /// </summary>
    public void RecordParcelStarted()
    {
        Interlocked.Increment(ref _inFlightCount);
    }

    /// <summary>
    /// 记录包裹完成处理
    /// </summary>
    /// <param name="success">是否成功</param>
    /// <param name="latencyMs">延迟（毫秒）</param>
    public void RecordParcelCompleted(bool success, double latencyMs)
    {
        Interlocked.Decrement(ref _inFlightCount);
        
        _recentParcels.Enqueue(new ParcelMetric
        {
            Timestamp = DateTimeOffset.UtcNow,
            Success = success,
            LatencyMs = latencyMs
        });

        // 清理过期数据
        CleanupOldMetrics();
    }

    /// <summary>
    /// 获取当前拥堵指标
    /// </summary>
    public CongestionMetrics GetCurrentMetrics()
    {
        CleanupOldMetrics();

        var metrics = _recentParcels.ToArray();
        var totalCount = metrics.Length;
        var successCount = metrics.Count(m => m.Success);
        var failedCount = totalCount - successCount;

        var successRate = totalCount > 0 ? (double)successCount / totalCount : 1.0;
        var avgLatency = metrics.Length > 0 ? metrics.Average(m => m.LatencyMs) : 0;
        var maxLatency = metrics.Length > 0 ? metrics.Max(m => m.LatencyMs) : 0;

        return new CongestionMetrics
        {
            InFlightParcels = _inFlightCount,
            SuccessRate = successRate,
            AverageLatencyMs = avgLatency,
            MaxLatencyMs = maxLatency,
            TimeWindowSeconds = (int)_timeWindow.TotalSeconds,
            TotalParcels = totalCount,
            FailedParcels = failedCount
        };
    }

    /// <summary>
    /// 获取当前在途包裹数
    /// </summary>
    public int GetInFlightCount() => _inFlightCount;

    private void CleanupOldMetrics()
    {
        var cutoffTime = DateTimeOffset.UtcNow - _timeWindow;
        
        while (_recentParcels.TryPeek(out var metric) && metric.Timestamp < cutoffTime)
        {
            _recentParcels.TryDequeue(out _);
        }
    }

    private class ParcelMetric
    {
        public DateTimeOffset Timestamp { get; set; }
        public bool Success { get; set; }
        public double LatencyMs { get; set; }
    }
}
