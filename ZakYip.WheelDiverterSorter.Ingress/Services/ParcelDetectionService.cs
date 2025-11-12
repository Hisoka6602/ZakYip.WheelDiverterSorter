using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZakYip.WheelDiverterSorter.Ingress.Configuration;
using ZakYip.WheelDiverterSorter.Ingress.Models;

namespace ZakYip.WheelDiverterSorter.Ingress.Services;

/// <summary>
/// 包裹检测服务
/// </summary>
/// <remarks>
/// 负责监听传感器事件，检测包裹到达，并生成唯一包裹ID
/// </remarks>
public class ParcelDetectionService : IParcelDetectionService, IDisposable
{
    private readonly IEnumerable<ISensor> _sensors;
    private readonly ILogger<ParcelDetectionService>? _logger;
    private readonly Services.ISensorHealthMonitor? _healthMonitor;
    private readonly ParcelDetectionOptions _options;
    private readonly Dictionary<string, DateTimeOffset> _lastTriggerTimes = new();
    private readonly Queue<long> _recentParcelIds = new();
    private readonly HashSet<long> _parcelIdSet = new();
    private readonly object _lockObject = new();
    private bool _isRunning;

    /// <summary>
    /// 包裹检测事件
    /// </summary>
    public event EventHandler<ParcelDetectedEventArgs>? ParcelDetected;

    /// <summary>
    /// 重复触发异常事件
    /// </summary>
    public event EventHandler<DuplicateTriggerEventArgs>? DuplicateTriggerDetected;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="sensors">传感器集合</param>
    /// <param name="options">包裹检测配置选项</param>
    /// <param name="logger">日志记录器</param>
    /// <param name="healthMonitor">传感器健康监控器（可选）</param>
    public ParcelDetectionService(
        IEnumerable<ISensor> sensors,
        IOptions<ParcelDetectionOptions>? options = null,
        ILogger<ParcelDetectionService>? logger = null,
        Services.ISensorHealthMonitor? healthMonitor = null)
    {
        _sensors = sensors ?? throw new ArgumentNullException(nameof(sensors));
        _options = options?.Value ?? new ParcelDetectionOptions();
        _logger = logger;
        _healthMonitor = healthMonitor;
    }

