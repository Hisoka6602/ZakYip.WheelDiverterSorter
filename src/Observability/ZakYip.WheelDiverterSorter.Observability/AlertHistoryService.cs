using System.Collections.Concurrent;
using ZakYip.WheelDiverterSorter.Core.LineModel;
using ZakYip.WheelDiverterSorter.Core.Enums.Communication;
using ZakYip.WheelDiverterSorter.Core.Enums.Conveyor;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Enums.Routing;
using ZakYip.WheelDiverterSorter.Core.Enums.Sensors;
using ZakYip.WheelDiverterSorter.Core.Enums.System;
using ZakYip.WheelDiverterSorter.Core.LineModel.Events;
using ZakYip.WheelDiverterSorter.Core.LineModel.Services;

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
        return _recentAlerts
            .Where(a => a.Severity == AlertSeverity.Critical)
            .OrderByDescending(a => a.RaisedAt)
            .Take(count)
            .ToList();
    }

    /// <summary>
    /// 获取所有最近的告警
    /// </summary>
    public List<AlertRaisedEventArgs> GetRecentAlerts(int count = 20)
    {
        return _recentAlerts
            .OrderByDescending(a => a.RaisedAt)
            .Take(count)
            .ToList();
    }
}
