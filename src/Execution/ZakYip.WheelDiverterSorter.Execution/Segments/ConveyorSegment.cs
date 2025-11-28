using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.LineModel;
using ZakYip.WheelDiverterSorter.Core.Enums;


using ZakYip.WheelDiverterSorter.Core.LineModel.Segments;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;

namespace ZakYip.WheelDiverterSorter.Execution.Segments;

/// <summary>
/// 中段皮带段实现。
/// 封装单个中段皮带段的启停逻辑和状态管理，内部组合驱动层接口。
/// </summary>
public sealed class ConveyorSegment : IConveyorSegment
{
    private readonly IConveyorSegmentDriver _driver;
    private readonly ILogger<ConveyorSegment> _logger;
    private readonly ISystemClock _clock;
    private ConveyorSegmentState _state;
    private string? _faultInfo;
    private readonly object _stateLock = new();

    public ConveyorSegmentId SegmentId { get; }

    public ConveyorSegmentState State
    {
        get
        {
            lock (_stateLock)
            {
                return _state;
            }
        }
        private set
        {
            lock (_stateLock)
            {
                _state = value;
            }
        }
    }

    public ConveyorSegment(
        IConveyorSegmentDriver driver,
        ILogger<ConveyorSegment> logger,
        ISystemClock clock)
    {
        _driver = driver ?? throw new ArgumentNullException(nameof(driver));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));

        SegmentId = new ConveyorSegmentId
        {
            Key = driver.Mapping.SegmentKey,
            DisplayName = driver.Mapping.DisplayName,
            Priority = driver.Mapping.Priority
        };

        _state = ConveyorSegmentState.Stopped;
    }

    public async ValueTask<ConveyorOperationResult> StartAsync(CancellationToken cancellationToken = default)
    {
        if (State == ConveyorSegmentState.Running)
        {
            _logger.LogDebug("皮带段 [{SegmentKey}] 已在运行中，跳过启动操作", SegmentId.Key);
            return ConveyorOperationResult.Success(SegmentId);
        }

        if (State == ConveyorSegmentState.Fault)
        {
            var msg = $"皮带段 [{SegmentId.Key}] 处于故障状态，无法启动";
            _logger.LogWarning(msg);
            return ConveyorOperationResult.Failure(msg, SegmentId);
        }

        try
        {
            State = ConveyorSegmentState.Starting;
            _logger.LogInformation("开始启动皮带段 [{SegmentKey}]", SegmentId.Key);

            await _driver.WriteStartSignalAsync(cancellationToken);

            // 等待运行反馈（如果配置了运行反馈点位）
            var startTime = _clock.LocalNowOffset;
            var timeout = TimeSpan.FromMilliseconds(_driver.Mapping.StartTimeoutMs);

            while (_clock.LocalNowOffset - startTime < timeout)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // 检查故障
                var hasFault = await _driver.ReadFaultStatusAsync(cancellationToken);
                if (hasFault)
                {
                    State = ConveyorSegmentState.Fault;
                    _faultInfo = $"启动过程中检测到故障信号";
                    _logger.LogError("皮带段 [{SegmentKey}] 启动失败: {FaultInfo}", SegmentId.Key, _faultInfo);
                    return ConveyorOperationResult.Failure(_faultInfo, SegmentId);
                }

                // 如果配置了运行反馈，检查是否已运行
                if (_driver.Mapping.RunningInputChannel.HasValue)
                {
                    var isRunning = await _driver.ReadRunningStatusAsync(cancellationToken);
                    if (isRunning)
                    {
                        State = ConveyorSegmentState.Running;
                        _logger.LogInformation("皮带段 [{SegmentKey}] 已成功启动并运行", SegmentId.Key);
                        return ConveyorOperationResult.Success(SegmentId);
                    }
                }
                else
                {
                    // 没有运行反馈，短暂延迟后假定启动成功
                    await Task.Delay(100, cancellationToken);
                    State = ConveyorSegmentState.Running;
                    _logger.LogInformation("皮带段 [{SegmentKey}] 已发送启动信号（无运行反馈配置）", SegmentId.Key);
                    return ConveyorOperationResult.Success(SegmentId);
                }

                await Task.Delay(200, cancellationToken);
            }

            // 超时：如果没有运行反馈配置，假定成功；否则视为故障
            if (!_driver.Mapping.RunningInputChannel.HasValue)
            {
                State = ConveyorSegmentState.Running;
                _logger.LogWarning("皮带段 [{SegmentKey}] 启动超时，但无运行反馈配置，假定成功", SegmentId.Key);
                return ConveyorOperationResult.Success(SegmentId);
            }

            State = ConveyorSegmentState.Fault;
            _faultInfo = $"启动超时（{_driver.Mapping.StartTimeoutMs}ms）未收到运行反馈";
            _logger.LogError("皮带段 [{SegmentKey}] 启动超时: {FaultInfo}", SegmentId.Key, _faultInfo);
            return ConveyorOperationResult.Failure(_faultInfo, SegmentId);
        }
        catch (Exception ex)
        {
            State = ConveyorSegmentState.Fault;
            _faultInfo = $"启动异常: {ex.Message}";
            _logger.LogError(ex, "皮带段 [{SegmentKey}] 启动时发生异常", SegmentId.Key);
            return ConveyorOperationResult.Failure(_faultInfo, SegmentId);
        }
    }

    public async ValueTask<ConveyorOperationResult> StopAsync(CancellationToken cancellationToken = default)
    {
        if (State == ConveyorSegmentState.Stopped)
        {
            _logger.LogDebug("皮带段 [{SegmentKey}] 已停止，跳过停止操作", SegmentId.Key);
            return ConveyorOperationResult.Success(SegmentId);
        }

        try
        {
            State = ConveyorSegmentState.Stopping;
            _logger.LogInformation("开始停止皮带段 [{SegmentKey}]", SegmentId.Key);

            await _driver.WriteStopSignalAsync(cancellationToken);

            // 等待停止完成（如果配置了运行反馈点位）
            var stopTime = _clock.LocalNowOffset;
            var timeout = TimeSpan.FromMilliseconds(_driver.Mapping.StopTimeoutMs);

            while (_clock.LocalNowOffset - stopTime < timeout)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (_driver.Mapping.RunningInputChannel.HasValue)
                {
                    var isRunning = await _driver.ReadRunningStatusAsync(cancellationToken);
                    if (!isRunning)
                    {
                        State = ConveyorSegmentState.Stopped;
                        _faultInfo = null;
                        _logger.LogInformation("皮带段 [{SegmentKey}] 已成功停止", SegmentId.Key);
                        return ConveyorOperationResult.Success(SegmentId);
                    }
                }
                else
                {
                    // 没有运行反馈，短暂延迟后假定停止成功
                    await Task.Delay(100, cancellationToken);
                    State = ConveyorSegmentState.Stopped;
                    _faultInfo = null;
                    _logger.LogInformation("皮带段 [{SegmentKey}] 已发送停止信号（无运行反馈配置）", SegmentId.Key);
                    return ConveyorOperationResult.Success(SegmentId);
                }

                await Task.Delay(200, cancellationToken);
            }

            // 超时：假定已停止
            State = ConveyorSegmentState.Stopped;
            _faultInfo = null;
            _logger.LogWarning("皮带段 [{SegmentKey}] 停止超时，但假定已停止", SegmentId.Key);
            return ConveyorOperationResult.Success(SegmentId);
        }
        catch (Exception ex)
        {
            _faultInfo = $"停止异常: {ex.Message}";
            _logger.LogError(ex, "皮带段 [{SegmentKey}] 停止时发生异常", SegmentId.Key);
            return ConveyorOperationResult.Failure(_faultInfo, SegmentId);
        }
    }

    public string? GetFaultInfo()
    {
        return _faultInfo;
    }
}
