using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Ingress.Models;

namespace ZakYip.WheelDiverterSorter.Ingress.Sensors;

/// <summary>
/// 模拟传感器基类
/// </summary>
/// <remarks>
/// 用于测试和调试，模拟真实传感器的行为
/// </remarks>
public abstract class MockSensorBase : ISensor {
    private CancellationTokenSource? _cts;
    private Task? _monitoringTask;

    /// <summary>
    /// 传感器ID
    /// </summary>
    public string SensorId { get; }

    /// <summary>
    /// 传感器类型
    /// </summary>
    public SensorType Type { get; }

    /// <summary>
    /// 传感器是否正在运行
    /// </summary>
    public bool IsRunning { get; private set; }

    /// <summary>
    /// 传感器事件触发时发生
    /// </summary>
    public event EventHandler<SensorEvent>? SensorTriggered;

    /// <summary>
    /// 传感器错误事件
    /// </summary>
    public event EventHandler<SensorErrorEventArgs>? SensorError;

    /// <summary>
    /// 模拟触发最小间隔（毫秒）
    /// </summary>
    protected int MinTriggerIntervalMs { get; }

    /// <summary>
    /// 模拟触发最大间隔（毫秒）
    /// </summary>
    protected int MaxTriggerIntervalMs { get; }

    /// <summary>
    /// 模拟包裹通过最小时间（毫秒）
    /// </summary>
    protected int MinParcelPassTimeMs { get; }

    /// <summary>
    /// 模拟包裹通过最大时间（毫秒）
    /// </summary>
    protected int MaxParcelPassTimeMs { get; }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="sensorId">传感器ID</param>
    /// <param name="type">传感器类型</param>
    /// <param name="minTriggerIntervalMs">模拟触发最小间隔（毫秒）</param>
    /// <param name="maxTriggerIntervalMs">模拟触发最大间隔（毫秒）</param>
    /// <param name="minParcelPassTimeMs">模拟包裹通过最小时间（毫秒）</param>
    /// <param name="maxParcelPassTimeMs">模拟包裹通过最大时间（毫秒）</param>
    protected MockSensorBase(
        string sensorId,
        SensorType type,
        int minTriggerIntervalMs = 5000,
        int maxTriggerIntervalMs = 15000,
        int minParcelPassTimeMs = 200,
        int maxParcelPassTimeMs = 500) {
        SensorId = sensorId ?? throw new ArgumentNullException(nameof(sensorId));
        Type = type;
        MinTriggerIntervalMs = minTriggerIntervalMs;
        MaxTriggerIntervalMs = maxTriggerIntervalMs;
        MinParcelPassTimeMs = minParcelPassTimeMs;
        MaxParcelPassTimeMs = maxParcelPassTimeMs;
    }

    /// <summary>
    /// 启动传感器监听
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken = default) {
        if (IsRunning) {
            return Task.CompletedTask;
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        IsRunning = true;

        // 启动模拟监听任务
        _monitoringTask = Task.Run(() => SimulateMonitoringAsync(_cts.Token), _cts.Token);

        return Task.CompletedTask;
    }

    /// <summary>
    /// 停止传感器监听
    /// </summary>
    public async Task StopAsync() {
        if (!IsRunning) {
            return;
        }

        _cts?.Cancel();
        IsRunning = false;

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
    /// 模拟传感器监听
    /// </summary>
    protected virtual async Task SimulateMonitoringAsync(CancellationToken cancellationToken) {
        var random = new Random();

        while (!cancellationToken.IsCancellationRequested) {
            // 模拟随机触发事件（使用配置的间隔）
            await Task.Delay(random.Next(MinTriggerIntervalMs, MaxTriggerIntervalMs), cancellationToken);

            if (cancellationToken.IsCancellationRequested) {
                break;
            }

            // 触发事件：物体遮挡
            OnSensorTriggered(new SensorEvent {
                SensorId = SensorId,
                SensorType = Type,
                TriggerTime = DateTimeOffset.UtcNow,
                IsTriggered = true
            });

            // 模拟包裹通过的时间（使用配置的时间）
            await Task.Delay(random.Next(MinParcelPassTimeMs, MaxParcelPassTimeMs), cancellationToken);

            // 触发事件：遮挡解除
            OnSensorTriggered(new SensorEvent {
                SensorId = SensorId,
                SensorType = Type,
                TriggerTime = DateTimeOffset.UtcNow,
                IsTriggered = false
            });
        }
    }

    /// <summary>
    /// 触发传感器事件
    /// </summary>
    protected virtual void OnSensorTriggered(SensorEvent sensorEvent) {
        SensorTriggered?.Invoke(this, sensorEvent);
    }

    /// <summary>
    /// 触发传感器错误事件
    /// </summary>
    protected virtual void OnSensorError(SensorErrorEventArgs args) {
        SensorError?.Invoke(this, args);
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose() {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    protected virtual void Dispose(bool disposing) {
        if (disposing) {
            StopAsync().GetAwaiter().GetResult();
            _cts?.Dispose();
        }
    }
}