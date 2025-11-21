using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;
using ZakYip.WheelDiverterSorter.Core.Enums.Communication;
using ZakYip.WheelDiverterSorter.Core.Enums.Conveyor;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Enums.Routing;
using ZakYip.WheelDiverterSorter.Core.Enums.Sensors;
using ZakYip.WheelDiverterSorter.Core.Enums.System;
using ZakYip.WheelDiverterSorter.Drivers.Abstractions;

namespace ZakYip.WheelDiverterSorter.Drivers.Vendors.Simulated;

/// <summary>
/// 仿真 IO 联动驱动。
/// 在仿真模式下模拟 IO 联动控制，不连接真实硬件。
/// </summary>
public class SimulatedIoLinkageDriver : IIoLinkageDriver
{
    private readonly ILogger<SimulatedIoLinkageDriver> _logger;
    private readonly Dictionary<int, bool> _ioStates = new();
    private readonly object _lock = new();

    public SimulatedIoLinkageDriver(ILogger<SimulatedIoLinkageDriver> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task SetIoPointAsync(IoLinkagePoint ioPoint, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var desiredState = ioPoint.Level == TriggerLevel.ActiveHigh;
            _ioStates[ioPoint.BitNumber] = desiredState;
            
            _logger.LogInformation(
                "仿真 IO 联动: 设置 IO {BitNumber} 为 {Level} ({State})",
                ioPoint.BitNumber,
                ioPoint.Level,
                desiredState ? "高电平" : "低电平");
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task SetIoPointsAsync(IEnumerable<IoLinkagePoint> ioPoints, CancellationToken cancellationToken = default)
    {
        foreach (var ioPoint in ioPoints)
        {
            await SetIoPointAsync(ioPoint, cancellationToken);
        }
    }

    /// <inheritdoc/>
    public Task<bool> ReadIoPointAsync(int bitNumber, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_ioStates.TryGetValue(bitNumber, out var state))
            {
                _logger.LogDebug("仿真 IO 联动: 读取 IO {BitNumber} 状态 = {State}", bitNumber, state);
                return Task.FromResult(state);
            }

            _logger.LogDebug("仿真 IO 联动: IO {BitNumber} 未设置，返回默认值 false", bitNumber);
            return Task.FromResult(false);
        }
    }

    /// <inheritdoc/>
    public Task ResetAllIoPointsAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var count = _ioStates.Count;
            _ioStates.Clear();
            _logger.LogInformation("仿真 IO 联动: 复位所有 IO 点 (共 {Count} 个)", count);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 获取所有 IO 状态（用于调试和测试）。
    /// </summary>
    /// <returns>IO 状态字典（只读）</returns>
    public IReadOnlyDictionary<int, bool> GetAllIoStates()
    {
        lock (_lock)
        {
            return new Dictionary<int, bool>(_ioStates);
        }
    }
}
