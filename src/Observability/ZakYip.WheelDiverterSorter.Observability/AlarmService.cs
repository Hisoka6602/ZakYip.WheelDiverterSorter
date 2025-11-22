using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using ZakYip.WheelDiverterSorter.Observability.Utilities;

namespace ZakYip.WheelDiverterSorter.Observability;

/// <summary>
/// 告警服务 / Alarm Service
/// Monitors system conditions and triggers alarms when rules are violated
/// </summary>
public class AlarmService
{
    private readonly ILogger<AlarmService> _logger;
    private readonly ISystemClock _clock;
    private readonly List<AlarmEvent> _activeAlarms = new();
    private readonly object _lock = new();

    // Metrics for alarm rules
    private long _sortingSuccessCount;
    private long _sortingFailureCount;
    private int _currentQueueLength;
    // 使用 ConcurrentDictionary 实现线程安全 / Use ConcurrentDictionary for thread safety
    private readonly ConcurrentDictionary<string, DateTime> _diverterFaults = new();
    private readonly ConcurrentDictionary<string, DateTime> _sensorFaults = new();
    private readonly ConcurrentDictionary<string, DateTime> _ruleEngineDisconnections = new();
    private DateTime? _lastRuleEngineConnected;
    private DateTime? _systemStartTime;

    // Alarm rule thresholds
    private const double SortingFailureRateThreshold = 0.05; // 5%
    private const int QueueBacklogThreshold = 50; // 50 parcels
    private static readonly TimeSpan RuleEngineDisconnectionThreshold = TimeSpan.FromMinutes(1);

    public AlarmService(ILogger<AlarmService> logger, ISystemClock clock)
    {
        _logger = logger;
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _systemStartTime = _clock.LocalNow;
    }

    /// <summary>
    /// 获取所有活跃告警 / Get all active alarms
    /// </summary>
    public IReadOnlyList<AlarmEvent> GetActiveAlarms()
    {
        lock (_lock)
        {
            return _activeAlarms.ToList();
        }
    }

    /// <summary>
    /// 记录分拣成功 / Record sorting success
    /// </summary>
    public void RecordSortingSuccess()
    {
        Interlocked.Increment(ref _sortingSuccessCount);
        CheckSortingFailureRate();
    }

    /// <summary>
    /// 记录分拣失败 / Record sorting failure
    /// </summary>
    public void RecordSortingFailure()
    {
        Interlocked.Increment(ref _sortingFailureCount);
        CheckSortingFailureRate();
    }

    /// <summary>
    /// 更新队列长度 / Update queue length
    /// </summary>
    public void UpdateQueueLength(int length)
    {
        _currentQueueLength = length;
        CheckQueueBacklog();
    }

    /// <summary>
    /// 报告摆轮故障 / Report diverter fault
    /// </summary>
    public void ReportDiverterFault(string diverterId, string faultDescription)
    {
        // TryAdd is thread-safe - only adds if key doesn't exist
        if (_diverterFaults.TryAdd(diverterId, _clock.LocalNow))
        {
            lock (_lock)
            {
                TriggerAlarm(new AlarmEvent
                {
                    Type = AlarmType.DiverterFault,
                    Level = AlarmLevel.P1,
                    Message = $"摆轮故障 / Diverter fault: {diverterId}",
                    Details = new Dictionary<string, object>
                    {
                        { "diverterId", diverterId },
                        { "faultDescription", faultDescription }
                    }
                });
            }
        }
    }

    /// <summary>
    /// 清除摆轮故障 / Clear diverter fault
    /// </summary>
    public void ClearDiverterFault(string diverterId)
    {
        // TryRemove is thread-safe
        if (_diverterFaults.TryRemove(diverterId, out _))
        {
            ClearAlarm(AlarmType.DiverterFault, diverterId);
        }
    }

