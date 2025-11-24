using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.LineModel;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;


using ZakYip.WheelDiverterSorter.Core.LineModel.Segments;namespace ZakYip.WheelDiverterSorter.Drivers.Vendors.Simulated;

/// <summary>
/// 仿真中段皮带段驱动。
/// 不访问真实 IO，在内存中模拟皮带段的启停和状态反馈。
/// </summary>
public sealed class SimulatedConveyorSegmentDriver : IConveyorSegmentDriver
{
    private readonly ILogger<SimulatedConveyorSegmentDriver> _logger;
    private bool _startSignalActive;
    private bool _stopSignalActive;
    private bool _simulatedFault;
    private bool _simulatedRunning;
    private DateTimeOffset? _startCommandTime;

    public ConveyorIoMapping Mapping { get; }

    public SimulatedConveyorSegmentDriver(
        ConveyorIoMapping mapping,
        ILogger<SimulatedConveyorSegmentDriver> logger)
    {
        Mapping = mapping ?? throw new ArgumentNullException(nameof(mapping));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task WriteStartSignalAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("仿真：启动中段皮带段 [{SegmentKey}]", Mapping.SegmentKey);
        _startSignalActive = true;
        _stopSignalActive = false;
        _startCommandTime = DateTimeOffset.Now;

        // 模拟启动延迟：短暂延迟后自动设置为运行状态
        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
            if (_startSignalActive && !_simulatedFault)
            {
                _simulatedRunning = true;
                _logger.LogInformation("仿真：中段皮带段 [{SegmentKey}] 已进入运行状态", Mapping.SegmentKey);
            }
        }, cancellationToken);

        return Task.CompletedTask;
    }

    public Task WriteStopSignalAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("仿真：停止中段皮带段 [{SegmentKey}]", Mapping.SegmentKey);
        _stopSignalActive = true;
        _startSignalActive = false;

        // 模拟停止延迟：短暂延迟后自动设置为停止状态
        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
            if (_stopSignalActive)
            {
                _simulatedRunning = false;
                _logger.LogInformation("仿真：中段皮带段 [{SegmentKey}] 已停止", Mapping.SegmentKey);
            }
        }, cancellationToken);

        return Task.CompletedTask;
    }

    public Task<bool> ReadFaultStatusAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_simulatedFault);
    }

    public Task<bool> ReadRunningStatusAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_simulatedRunning);
    }

    public Task ResetAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("仿真：复位中段皮带段 [{SegmentKey}]", Mapping.SegmentKey);
        _startSignalActive = false;
        _stopSignalActive = false;
        _simulatedRunning = false;
        _startCommandTime = null;
        return Task.CompletedTask;
    }

    /// <summary>
    /// 注入故障状态（仅用于测试）
    /// </summary>
    public void InjectFault(bool hasFault)
    {
        _simulatedFault = hasFault;
        if (hasFault)
        {
            _simulatedRunning = false;
            _logger.LogWarning("仿真：中段皮带段 [{SegmentKey}] 发生故障", Mapping.SegmentKey);
        }
    }

    /// <summary>
    /// 设置运行状态（仅用于测试）
    /// </summary>
    public void SetRunningState(bool isRunning)
    {
        _simulatedRunning = isRunning;
    }
}
