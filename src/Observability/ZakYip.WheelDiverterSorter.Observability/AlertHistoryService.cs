using ZakYip.WheelDiverterSorter.Core.Events.Monitoring;
using System.Collections.Concurrent;
using ZakYip.WheelDiverterSorter.Core.LineModel;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Events.Sorting;
using ZakYip.WheelDiverterSorter.Core.Events.Hardware;
using ZakYip.WheelDiverterSorter.Core.Events.Chute;
using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using ZakYip.WheelDiverterSorter.Core.Enums.Monitoring;

namespace ZakYip.WheelDiverterSorter.Observability;

/// <summary>
/// 告警历史记录服务（仅保留最近的告警，用于Health端点展示）
/// </summary>
public class AlertHistoryService : IAlertSink
{
    private readonly ConcurrentQueue<AlertRaisedEventArgs> _recentAlerts = new();
    private readonly int _maxHistorySize;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="maxHistorySize">最大历史记录数量，默认50</param>
    public AlertHistoryService(int maxHistorySize = 50)
    {
        _maxHistorySize = maxHistorySize;
    }

    /// <summary>
    /// 写入告警事件（记录到内存历史）
    /// </summary>
    public Task WriteAlertAsync(AlertRaisedEventArgs alertEvent, CancellationToken cancellationToken = default)
    {
        _recentAlerts.Enqueue(alertEvent);

        // 保持队列大小限制
        while (_recentAlerts.Count > _maxHistorySize)
        {
            _recentAlerts.TryDequeue(out _);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 获取最近N条Critical级别的告警
    /// </summary>
    public List<AlertRaisedEventArgs> GetRecentCriticalAlerts(int count = 10)
    {
        // PR-PERF: Optimize - filter Critical first, then sort only the filtered subset
        // This avoids sorting non-Critical alerts, which is more efficient when Critical alerts are a minority
        var criticalAlerts = new List<AlertRaisedEventArgs>();
        
        foreach (var alert in _recentAlerts)
        {
            if (alert.Severity == AlertSeverity.Critical)
            {
                criticalAlerts.Add(alert);
            }
        }
        
        // Sort only the Critical alerts by RaisedAt descending
        criticalAlerts.Sort((a, b) => b.RaisedAt.CompareTo(a.RaisedAt));
        
        // Take up to 'count' items
        if (criticalAlerts.Count > count)
        {
            return criticalAlerts.GetRange(0, count);
        }
        
        return criticalAlerts;
    }

    /// <summary>
    /// 获取所有最近的告警
    /// </summary>
    public List<AlertRaisedEventArgs> GetRecentAlerts(int count = 20)
    {
        // PR-PERF: Optimize - single pass with pre-allocated capacity
        var alertsArray = _recentAlerts.ToArray();
        Array.Sort(alertsArray, (a, b) => b.RaisedAt.CompareTo(a.RaisedAt));
        
        var takeCount = Math.Min(count, alertsArray.Length);
        var result = new List<AlertRaisedEventArgs>(takeCount);
        
        for (int i = 0; i < takeCount; i++)
        {
            result.Add(alertsArray[i]);
        }
        
        return result;
    }
}
