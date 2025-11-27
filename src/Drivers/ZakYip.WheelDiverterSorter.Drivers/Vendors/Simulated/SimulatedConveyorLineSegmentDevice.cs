using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Hardware;
using ZakYip.WheelDiverterSorter.Core.Results;

namespace ZakYip.WheelDiverterSorter.Drivers.Vendors.Simulated;

/// <summary>
/// 模拟线体段设备实现 - HAL实现
/// Simulated Conveyor Line Segment Device - HAL Implementation
/// </summary>
/// <remarks>
/// 此类实现 <see cref="IConveyorLineSegmentDevice"/> HAL接口，
/// 提供线体段控制的内存模拟实现，用于测试和仿真环境。
/// </remarks>
public sealed class SimulatedConveyorLineSegmentDevice : IConveyorLineSegmentDevice
{
    private readonly ILogger<SimulatedConveyorLineSegmentDevice>? _logger;
    private ConveyorSegmentState _currentState = ConveyorSegmentState.Stopped;
    private decimal _currentSpeed;
    private decimal _targetSpeed;

    /// <inheritdoc/>
    public string SegmentId { get; }

    /// <summary>
    /// 创建模拟线体段设备
    /// </summary>
    /// <param name="segmentId">线体段标识</param>
    /// <param name="logger">日志记录器</param>
    public SimulatedConveyorLineSegmentDevice(
        string segmentId,
        ILogger<SimulatedConveyorLineSegmentDevice>? logger = null)
    {
        SegmentId = segmentId ?? throw new ArgumentNullException(nameof(segmentId));
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task<OperationResult> SetSpeedAsync(
        decimal speedMmPerSec,
        CancellationToken cancellationToken = default)
    {
        _targetSpeed = speedMmPerSec;
        _currentSpeed = speedMmPerSec;

        if (speedMmPerSec > 0 && _currentState == ConveyorSegmentState.Stopped)
        {
            _currentState = ConveyorSegmentState.Running;
        }
        else if (speedMmPerSec == 0)
        {
            _currentState = ConveyorSegmentState.Stopped;
        }

        _logger?.LogInformation(
            "[线体-HAL模拟] 段 {SegmentId} 设置速度 {Speed} mm/s | 状态={State}",
            SegmentId,
            speedMmPerSec,
            _currentState);

        return Task.FromResult(OperationResult.Success());
    }

    /// <inheritdoc/>
    public Task<OperationResult> StopAsync(CancellationToken cancellationToken = default)
    {
        _currentState = ConveyorSegmentState.Stopped;
        _currentSpeed = 0;

        _logger?.LogInformation(
            "[线体-HAL模拟] 段 {SegmentId} 停止",
            SegmentId);

        return Task.FromResult(OperationResult.Success());
    }

    /// <inheritdoc/>
    public Task<OperationResult> StartAsync(CancellationToken cancellationToken = default)
    {
        _currentState = ConveyorSegmentState.Running;
        _currentSpeed = _targetSpeed > 0 ? _targetSpeed : 1000; // 默认速度 1000 mm/s

        _logger?.LogInformation(
            "[线体-HAL模拟] 段 {SegmentId} 启动 | 速度={Speed} mm/s",
            SegmentId,
            _currentSpeed);

        return Task.FromResult(OperationResult.Success());
    }

    /// <inheritdoc/>
    public Task<ConveyorSegmentState> GetStateAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_currentState);
    }

    /// <inheritdoc/>
    public Task<decimal> GetCurrentSpeedAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_currentSpeed);
    }

    /// <summary>
    /// 注入故障状态（仅用于测试）
    /// </summary>
    public void InjectFault()
    {
        _currentState = ConveyorSegmentState.Fault;
        _currentSpeed = 0;
        _logger?.LogWarning("[线体-HAL模拟] 段 {SegmentId} 注入故障", SegmentId);
    }

    /// <summary>
    /// 清除故障状态（仅用于测试）
    /// </summary>
    public void ClearFault()
    {
        _currentState = ConveyorSegmentState.Stopped;
        _logger?.LogInformation("[线体-HAL模拟] 段 {SegmentId} 清除故障", SegmentId);
    }
}