    /// <summary>
    /// 报告传感器故障 / Report sensor fault
    /// </summary>
    public void ReportSensorFault(string sensorId, string faultDescription)
    {
        // TryAdd is thread-safe - only adds if key doesn't exist
        if (_sensorFaults.TryAdd(sensorId, _clock.LocalNow))
        {
            lock (_lock)
            {
                TriggerAlarm(new AlarmEvent
                {
                    Type = AlarmType.SensorFault,
                    Level = AlarmLevel.P2,
                    Message = $"传感器故障 / Sensor fault: {sensorId}",
                    Details = new Dictionary<string, object>
                    {
                        { "sensorId", sensorId },
                        { "faultDescription", faultDescription }
                    }
                });
            }
        }
    }

    /// <summary>
    /// 清除传感器故障 / Clear sensor fault
    /// </summary>
    public void ClearSensorFault(string sensorId)
    {
        // TryRemove is thread-safe
        if (_sensorFaults.TryRemove(sensorId, out _))
        {
            ClearAlarm(AlarmType.SensorFault, sensorId);
        }
    }

    /// <summary>
    /// 报告RuleEngine断线 / Report RuleEngine disconnection
    /// </summary>
    public void ReportRuleEngineDisconnection(string connectionType)
    {
        // TryAdd is thread-safe
        _ruleEngineDisconnections.TryAdd(connectionType, _clock.LocalNow);
        _lastRuleEngineConnected = null;
    }

    /// <summary>
    /// 报告RuleEngine连接恢复 / Report RuleEngine reconnection
    /// </summary>
    public void ReportRuleEngineReconnection(string connectionType)
    {
        // TryRemove is thread-safe
        _ruleEngineDisconnections.TryRemove(connectionType, out _);
        _lastRuleEngineConnected = _clock.LocalNow;
        ClearAlarm(AlarmType.RuleEngineDisconnected, connectionType);
    }

    /// <summary>
    /// 检查RuleEngine断线时长 / Check RuleEngine disconnection duration
    /// </summary>
    public void CheckRuleEngineDisconnection()
    {
        lock (_lock)
        {
            foreach (var kvp in _ruleEngineDisconnections.ToList())
            {
                var disconnectionDuration = _clock.LocalNow - kvp.Value;
                if (disconnectionDuration > RuleEngineDisconnectionThreshold)
                {
                    // Check if alarm already exists
                    var existingAlarm = _activeAlarms.FirstOrDefault(a =>
                        a.Type == AlarmType.RuleEngineDisconnected &&
                        a.Details.ContainsKey("connectionType") &&
                        a.Details["connectionType"].ToString() == kvp.Key);

                    if (existingAlarm == null)
                    {
                        TriggerAlarm(new AlarmEvent
                        {
                            Type = AlarmType.RuleEngineDisconnected,
                            Level = AlarmLevel.P1,
                            Message = $"RuleEngine断线超过1分钟 / RuleEngine disconnected for more than 1 minute: {kvp.Key}",
                            Details = new Dictionary<string, object>
                            {
                                { "connectionType", kvp.Key },
                                { "disconnectionDuration", disconnectionDuration.TotalSeconds }
                            }
                        });
                    }
                }
            }
        }
    }

    /// <summary>
    /// 报告系统重启 / Report system restart
    /// </summary>
    public void ReportSystemRestart()
    {
        TriggerAlarm(new AlarmEvent
        {
            Type = AlarmType.SystemRestart,
            Level = AlarmLevel.P3,
            Message = "系统重启 / System restarted",
            Details = new Dictionary<string, object>
            {
                { "startTime", _systemStartTime ?? _clock.LocalNow }
            }
        });
    }

    /// <summary>
    /// 确认告警 / Acknowledge alarm
    /// </summary>
    public void AcknowledgeAlarm(AlarmEvent alarm)
    {
        lock (_lock)
        {
            var existingAlarm = _activeAlarms.FirstOrDefault(a => a == alarm);
            if (existingAlarm != null)
            {
                existingAlarm.IsAcknowledged = true;
                existingAlarm.AcknowledgedAt = _clock.LocalNow;
            }
        }
    }

