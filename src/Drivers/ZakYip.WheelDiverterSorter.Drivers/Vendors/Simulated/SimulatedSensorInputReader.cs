using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;

namespace ZakYip.WheelDiverterSorter.Drivers.Vendors.Simulated;

/// <summary>
/// 模拟传感器输入读取器
/// Simulated Sensor Input Reader - In-memory implementation for testing and simulation
/// </summary>
public class SimulatedSensorInputReader : ISensorInputReader
{
    private readonly ILogger<SimulatedSensorInputReader> _logger;
    private readonly Dictionary<int, bool> _sensorStates = new();
    private readonly Dictionary<int, bool> _sensorOnlineStates = new();

    public SimulatedSensorInputReader(ILogger<SimulatedSensorInputReader> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 设置传感器状态（用于测试场景）
    /// </summary>
    public void SetSensorState(int logicalPoint, bool state)
    {
        _sensorStates[logicalPoint] = state;
        _sensorOnlineStates[logicalPoint] = true;
        _logger.LogDebug("Sensor {LogicalPoint} state set to {State}", logicalPoint, state);
    }

    /// <summary>
    /// 设置传感器在线状态（用于故障注入）
    /// </summary>
    public void SetSensorOnline(int logicalPoint, bool isOnline)
    {
        _sensorOnlineStates[logicalPoint] = isOnline;
        _logger.LogDebug("Sensor {LogicalPoint} online status set to {IsOnline}", logicalPoint, isOnline);
    }

    public Task<bool> ReadSensorAsync(int logicalPoint, CancellationToken cancellationToken = default)
    {
        if (!_sensorOnlineStates.GetValueOrDefault(logicalPoint, true))
        {
            _logger.LogWarning("Sensor {LogicalPoint} is offline", logicalPoint);
            return Task.FromResult(false);
        }

        var state = _sensorStates.GetValueOrDefault(logicalPoint, false);
        _logger.LogTrace("Sensor {LogicalPoint} read: {State}", logicalPoint, state);
        return Task.FromResult(state);
    }

    public Task<IDictionary<int, bool>> ReadSensorsAsync(IEnumerable<int> logicalPoints, CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<int, bool>();
        
        foreach (var point in logicalPoints)
        {
            if (!_sensorOnlineStates.GetValueOrDefault(point, true))
            {
                _logger.LogWarning("Sensor {LogicalPoint} is offline", point);
                results[point] = false;
            }
            else
            {
                results[point] = _sensorStates.GetValueOrDefault(point, false);
            }
        }

        _logger.LogTrace("Batch read {Count} sensors", results.Count);
        return Task.FromResult<IDictionary<int, bool>>(results);
    }

    public Task<bool> IsSensorOnlineAsync(int logicalPoint)
    {
        var isOnline = _sensorOnlineStates.GetValueOrDefault(logicalPoint, true);
        return Task.FromResult(isOnline);
    }
}
