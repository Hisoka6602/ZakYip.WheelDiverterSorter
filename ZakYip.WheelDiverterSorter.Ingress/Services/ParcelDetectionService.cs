using Microsoft.Extensions.Logging;
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
    private readonly Dictionary<string, DateTimeOffset> _lastTriggerTimes = new();
    private readonly object _lockObject = new();
    private bool _isRunning;

    /// <summary>
    /// 包裹检测事件
    /// </summary>
    public event EventHandler<ParcelDetectedEventArgs>? ParcelDetected;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="sensors">传感器集合</param>
    /// <param name="logger">日志记录器</param>
    /// <param name="healthMonitor">传感器健康监控器（可选）</param>
    public ParcelDetectionService(
        IEnumerable<ISensor> sensors,
        ILogger<ParcelDetectionService>? logger = null,
        Services.ISensorHealthMonitor? healthMonitor = null)
    {
        _sensors = sensors ?? throw new ArgumentNullException(nameof(sensors));
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
                if (timeSinceLastTrigger.TotalMilliseconds < 1000) // 1秒内的重复触发忽略
                {
                    return;
                }
            }

            _lastTriggerTimes[sensorEvent.SensorId] = sensorEvent.TriggerTime;
        }

        // 生成唯一包裹ID
        var parcelId = GenerateParcelId(sensorEvent);

        _logger?.LogInformation(
            "检测到包裹 {ParcelId}，传感器: {SensorId} ({SensorType})",
            parcelId,
            sensorEvent.SensorId,
            sensorEvent.SensorType);

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

    /// <summary>
    /// 生成唯一包裹ID
    /// </summary>
    /// <param name="sensorEvent">传感器事件</param>
    /// <returns>唯一包裹ID</returns>
    private static string GenerateParcelId(SensorEvent sensorEvent)
    {
        // 格式: PKG_{时间戳}_{传感器ID后缀}_{随机数}
        var timestamp = sensorEvent.TriggerTime.ToUnixTimeMilliseconds();
        var sensorSuffix = sensorEvent.SensorId.Length > 3 
            ? sensorEvent.SensorId[^3..] 
            : sensorEvent.SensorId;
        var random = new Random().Next(1000, 9999);

        return $"PKG_{timestamp}_{sensorSuffix}_{random}";
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
