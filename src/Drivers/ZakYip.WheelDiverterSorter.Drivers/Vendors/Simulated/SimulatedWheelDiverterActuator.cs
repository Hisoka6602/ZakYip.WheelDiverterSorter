using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Hardware;
using ZakYip.WheelDiverterSorter.Core.Enums;

namespace ZakYip.WheelDiverterSorter.Drivers.Vendors.Simulated;

/// <summary>
/// 模拟摆轮执行器
/// Simulated Wheel Diverter Actuator - In-memory implementation for testing and simulation
/// </summary>
public class SimulatedWheelDiverterActuator : IWheelDiverterActuator
{
    private readonly ILogger<SimulatedWheelDiverterActuator> _logger;
    private string _currentState = "PassThrough";

    public string DiverterId { get; }

    public SimulatedWheelDiverterActuator(string diverterId, ILogger<SimulatedWheelDiverterActuator> logger)
    {
        DiverterId = diverterId;
        _logger = logger;
    }

    public Task<bool> TurnLeftAsync(CancellationToken cancellationToken = default)
    {
        _currentState = "Left";
        _logger.LogInformation(
            "[摆轮通信-模拟] 摆轮 {DiverterId} 左转 | 当前状态={State}",
            DiverterId,
            _currentState);
        return Task.FromResult(true);
    }

    public Task<bool> TurnRightAsync(CancellationToken cancellationToken = default)
    {
        _currentState = "Right";
        _logger.LogInformation(
            "[摆轮通信-模拟] 摆轮 {DiverterId} 右转 | 当前状态={State}",
            DiverterId,
            _currentState);
        return Task.FromResult(true);
    }

    public Task<bool> PassThroughAsync(CancellationToken cancellationToken = default)
    {
        _currentState = "PassThrough";
        _logger.LogInformation(
            "[摆轮通信-模拟] 摆轮 {DiverterId} 直通 | 当前状态={State}",
            DiverterId,
            _currentState);
        return Task.FromResult(true);
    }

    public Task<bool> StopAsync(CancellationToken cancellationToken = default)
    {
        _currentState = "Stopped";
        _logger.LogInformation(
            "[摆轮通信-模拟] 摆轮 {DiverterId} 停止 | 当前状态={State}",
            DiverterId,
            _currentState);
        return Task.FromResult(true);
    }

    public Task<string> GetStatusAsync()
    {
        return Task.FromResult($"Diverter {DiverterId}: {_currentState}");
    }
}
