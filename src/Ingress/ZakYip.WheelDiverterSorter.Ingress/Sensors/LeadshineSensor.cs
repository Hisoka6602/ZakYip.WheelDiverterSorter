using ZakYip.WheelDiverterSorter.Core.Events.Sensor;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Ingress.Models;
using ZakYip.WheelDiverterSorter.Core.Hardware.Devices;
using ZakYip.WheelDiverterSorter.Core.Hardware.IoLinkage;
using ZakYip.WheelDiverterSorter.Core.Hardware.Mappings;
using ZakYip.WheelDiverterSorter.Core.Hardware.Ports;
using ZakYip.WheelDiverterSorter.Core.Hardware.Providers;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Observability.Utilities;

namespace ZakYip.WheelDiverterSorter.Ingress.Sensors;

/// <summary>
/// 通用雷赛（Leadshine）传感器
/// </summary>
/// <remarks>
/// 提供传感器通用功能的实现，通过构造函数参数指定传感器类型。
/// 此类合并了原来的 LeadshineLaserSensor 和 LeadshinePhotoelectricSensor 的功能。
/// </remarks>
public class LeadshineSensor : ISensor {
    private readonly ILogger _logger;
    private readonly ILogDeduplicator _logDeduplicator;
    private readonly IInputPort _inputPort;
    private readonly ISystemClock _systemClock;
    private readonly int _inputBit;
    private readonly string _sensorTypeName;
    private readonly int _pollingIntervalMs;
    private CancellationTokenSource? _cts;
    private Task? _monitoringTask;
    private bool _lastState;

    /// <summary>
    /// 传感器ID
    /// </summary>
    public long SensorId { get; }

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
    /// 构造函数
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="logDeduplicator">日志去重器</param>
    /// <param name="sensorId">传感器ID</param>
    /// <param name="type">传感器类型</param>
    /// <param name="inputPort">输入端口</param>
    /// <param name="inputBit">输入位索引</param>
    /// <param name="systemClock">系统时钟</param>
    /// <param name="pollingIntervalMs">传感器轮询间隔（毫秒），默认10ms</param>
    public LeadshineSensor(
        ILogger logger,
        ILogDeduplicator logDeduplicator,
        long sensorId,
        SensorType type,
        IInputPort inputPort,
        int inputBit,
        ISystemClock systemClock,
        int pollingIntervalMs = 10) {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _logDeduplicator = logDeduplicator ?? throw new ArgumentNullException(nameof(logDeduplicator));
        SensorId = sensorId;
        Type = type;
        _inputPort = inputPort ?? throw new ArgumentNullException(nameof(inputPort));
        _inputBit = inputBit;
        _systemClock = systemClock ?? throw new ArgumentNullException(nameof(systemClock));
        _pollingIntervalMs = pollingIntervalMs;
        _lastState = false;
        
        // 根据传感器类型设置名称（用于日志）
        _sensorTypeName = type switch {
            SensorType.Laser => "激光传感器",
            SensorType.Photoelectric => "光电传感器",
            _ => "传感器"
        };
    }

    /// <summary>
    /// 启动传感器监听
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken = default) {
        if (IsRunning) {
            return Task.CompletedTask;
        }

        _logger.LogInformation("启动雷赛{SensorTypeName} {SensorId}，输入位 {InputBit}", _sensorTypeName, SensorId, _inputBit);

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        IsRunning = true;

        // 启动监听任务
        _monitoringTask = Task.Run(() => MonitorInputAsync(_cts.Token), _cts.Token);

        return Task.CompletedTask;
    }

    /// <summary>
    /// 停止传感器监听
    /// </summary>
    public async Task StopAsync() {
        if (!IsRunning) {
            return;
        }

        _logger.LogInformation("停止雷赛{SensorTypeName} {SensorId}", _sensorTypeName, SensorId);

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
    /// 监听输入端口
    /// </summary>
    private async Task MonitorInputAsync(CancellationToken cancellationToken) {
        _logger.LogInformation("雷赛{SensorTypeName} {SensorId} 开始监听", _sensorTypeName, SensorId);

        while (!cancellationToken.IsCancellationRequested) {
            try {
                // 读取输入位状态
                var currentState = await _inputPort.ReadAsync(_inputBit);

                // 检测状态变化
                if (currentState != _lastState) {
                    _lastState = currentState;

                    // 触发事件
                    var sensorEvent = new SensorEvent {
                        SensorId = SensorId,
                        SensorType = Type,
                        TriggerTime = _systemClock.LocalNowOffset,
                        IsTriggered = currentState
                    };

                    OnSensorTriggered(sensorEvent);

                    // 使用去重日志记录，防止相同状态变化在短时间内重复输出
                    _logger.LogInformationDeduplicated(
                        _logDeduplicator,
                        "雷赛{SensorTypeName} {SensorId} 状态变化: {State}",
                        _sensorTypeName,
                        SensorId,
                        currentState ? "触发" : "解除");
                }

                // 使用配置的轮询间隔
                await Task.Delay(_pollingIntervalMs, cancellationToken);
            }
            catch (OperationCanceledException) {
                break;
            }
            catch (Exception ex) {
                _logger.LogError(ex, "雷赛{SensorTypeName} {SensorId} 读取失败", _sensorTypeName, SensorId);

                // 触发错误事件
                OnSensorError(new SensorErrorEventArgs {
                    SensorId = SensorId,
                    Type = Type,
                    ErrorMessage = $"读取输入位失败: {ex.Message}",
                    Exception = ex,
                    ErrorTime = _systemClock.LocalNowOffset
                });

                // 发生错误时等待一段时间再重试
                await Task.Delay(1000, cancellationToken);
            }
        }

        _logger.LogInformation("雷赛{SensorTypeName} {SensorId} 停止监听", _sensorTypeName, SensorId);
    }

    /// <summary>
    /// 触发传感器事件
    /// </summary>
    protected virtual void OnSensorTriggered(SensorEvent sensorEvent) {
        SensorTriggered.SafeInvoke(this, sensorEvent, _logger, nameof(SensorTriggered));
    }

    /// <summary>
    /// 触发传感器错误事件
    /// </summary>
    protected virtual void OnSensorError(SensorErrorEventArgs args) {
        SensorError.SafeInvoke(this, args, _logger, nameof(SensorError));
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