    private void CheckSortingFailureRate()
    {
        var totalCount = _sortingSuccessCount + _sortingFailureCount;
        if (totalCount < 20) // Need minimum samples
        {
            return;
        }

        var failureRate = (double)_sortingFailureCount / totalCount;
        if (failureRate > SortingFailureRateThreshold)
        {
            lock (_lock)
            {
                // Check if alarm already exists
                var existingAlarm = _activeAlarms.FirstOrDefault(a =>
                    a.Type == AlarmType.SortingFailureRateHigh);

                if (existingAlarm == null)
                {
                    TriggerAlarm(new AlarmEvent
                    {
                        Type = AlarmType.SortingFailureRateHigh,
                        Level = AlarmLevel.P1,
                        Message = $"分拣失败率过高 / Sorting failure rate too high: {failureRate:P2}",
                        Details = new Dictionary<string, object>
                        {
                            { "failureRate", failureRate },
                            { "successCount", _sortingSuccessCount },
                            { "failureCount", _sortingFailureCount }
                        }
                    });
                }
            }
        }
        else
        {
            // Clear alarm if rate is back to normal
            ClearAlarm(AlarmType.SortingFailureRateHigh, null);
        }
    }

    private void CheckQueueBacklog()
    {
        if (_currentQueueLength > QueueBacklogThreshold)
        {
            lock (_lock)
            {
                // Check if alarm already exists
                var existingAlarm = _activeAlarms.FirstOrDefault(a =>
                    a.Type == AlarmType.QueueBacklog);

                if (existingAlarm == null)
                {
                    TriggerAlarm(new AlarmEvent
                    {
                        Type = AlarmType.QueueBacklog,
                        Level = AlarmLevel.P2,
                        Message = $"队列积压超过阈值 / Queue backlog exceeds threshold: {_currentQueueLength} parcels",
                        Details = new Dictionary<string, object>
                        {
                            { "queueLength", _currentQueueLength },
                            { "threshold", QueueBacklogThreshold }
                        }
                    });
                }
            }
        }
        else
        {
            // Clear alarm if queue is back to normal
            ClearAlarm(AlarmType.QueueBacklog, null);
        }
    }

    private void TriggerAlarm(AlarmEvent alarm)
    {
        // Set triggered time using local time
        if (alarm.TriggeredAt == default)
        {
            alarm.TriggeredAt = _clock.LocalNow;
        }
        
        lock (_lock)
        {
            _activeAlarms.Add(alarm);
        }

        _logger.LogWarning(
            "[ALARM] Level={Level}, Type={Type}, Message={Message}",
            alarm.Level,
            alarm.Type,
            alarm.Message);
    }

    private void ClearAlarm(AlarmType type, string? identifier)
    {
        lock (_lock)
        {
            _activeAlarms.RemoveAll(a =>
            {
                if (a.Type != type)
                    return false;

                if (identifier == null)
                    return true;

                // Check if the alarm matches the identifier
                if (a.Details.ContainsKey("diverterId") && a.Details["diverterId"].ToString() == identifier)
                    return true;
                if (a.Details.ContainsKey("sensorId") && a.Details["sensorId"].ToString() == identifier)
                    return true;
                if (a.Details.ContainsKey("connectionType") && a.Details["connectionType"].ToString() == identifier)
                    return true;

                return false;
            });
        }
    }

    /// <summary>
    /// 重置分拣统计计数器 / Reset sorting statistics
    /// </summary>
    public void ResetSortingStatistics()
    {
        Interlocked.Exchange(ref _sortingSuccessCount, 0);
        Interlocked.Exchange(ref _sortingFailureCount, 0);
    }

    /// <summary>
    /// 获取当前分拣失败率 / Get current sorting failure rate
    /// </summary>
    public double GetSortingFailureRate()
    {
        var totalCount = _sortingSuccessCount + _sortingFailureCount;
        if (totalCount == 0)
            return 0;

        return (double)_sortingFailureCount / totalCount;
    }
}
