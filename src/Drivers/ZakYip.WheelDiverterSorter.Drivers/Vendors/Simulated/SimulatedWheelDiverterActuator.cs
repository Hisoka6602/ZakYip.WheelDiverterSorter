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
        _logger.LogDebug("Diverter {DiverterId} turned left", DiverterId);
        return Task.FromResult(true);
    }

    public Task<bool> TurnRightAsync(CancellationToken cancellationToken = default)
    {
        _currentState = "Right";
        _logger.LogDebug("Diverter {DiverterId} turned right", DiverterId);
        return Task.FromResult(true);
    }

    public Task<bool> PassThroughAsync(CancellationToken cancellationToken = default)
    {
        _currentState = "PassThrough";
        _logger.LogDebug("Diverter {DiverterId} set to pass-through", DiverterId);
        return Task.FromResult(true);
    }

    public Task<bool> StopAsync(CancellationToken cancellationToken = default)
    {
        _currentState = "Stopped";
        _logger.LogDebug("Diverter {DiverterId} stopped", DiverterId);
        return Task.FromResult(true);
    }

    public Task<string> GetStatusAsync()
    {
        return Task.FromResult($"Diverter {DiverterId}: {_currentState}");
    }
}
