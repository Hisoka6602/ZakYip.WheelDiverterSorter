using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Enums.Communication;
using ZakYip.WheelDiverterSorter.Core.Enums.Conveyor;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Enums.Routing;
using ZakYip.WheelDiverterSorter.Core.Enums.Sensors;
using ZakYip.WheelDiverterSorter.Core.Enums.System;
using ZakYip.WheelDiverterSorter.Ingress.Models;

namespace ZakYip.WheelDiverterSorter.Ingress.Services;

/// <summary>
/// 传感器健康监控服务实现
/// </summary>
/// <remarks>
/// 监控传感器的健康状态，检测长时间无响应、读取错误等故障情况
/// </remarks>
public class SensorHealthMonitor : ISensorHealthMonitor, IDisposable {
    private readonly IEnumerable<ISensor> _sensors;
    private readonly ILogger<SensorHealthMonitor>? _logger;
    private readonly Dictionary<string, SensorHealthStatus> _healthStatus = new();
    private readonly Dictionary<string, DateTimeOffset> _faultStartTimes = new();
    private readonly object _lockObject = new();
    private CancellationTokenSource? _cts;
    private Task? _monitoringTask;
    private bool _isRunning;

    // 配置参数
    private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(5);

    private readonly TimeSpan _noResponseThreshold = TimeSpan.FromSeconds(60);
    private readonly int _errorThreshold = 3;

    /// <summary>
    /// 传感器故障事件
    /// </summary>
    public event EventHandler<SensorFaultEventArgs>? SensorFault;

    /// <summary>
    /// 传感器恢复事件
    /// </summary>
    public event EventHandler<SensorRecoveryEventArgs>? SensorRecovery;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="sensors">传感器集合</param>
    /// <param name="logger">日志记录器</param>
    public SensorHealthMonitor(
        IEnumerable<ISensor> sensors,
        ILogger<SensorHealthMonitor>? logger = null) {
        _sensors = sensors ?? throw new ArgumentNullException(nameof(sensors));
        _logger = logger;

        // 初始化健康状态
        foreach (var sensor in _sensors) {
            _healthStatus[sensor.SensorId] = new SensorHealthStatus {
                SensorId = sensor.SensorId,
                Type = sensor.Type,
                IsHealthy = true,
                LastCheckTime = DateTimeOffset.UtcNow
            };

            // 订阅传感器事件
            sensor.SensorTriggered += OnSensorTriggered;
        }
    }

