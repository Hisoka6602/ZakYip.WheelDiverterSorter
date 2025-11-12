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
    private readonly Queue<string> _recentParcelIds = new();
    private readonly HashSet<string> _parcelIdSet = new();
    private readonly object _lockObject = new();
    private readonly Random _random = new();
    private bool _isRunning;

    /// <summary>
    /// 包裹检测事件
    /// </summary>
    public event EventHandler<ParcelDetectedEventArgs>? ParcelDetected;

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
            // 防止短时间内重复触发（去抖动）
            if (_lastTriggerTimes.TryGetValue(sensorEvent.SensorId, out var lastTime))
            {
                var timeSinceLastTrigger = sensorEvent.TriggerTime - lastTime;
                if (timeSinceLastTrigger.TotalMilliseconds < _options.DeduplicationWindowMs)
                {
                    _logger?.LogDebug(
                        "忽略重复触发: 传感器 {SensorId}, 距上次触发 {TimeSinceLastMs}ms",
                        sensorEvent.SensorId,
                        timeSinceLastTrigger.TotalMilliseconds);
                    return;
                }
            }

            _lastTriggerTimes[sensorEvent.SensorId] = sensorEvent.TriggerTime;

            // 生成唯一包裹ID，并确保不重复
            var parcelId = GenerateUniqueParcelId(sensorEvent);

            // 记录包裹ID到历史记录中
            AddParcelIdToHistory(parcelId);

            _logger?.LogInformation(
                "检测到包裹 {ParcelId}，传感器: {SensorId} ({SensorType})，位置: {Position}",
                parcelId,
                sensorEvent.SensorId,
                sensorEvent.SensorType,
                sensorEvent.SensorId);

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
    /// <returns>唯一包裹ID</returns>
    private string GenerateUniqueParcelId(SensorEvent sensorEvent)
    {
        string parcelId;
        int attempts = 0;
        const int maxAttempts = 10;

        do
        {
            // 格式: PKG_{时间戳}_{传感器ID后缀}_{随机数}
            var timestamp = sensorEvent.TriggerTime.ToUnixTimeMilliseconds();
            var sensorSuffix = sensorEvent.SensorId.Length > 3 
                ? sensorEvent.SensorId[^3..] 
                : sensorEvent.SensorId;
            var random = _random.Next(1000, 9999);

            parcelId = $"PKG_{timestamp}_{sensorSuffix}_{random}";
            attempts++;

            // 检查是否已存在于历史记录中
            if (!_parcelIdSet.Contains(parcelId))
            {
                break;
            }

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
    private void AddParcelIdToHistory(string parcelId)
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
