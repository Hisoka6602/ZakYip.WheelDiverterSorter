using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Hardware;
using ZakYip.WheelDiverterSorter.Core.Results;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Execution;
using HalWheelCommand = ZakYip.WheelDiverterSorter.Core.Hardware.WheelCommand;

namespace ZakYip.WheelDiverterSorter.Drivers.Vendors.Simulated;

/// <summary>
/// 模拟摆轮设备实现 - HAL实现
/// Simulated Wheel Diverter Device - HAL Implementation
/// </summary>
/// <remarks>
/// 此类实现 <see cref="IWheelDiverterDevice"/> HAL接口，
/// 提供摆轮控制的内存模拟实现，用于测试和仿真环境。
/// </remarks>
public sealed class SimulatedWheelDiverterDevice : IWheelDiverterDevice
{
    private readonly ILogger<SimulatedWheelDiverterDevice>? _logger;
    private WheelDiverterState _currentState = WheelDiverterState.Idle;
    private DiverterDirection _lastDirection = DiverterDirection.Straight;

    /// <inheritdoc/>
    public string DeviceId { get; }

    /// <summary>
    /// 创建模拟摆轮设备
    /// </summary>
    /// <param name="deviceId">设备标识</param>
    /// <param name="logger">日志记录器</param>
    public SimulatedWheelDiverterDevice(string deviceId, ILogger<SimulatedWheelDiverterDevice>? logger = null)
    {
        DeviceId = deviceId ?? throw new ArgumentNullException(nameof(deviceId));
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task<OperationResult> ExecuteAsync(
        HalWheelCommand command,
        CancellationToken cancellationToken = default)
    {
        _lastDirection = command.Direction;
        _currentState = command.Direction switch
        {
            DiverterDirection.Left => WheelDiverterState.AtLeft,
            DiverterDirection.Right => WheelDiverterState.AtRight,
            DiverterDirection.Straight => WheelDiverterState.AtStraight,
            _ => WheelDiverterState.Unknown
        };

        _logger?.LogInformation(
            "[摆轮-HAL模拟] 设备 {DeviceId} 执行命令 | 方向={Direction} | 状态={State}",
            DeviceId,
            command.Direction,
            _currentState);

        return Task.FromResult(OperationResult.Success());
    }

    /// <inheritdoc/>
    public Task<OperationResult> StopAsync(CancellationToken cancellationToken = default)
    {
        _currentState = WheelDiverterState.Idle;

        _logger?.LogInformation(
            "[摆轮-HAL模拟] 设备 {DeviceId} 停止",
            DeviceId);

        return Task.FromResult(OperationResult.Success());
    }

    /// <inheritdoc/>
    public Task<WheelDiverterState> GetStateAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_currentState);
    }

    /// <summary>
    /// 获取最后执行的方向（用于测试）
    /// </summary>
    public DiverterDirection LastDirection => _lastDirection;

    /// <summary>
    /// 注入故障状态（仅用于测试）
    /// </summary>
    public void InjectFault()
    {
        _currentState = WheelDiverterState.Fault;
        _logger?.LogWarning("[摆轮-HAL模拟] 设备 {DeviceId} 注入故障", DeviceId);
    }

    /// <summary>
    /// 清除故障状态（仅用于测试）
    /// </summary>
    public void ClearFault()
    {
        _currentState = WheelDiverterState.Idle;
        _logger?.LogInformation("[摆轮-HAL模拟] 设备 {DeviceId} 清除故障", DeviceId);
    }
}
