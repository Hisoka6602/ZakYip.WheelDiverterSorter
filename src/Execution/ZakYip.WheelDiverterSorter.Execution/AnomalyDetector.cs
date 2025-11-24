using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using ZakYip.WheelDiverterSorter.Core.LineModel;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.LineModel.Events;
using ZakYip.WheelDiverterSorter.Core.LineModel.Services;

namespace ZakYip.WheelDiverterSorter.Execution;

/// <summary>
/// 异常趋势检测器实现 / Anomaly Trend Detector Implementation
/// </summary>
public class AnomalyDetector : IAnomalyDetector
{
    private readonly IAlertSink _alertSink;
    private readonly ILogger<AnomalyDetector> _logger;

    // 时间窗口内的数据缓存 - 使用线程安全的并发队列
    // Thread-safe concurrent queues for time-window data caching
    private readonly object _lock = new();

    private readonly ConcurrentQueue<SortingRecord> _sortingRecords = new();
    private readonly ConcurrentQueue<OverloadRecord> _overloadRecords = new();
    private readonly ConcurrentQueue<DateTimeOffset> _upstreamTimeouts = new();

    // 配置参数（可通过配置文件或构造函数注入）
    private readonly TimeSpan _monitoringWindow = TimeSpan.FromMinutes(5);

    private readonly double _exceptionChuteRatioThreshold = 0.15; // 15%
    private readonly double _overloadSpikeThreshold = 2.0; // 2x increase
    private readonly double _upstreamTimeoutRatioThreshold = 0.10; // 10%
    private readonly int _minimumSampleSize = 20;

    // 上一次告警时间（防止重复告警）
    private DateTimeOffset? _lastExceptionRatioAlert;

    private DateTimeOffset? _lastOverloadSpikeAlert;
    private DateTimeOffset? _lastUpstreamTimeoutAlert;
    private readonly TimeSpan _alertCooldown = TimeSpan.FromMinutes(10);