    /// <summary>
    /// 启动健康监控
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken = default) {
        if (_isRunning) {
            return Task.CompletedTask;
        }

        _logger?.LogInformation("启动传感器健康监控服务");

        lock (_lockObject) {
            foreach (var status in _healthStatus.Values) {
                status.StartTime = DateTimeOffset.UtcNow;
            }
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _isRunning = true;

        // 启动监控任务
        _monitoringTask = Task.Run(() => MonitorHealthAsync(_cts.Token), _cts.Token);

        return Task.CompletedTask;
    }

    /// <summary>
    /// 停止健康监控
    /// </summary>
    public async Task StopAsync() {
        if (!_isRunning) {
            return;
        }

        _logger?.LogInformation("停止传感器健康监控服务");

        _cts?.Cancel();
        _isRunning = false;

        if (_monitoringTask != null) {
            try {
                await _monitoringTask;
            }
            catch (OperationCanceledException) {
                // 预期的取消异常
            }
        }
    }

    /// <summary>
    /// 获取传感器健康状态
    /// </summary>
    public SensorHealthStatus GetHealthStatus(string sensorId) {
        lock (_lockObject) {
            if (_healthStatus.TryGetValue(sensorId, out var status)) {
                // 更新运行时长
                if (status.StartTime.HasValue) {
                    status.UptimeSeconds = (DateTimeOffset.UtcNow - status.StartTime.Value).TotalSeconds;
                }
                return status;
            }

            throw new ArgumentException($"传感器 {sensorId} 不存在", nameof(sensorId));
        }
    }

    /// <summary>
    /// 获取所有传感器的健康状态
    /// </summary>
    public IDictionary<string, SensorHealthStatus> GetAllHealthStatus() {
        lock (_lockObject) {
            // 更新所有传感器的运行时长
            foreach (var status in _healthStatus.Values) {
                if (status.StartTime.HasValue) {
                    status.UptimeSeconds = (DateTimeOffset.UtcNow - status.StartTime.Value).TotalSeconds;
                }
            }

            return new Dictionary<string, SensorHealthStatus>(_healthStatus);
        }
    }

    /// <summary>
    /// 手动报告传感器错误
    /// </summary>
    public void ReportError(string sensorId, string error) {
        lock (_lockObject) {
            if (_healthStatus.TryGetValue(sensorId, out var status)) {
                status.ErrorCount++;
                status.LastError = error;
                status.LastErrorTime = DateTimeOffset.UtcNow;
                status.LastCheckTime = DateTimeOffset.UtcNow;

                _logger?.LogWarning(
                    "传感器 {SensorId} 报告错误 (第{Count}次): {Error}",
                    sensorId,
                    status.ErrorCount,
                    error);

                // 检查是否达到故障阈值
                if (status.IsHealthy && status.ErrorCount >= _errorThreshold) {
                    MarkSensorAsFaulty(sensorId, SensorFaultType.ReadError, error);
                }
            }
        }
    }

    /// <summary>
    /// 处理传感器触发事件
    /// </summary>
    private void OnSensorTriggered(object? sender, SensorEvent sensorEvent) {
        lock (_lockObject) {
            if (_healthStatus.TryGetValue(sensorEvent.SensorId, out var status)) {
                status.LastTriggerTime = sensorEvent.TriggerTime;
                status.TotalTriggerCount++;
                status.LastCheckTime = DateTimeOffset.UtcNow;

                // 如果传感器之前处于故障状态，现在恢复了
                if (!status.IsHealthy) {
                    MarkSensorAsRecovered(sensorEvent.SensorId);
                }

                // 重置错误计数
                if (status.ErrorCount > 0) {
                    _logger?.LogDebug("传感器 {SensorId} 正常触发，重置错误计数", sensorEvent.SensorId);
                    status.ErrorCount = 0;
                }
            }
        }
    }

    /// <summary>
    /// 监控健康状态
    /// </summary>
    private async Task MonitorHealthAsync(CancellationToken cancellationToken) {
        _logger?.LogInformation("传感器健康监控任务已启动");

        while (!cancellationToken.IsCancellationRequested) {
            try {
                await Task.Delay(_checkInterval, cancellationToken);

                lock (_lockObject) {
                    var now = DateTimeOffset.UtcNow;

                    foreach (var status in _healthStatus.Values) {
                        status.LastCheckTime = now;

                        // 检查是否长时间无响应
                        if (status.IsHealthy && status.LastTriggerTime.HasValue) {
                            var timeSinceLastTrigger = now - status.LastTriggerTime.Value;
                            if (timeSinceLastTrigger > _noResponseThreshold) {
                                _logger?.LogWarning(
                                    "传感器 {SensorId} 已 {Seconds} 秒未响应",
                                    status.SensorId,
                                    timeSinceLastTrigger.TotalSeconds);

                                // 注意：长时间无响应可能是正常的（没有包裹经过）
                                // 这里只记录警告，不标记为故障
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException) {
                break;
            }
            catch (Exception ex) {
                _logger?.LogError(ex, "健康监控任务发生异常");
            }
        }

        _logger?.LogInformation("传感器健康监控任务已停止");
    }

    /// <summary>
    /// 标记传感器为故障状态
    /// </summary>
    private void MarkSensorAsFaulty(string sensorId, SensorFaultType faultType, string description) {
        if (_healthStatus.TryGetValue(sensorId, out var status)) {
            status.IsHealthy = false;
            _faultStartTimes[sensorId] = DateTimeOffset.UtcNow;

            _logger?.LogError(
                "传感器 {SensorId} 进入故障状态: {FaultType} - {Description}",
                sensorId,
                faultType,
                description);

            // 触发故障事件
            SensorFault?.Invoke(this, new SensorFaultEventArgs {
                SensorId = sensorId,
                Type = status.Type,
                FaultType = faultType,
                Description = description,
                FaultTime = DateTimeOffset.UtcNow
            });
        }
    }

    /// <summary>
    /// 标记传感器为恢复状态
    /// </summary>
    private void MarkSensorAsRecovered(string sensorId) {
        if (_healthStatus.TryGetValue(sensorId, out var status) && !status.IsHealthy) {
            status.IsHealthy = true;
            status.ErrorCount = 0;

            var faultDuration = 0.0;
            if (_faultStartTimes.TryGetValue(sensorId, out var faultStartTime)) {
                faultDuration = (DateTimeOffset.UtcNow - faultStartTime).TotalSeconds;
                _faultStartTimes.Remove(sensorId);
            }

            _logger?.LogInformation(
                "传感器 {SensorId} 已恢复正常 (故障持续 {Seconds} 秒)",
                sensorId,
                faultDuration);

            // 触发恢复事件
            SensorRecovery?.Invoke(this, new SensorRecoveryEventArgs {
                SensorId = sensorId,
                Type = status.Type,
                RecoveryTime = DateTimeOffset.UtcNow,
                FaultDurationSeconds = faultDuration
            });
        }
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose() {
        StopAsync().GetAwaiter().GetResult();

        // 取消订阅传感器事件
        foreach (var sensor in _sensors) {
            sensor.SensorTriggered -= OnSensorTriggered;
        }

        _cts?.Dispose();
    }
}