    /// <summary>
    /// 启动包裹检测服务
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_isRunning)
        {
            return;
        }

        _logger?.LogInformation("启动包裹检测服务");

        // 订阅所有传感器的事件
        foreach (var sensor in _sensors)
        {
            sensor.SensorTriggered += OnSensorTriggered;
            sensor.SensorError += OnSensorError;
            await sensor.StartAsync(cancellationToken);
            _logger?.LogInformation("传感器 {SensorId} ({Type}) 已启动", sensor.SensorId, sensor.Type);
        }

        // 启动健康监控
        if (_healthMonitor != null)
        {
            await _healthMonitor.StartAsync(cancellationToken);
            _logger?.LogInformation("传感器健康监控已启动");
        }

        _isRunning = true;
    }

    /// <summary>
    /// 停止包裹检测服务
    /// </summary>
    public async Task StopAsync()
    {
        if (!_isRunning)
        {
            return;
        }

        _logger?.LogInformation("停止包裹检测服务");

        // 停止健康监控
        if (_healthMonitor != null)
        {
            await _healthMonitor.StopAsync();
            _logger?.LogInformation("传感器健康监控已停止");
        }

        // 取消订阅并停止所有传感器
        foreach (var sensor in _sensors)
        {
            sensor.SensorTriggered -= OnSensorTriggered;
            sensor.SensorError -= OnSensorError;
            await sensor.StopAsync();
            _logger?.LogInformation("传感器 {SensorId} ({Type}) 已停止", sensor.SensorId, sensor.Type);
        }

        _isRunning = false;
    }

    /// <summary>
    /// 处理传感器触发事件
    /// </summary>
    private void OnSensorTriggered(object? sender, SensorEvent sensorEvent)
    {
        // 只处理物体遮挡事件（IsTriggered = true）
        if (!sensorEvent.IsTriggered)
        {
            return;
        }

        lock (_lockObject)
        {
            bool isDuplicate = false;
            double timeSinceLastTriggerMs = 0;

            // 检测是否为重复触发（去抖动检查）
            if (_lastTriggerTimes.TryGetValue(sensorEvent.SensorId, out var lastTime))
            {
                var timeSinceLastTrigger = sensorEvent.TriggerTime - lastTime;
                timeSinceLastTriggerMs = timeSinceLastTrigger.TotalMilliseconds;
                
                if (timeSinceLastTriggerMs < _options.DeduplicationWindowMs)
                {
                    isDuplicate = true;
                    _logger?.LogWarning(
                        "检测到重复触发: 传感器 {SensorId}, 距上次触发 {TimeSinceLastMs}ms",
                        sensorEvent.SensorId,
                        timeSinceLastTriggerMs);
                }
            }

            _lastTriggerTimes[sensorEvent.SensorId] = sensorEvent.TriggerTime;

            // 生成唯一包裹ID
            var parcelId = GenerateUniqueParcelId(sensorEvent);

            // 记录包裹ID到历史记录中
            AddParcelIdToHistory(parcelId);

            if (isDuplicate)
            {
                // 触发重复触发异常事件
                var duplicateEventArgs = new DuplicateTriggerEventArgs
                {
                    ParcelId = parcelId,
                    DetectedAt = sensorEvent.TriggerTime,
                    SensorId = sensorEvent.SensorId,
                    SensorType = sensorEvent.SensorType,
                    TimeSinceLastTriggerMs = timeSinceLastTriggerMs,
                    Reason = $"传感器在{_options.DeduplicationWindowMs}ms去重窗口内重复触发"
                };

                _logger?.LogWarning(
                    "包裹 {ParcelId} 标记为重复触发异常，传感器: {SensorId}，距上次触发: {TimeSinceLastMs}ms",
                    parcelId,
                    sensorEvent.SensorId,
                    timeSinceLastTriggerMs);

                DuplicateTriggerDetected?.Invoke(this, duplicateEventArgs);
            }

            // 无论是否重复触发，都触发包裹检测事件（业务层决定如何处理）
            _logger?.LogInformation(
                "检测到包裹 {ParcelId}，传感器: {SensorId} ({SensorType})，位置: {Position}{DuplicateFlag}",
                parcelId,
                sensorEvent.SensorId,
                sensorEvent.SensorType,
                sensorEvent.SensorId,
                isDuplicate ? " [重复触发]" : "");

            // 触发包裹检测事件
            var eventArgs = new ParcelDetectedEventArgs
            {
                ParcelId = parcelId,
                DetectedAt = sensorEvent.TriggerTime,
                SensorId = sensorEvent.SensorId,
                SensorType = sensorEvent.SensorType
            };

            ParcelDetected?.Invoke(this, eventArgs);
        }
    }

    /// <summary>
    /// 生成唯一包裹ID
    /// </summary>
    /// <param name="sensorEvent">传感器事件</param>
    /// <returns>唯一包裹ID (毫秒时间戳)</returns>
    private long GenerateUniqueParcelId(SensorEvent sensorEvent)
    {
        long parcelId;
        int attempts = 0;
        const int maxAttempts = 10;

        do
        {
            // 使用触发时间的毫秒时间戳作为包裹ID
            parcelId = sensorEvent.TriggerTime.ToUnixTimeMilliseconds();
            attempts++;

            // 检查是否已存在于历史记录中
            if (!_parcelIdSet.Contains(parcelId))
            {
                break;
            }

            // 如果ID已存在，增加1毫秒来生成新ID
            sensorEvent = sensorEvent with { TriggerTime = sensorEvent.TriggerTime.AddMilliseconds(1) };

            _logger?.LogWarning(
                "生成的包裹ID {ParcelId} 已存在，重新生成 (尝试 {Attempt}/{MaxAttempts})",
                parcelId,
                attempts,
                maxAttempts);

        } while (attempts < maxAttempts);

        return parcelId;
    }

    /// <summary>
    /// 将包裹ID添加到历史记录
    /// </summary>
    /// <param name="parcelId">包裹ID</param>
    private void AddParcelIdToHistory(long parcelId)
    {
        _parcelIdSet.Add(parcelId);
        _recentParcelIds.Enqueue(parcelId);

        // 如果超过最大历史记录数量，移除最旧的记录
        while (_recentParcelIds.Count > _options.ParcelIdHistorySize)
        {
            var oldestId = _recentParcelIds.Dequeue();
            _parcelIdSet.Remove(oldestId);
        }
    }

    /// <summary>
    /// 处理传感器错误事件
    /// </summary>
    private void OnSensorError(object? sender, SensorErrorEventArgs e)
    {
        _logger?.LogError(
            e.Exception,
            "传感器 {SensorId} 发生错误: {ErrorMessage}",
            e.SensorId,
            e.ErrorMessage);

        // 报告给健康监控器
        _healthMonitor?.ReportError(e.SensorId, e.ErrorMessage);
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        StopAsync().GetAwaiter().GetResult();

        foreach (var sensor in _sensors)
        {
            if (sensor is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}