    public AnomalyDetector(IAlertSink alertSink, ILogger<AnomalyDetector> logger)
    {
        _alertSink = alertSink ?? throw new ArgumentNullException(nameof(alertSink));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void RecordSortingResult(string targetChuteId, bool isExceptionChute)
    {
        lock (_lock)
        {
            _sortingRecords.Enqueue(new SortingRecord
            {
                Timestamp = DateTimeOffset.UtcNow,
                ChuteId = targetChuteId,
                IsExceptionChute = isExceptionChute
            });

            // 清理过期数据
            CleanupOldRecords();
        }
    }

    public void RecordOverload(string reason)
    {
        lock (_lock)
        {
            _overloadRecords.Enqueue(new OverloadRecord
            {
                Timestamp = DateTimeOffset.UtcNow,
                Reason = reason
            });

            // 清理过期数据
            CleanupOldRecords();
        }
    }

    public void RecordUpstreamTimeout()
    {
        lock (_lock)
        {
            _upstreamTimeouts.Enqueue(DateTimeOffset.UtcNow);

            // 清理过期数据
            CleanupOldRecords();
        }
    }

    public async Task CheckAnomalyTrendsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await CheckExceptionChuteRatioAsync(cancellationToken);
            await CheckOverloadSpikeAsync(cancellationToken);
            await CheckUpstreamTimeoutRatioAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during anomaly trend check");
        }
    }

    public void ResetStatistics()
    {
        lock (_lock)
        {
            _sortingRecords.Clear();
            _overloadRecords.Clear();
            _upstreamTimeouts.Clear();
            _lastExceptionRatioAlert = null;
            _lastOverloadSpikeAlert = null;
            _lastUpstreamTimeoutAlert = null;
        }
    }

    private async Task CheckExceptionChuteRatioAsync(CancellationToken cancellationToken)
    {
        await Task.Yield();
        lock (_lock)
        {
            var now = DateTimeOffset.UtcNow;
            var windowStart = now - _monitoringWindow;

            var recentRecords = _sortingRecords.Where(r => r.Timestamp >= windowStart).ToList();

            if (recentRecords.Count < _minimumSampleSize)
            {
                return; // Not enough data
            }

            var exceptionCount = recentRecords.Count(r => r.IsExceptionChute);
            var exceptionRatio = (double)exceptionCount / recentRecords.Count;

            if (exceptionRatio > _exceptionChuteRatioThreshold)
            {
                // Check cooldown
                if (_lastExceptionRatioAlert.HasValue &&
                    (now - _lastExceptionRatioAlert.Value) < _alertCooldown)
                {
                    return; // Still in cooldown period
                }

                _lastExceptionRatioAlert = now;

                var alertEvent = new AlertRaisedEventArgs
                {
                    AlertCode = "EXCEPTION_CHUTE_RATIO_HIGH",
                    Severity = AlertSeverity.Warning,
                    Message = $"异常格口比例过高 / Exception chute ratio too high: {exceptionRatio:P2} (threshold: {_exceptionChuteRatioThreshold:P2})",
                    RaisedAt = now,
                    Details = new Dictionary<string, object>
                    {
                        { "exceptionRatio", exceptionRatio },
                        { "threshold", _exceptionChuteRatioThreshold },
                        { "exceptionCount", exceptionCount },
                        { "totalCount", recentRecords.Count },
                        { "windowMinutes", _monitoringWindow.TotalMinutes }
                    }
                };

                _logger.LogWarning(
                    "[ANOMALY ALERT] Exception chute ratio high: {Ratio:P2} (threshold: {Threshold:P2}), " +
                    "samples: {ExceptionCount}/{TotalCount} in last {WindowMinutes} minutes",
                    exceptionRatio, _exceptionChuteRatioThreshold, exceptionCount, recentRecords.Count, _monitoringWindow.TotalMinutes);

                _ = Task.Run(async () => await _alertSink.WriteAlertAsync(alertEvent, cancellationToken), cancellationToken);
            }
        }
    }

    private async Task CheckOverloadSpikeAsync(CancellationToken cancellationToken)
    {
        await Task.Yield();
        lock (_lock)
        {
            var now = DateTimeOffset.UtcNow;
            var windowStart = now - _monitoringWindow;
            var halfWindowStart = now - TimeSpan.FromMinutes(_monitoringWindow.TotalMinutes / 2);

            var recentRecords = _overloadRecords.Where(r => r.Timestamp >= windowStart).ToList();

            if (recentRecords.Count < _minimumSampleSize)
            {
                return; // Not enough data
            }

            // Split into two halves
            var firstHalf = recentRecords.Where(r => r.Timestamp < halfWindowStart).ToList();
            var secondHalf = recentRecords.Where(r => r.Timestamp >= halfWindowStart).ToList();

            if (firstHalf.Count < 5 || secondHalf.Count < 5)
            {
                return; // Not enough data in each half
            }

            // Check for RouteOverload spike
            var routeOverloadFirstHalf = firstHalf.Count(r => r.Reason.Contains("Route", StringComparison.OrdinalIgnoreCase) ||
                                                              r.Reason.Contains("Capacity", StringComparison.OrdinalIgnoreCase));
            var routeOverloadSecondHalf = secondHalf.Count(r => r.Reason.Contains("Route", StringComparison.OrdinalIgnoreCase) ||
                                                               r.Reason.Contains("Capacity", StringComparison.OrdinalIgnoreCase));

            if (routeOverloadFirstHalf == 0)
            {
                return; // Cannot calculate spike ratio
            }

            var spikeRatio = (double)routeOverloadSecondHalf / routeOverloadFirstHalf;

            if (spikeRatio >= _overloadSpikeThreshold)
            {
                // Check cooldown
                if (_lastOverloadSpikeAlert.HasValue &&
                    (now - _lastOverloadSpikeAlert.Value) < _alertCooldown)
                {
                    return; // Still in cooldown period
                }

                _lastOverloadSpikeAlert = now;

                var alertEvent = new AlertRaisedEventArgs
                {
                    AlertCode = "OVERLOAD_SPIKE",
                    Severity = AlertSeverity.Warning,
                    Message = $"超载事件激增 / Overload spike detected: {spikeRatio:F2}x increase (threshold: {_overloadSpikeThreshold:F2}x)",
                    RaisedAt = now,
                    Details = new Dictionary<string, object>
                    {
                        { "spikeRatio", spikeRatio },
                        { "threshold", _overloadSpikeThreshold },
                        { "firstHalfCount", routeOverloadFirstHalf },
                        { "secondHalfCount", routeOverloadSecondHalf },
                        { "windowMinutes", _monitoringWindow.TotalMinutes }
                    }
                };

                _logger.LogWarning(
                    "[ANOMALY ALERT] Overload spike detected: {SpikeRatio:F2}x increase (threshold: {Threshold:F2}x), " +
                    "from {FirstHalf} to {SecondHalf} in last {WindowMinutes} minutes",
                    spikeRatio, _overloadSpikeThreshold, routeOverloadFirstHalf, routeOverloadSecondHalf, _monitoringWindow.TotalMinutes);

                _ = Task.Run(async () => await _alertSink.WriteAlertAsync(alertEvent, cancellationToken), cancellationToken);
            }
        }
    }

    private async Task CheckUpstreamTimeoutRatioAsync(CancellationToken cancellationToken)
    {
        await Task.Yield();
        lock (_lock)
        {
            var now = DateTimeOffset.UtcNow;
            var windowStart = now - _monitoringWindow;

            var recentTimeouts = _upstreamTimeouts.Where(t => t >= windowStart).ToList();
            var recentSortings = _sortingRecords.Where(r => r.Timestamp >= windowStart).ToList();

            if (recentSortings.Count < _minimumSampleSize)
            {
                return; // Not enough data
            }

            var timeoutRatio = (double)recentTimeouts.Count / recentSortings.Count;

            if (timeoutRatio > _upstreamTimeoutRatioThreshold)
            {
                // Check cooldown
                if (_lastUpstreamTimeoutAlert.HasValue &&
                    (now - _lastUpstreamTimeoutAlert.Value) < _alertCooldown)
                {
                    return; // Still in cooldown period
                }

                _lastUpstreamTimeoutAlert = now;

                var alertEvent = new AlertRaisedEventArgs
                {
                    AlertCode = "UPSTREAM_TIMEOUT_HIGH",
                    Severity = AlertSeverity.Critical,
                    Message = $"上游超时比例过高 / Upstream timeout ratio too high: {timeoutRatio:P2} (threshold: {_upstreamTimeoutRatioThreshold:P2})",
                    RaisedAt = now,
                    Details = new Dictionary<string, object>
                    {
                        { "timeoutRatio", timeoutRatio },
                        { "threshold", _upstreamTimeoutRatioThreshold },
                        { "timeoutCount", recentTimeouts.Count },
                        { "totalCount", recentSortings.Count },
                        { "windowMinutes", _monitoringWindow.TotalMinutes }
                    }
                };

                _logger.LogError(
                    "[ANOMALY ALERT] Upstream timeout ratio high: {Ratio:P2} (threshold: {Threshold:P2}), " +
                    "timeouts: {TimeoutCount}/{TotalCount} in last {WindowMinutes} minutes",
                    timeoutRatio, _upstreamTimeoutRatioThreshold, recentTimeouts.Count, recentSortings.Count, _monitoringWindow.TotalMinutes);

                _ = Task.Run(async () => await _alertSink.WriteAlertAsync(alertEvent, cancellationToken), cancellationToken);
            }
        }
    }

    private void CleanupOldRecords()
    {
        var now = DateTimeOffset.UtcNow;
        var windowStart = now - _monitoringWindow;

        // ConcurrentQueue.TryPeek is thread-safe
        while (_sortingRecords.TryPeek(out var sortingRecord) && sortingRecord.Timestamp < windowStart)
        {
            _sortingRecords.TryDequeue(out _);
        }

        while (_overloadRecords.TryPeek(out var overloadRecord) && overloadRecord.Timestamp < windowStart)
        {
            _overloadRecords.TryDequeue(out _);
        }

        while (_upstreamTimeouts.TryPeek(out var timeout) && timeout < windowStart)
        {
            _upstreamTimeouts.TryDequeue(out _);
        }
    }

    private record struct SortingRecord
    {
        public DateTimeOffset Timestamp { get; init; }
        public string ChuteId { get; init; }
        public bool IsExceptionChute { get; init; }
    }

    private record struct OverloadRecord
    {
        public DateTimeOffset Timestamp { get; init; }
        public string Reason { get; init; }
    }
}
