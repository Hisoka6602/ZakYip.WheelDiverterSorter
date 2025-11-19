using System.Collections.Concurrent;
using ZakYip.WheelDiverterSorter.Core.LineModel;
using ZakYip.WheelDiverterSorter.Core.LineModel.Enums;

namespace ZakYip.WheelDiverterSorter.Drivers.Vendors.Simulated;

/// <summary>
/// 仿真信号塔输出。
/// 通过内存记录模拟三色灯和蜂鸣器状态，用于测试和仿真场景。
/// </summary>
public class SimulatedSignalTowerOutput : ISignalTowerOutput
{
    private readonly ConcurrentDictionary<SignalTowerChannel, SignalTowerState> _channelStates = new();
    private readonly List<SignalTowerStateChange> _stateChangeHistory = new();
    private readonly object _historyLock = new();

    public SimulatedSignalTowerOutput()
    {
        // 初始化所有通道为关闭状态
        foreach (SignalTowerChannel channel in Enum.GetValues<SignalTowerChannel>())
        {
            _channelStates[channel] = SignalTowerState.CreateOff(channel);
        }
    }

    /// <inheritdoc/>
    public Task SetChannelStateAsync(SignalTowerState state, CancellationToken cancellationToken = default)
    {
        _channelStates[state.Channel] = state;
        RecordStateChange(state);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task SetChannelStatesAsync(IEnumerable<SignalTowerState> states, CancellationToken cancellationToken = default)
    {
        foreach (var state in states)
        {
            _channelStates[state.Channel] = state;
            RecordStateChange(state);
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task TurnOffAllAsync(CancellationToken cancellationToken = default)
    {
        foreach (SignalTowerChannel channel in Enum.GetValues<SignalTowerChannel>())
        {
            var offState = SignalTowerState.CreateOff(channel);
            _channelStates[channel] = offState;
            RecordStateChange(offState);
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<IDictionary<SignalTowerChannel, SignalTowerState>> GetAllChannelStatesAsync(
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IDictionary<SignalTowerChannel, SignalTowerState>>(
            new Dictionary<SignalTowerChannel, SignalTowerState>(_channelStates));
    }

    /// <summary>
    /// 获取状态变更历史记录（仅供测试使用）。
    /// </summary>
    public IReadOnlyList<SignalTowerStateChange> GetStateChangeHistory()
    {
        lock (_historyLock)
        {
            return _stateChangeHistory.ToList();
        }
    }

    /// <summary>
    /// 清除状态变更历史记录（仅供测试使用）。
    /// </summary>
    public void ClearHistory()
    {
        lock (_historyLock)
        {
            _stateChangeHistory.Clear();
        }
    }

    private void RecordStateChange(SignalTowerState state)
    {
        lock (_historyLock)
        {
            _stateChangeHistory.Add(new SignalTowerStateChange
            {
                State = state,
                ChangedAt = DateTimeOffset.UtcNow
            });
        }
    }
}

/// <summary>
/// 信号塔状态变更记录。
/// 用于测试和验证信号塔状态变更的时序。
/// </summary>
public readonly record struct SignalTowerStateChange
{
    public required SignalTowerState State { get; init; }
    public required DateTimeOffset ChangedAt { get; init; }
}
