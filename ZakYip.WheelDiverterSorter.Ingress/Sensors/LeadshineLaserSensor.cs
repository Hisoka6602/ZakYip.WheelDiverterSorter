using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Drivers.Abstractions;
using ZakYip.WheelDiverterSorter.Ingress.Models;

namespace ZakYip.WheelDiverterSorter.Ingress.Sensors;

/// <summary>
/// 基于雷赛（Leadshine）控制器的真实激光传感器
/// </summary>
/// <remarks>
/// 通过读取雷赛控制器的IO输入端口来检测包裹通过
/// </remarks>
public class LeadshineLaserSensor : ISensor
{
    private readonly ILogger<LeadshineLaserSensor> _logger;
    private readonly IInputPort _inputPort;
    private readonly int _inputBit;
    private CancellationTokenSource? _cts;
    private Task? _monitoringTask;
    private bool _lastState;

    /// <summary>
    /// 传感器ID
    /// </summary>
    public string SensorId { get; }

    /// <summary>
    /// 传感器类型
    /// </summary>
    public SensorType Type => SensorType.Laser;

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
    /// 构造函数
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="sensorId">传感器ID</param>
    /// <param name="inputPort">输入端口</param>
    /// <param name="inputBit">输入位索引</param>
    public LeadshineLaserSensor(
        ILogger<LeadshineLaserSensor> logger,
        string sensorId,
        IInputPort inputPort,
        int inputBit)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        SensorId = sensorId ?? throw new ArgumentNullException(nameof(sensorId));
        _inputPort = inputPort ?? throw new ArgumentNullException(nameof(inputPort));
        _inputBit = inputBit;
        _lastState = false;
    }

    /// <summary>
    /// 启动传感器监听
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (IsRunning)
        {
            return Task.CompletedTask;
        }

        _logger.LogInformation("启动雷赛激光传感器 {SensorId}，输入位 {InputBit}", SensorId, _inputBit);

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        IsRunning = true;

        // 启动监听任务
        _monitoringTask = Task.Run(() => MonitorInputAsync(_cts.Token), _cts.Token);

        return Task.CompletedTask;
    }

    /// <summary>
    /// 停止传感器监听
    /// </summary>
    public async Task StopAsync()
    {
        if (!IsRunning)
        {
            return;
        }

        _logger.LogInformation("停止雷赛激光传感器 {SensorId}", SensorId);

        _cts?.Cancel();
        IsRunning = false;

        if (_monitoringTask != null)
        {
            try
            {
                await _monitoringTask;
            }
            catch (OperationCanceledException)
            {
                // 预期的取消异常
            }
        }
    }

    /// <summary>
    /// 监听输入端口
    /// </summary>
    private async Task MonitorInputAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("雷赛激光传感器 {SensorId} 开始监听", SensorId);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // 读取输入位状态
                var currentState = await _inputPort.ReadAsync(_inputBit);

                // 检测状态变化
                if (currentState != _lastState)
                {
                    _lastState = currentState;

                    // 触发事件
                    var sensorEvent = new SensorEvent
                    {
                        SensorId = SensorId,
                        SensorType = Type,
                        TriggerTime = DateTimeOffset.UtcNow,
                        IsTriggered = currentState
                    };

                    OnSensorTriggered(sensorEvent);

                    _logger.LogDebug(
                        "雷赛激光传感器 {SensorId} 状态变化: {State}",
                        SensorId,
                        currentState ? "触发" : "解除");
                }

                // 短暂延迟以避免过度轮询（根据实际需求调整）
                await Task.Delay(10, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "雷赛激光传感器 {SensorId} 读取失败", SensorId);
                
                // 触发错误事件
                OnSensorError(new SensorErrorEventArgs
                {
                    SensorId = SensorId,
                    Type = Type,
                    ErrorMessage = $"读取输入位失败: {ex.Message}",
                    Exception = ex
                });

                // 发生错误时等待一段时间再重试
                await Task.Delay(1000, cancellationToken);
            }
        }

        _logger.LogInformation("雷赛激光传感器 {SensorId} 停止监听", SensorId);
    }

    /// <summary>
    /// 触发传感器事件
    /// </summary>
    protected virtual void OnSensorTriggered(SensorEvent sensorEvent)
    {
        SensorTriggered?.Invoke(this, sensorEvent);
    }

    /// <summary>
    /// 触发传感器错误事件
    /// </summary>
    protected virtual void OnSensorError(SensorErrorEventArgs args)
    {
        SensorError?.Invoke(this, args);
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            StopAsync().GetAwaiter().GetResult();
            _cts?.Dispose();
        }
    }
}
