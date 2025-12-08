using System.Collections.Concurrent;
using ZakYip.WheelDiverterSorter.Core.LineModel;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.LineModel.Bindings;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Utilities;

namespace ZakYip.WheelDiverterSorter.Drivers.Vendors.Simulated;

/// <summary>
/// 仿真面板输入读取器。
/// 通过内存状态模拟面板按钮，用于测试和仿真场景。
/// </summary>
public class SimulatedPanelInputReader : IPanelInputReader
{
    private readonly ConcurrentDictionary<PanelButtonType, PanelButtonState> _buttonStates = new();
    private readonly ISystemClock _systemClock;

    public SimulatedPanelInputReader(ISystemClock systemClock)
    {
        _systemClock = systemClock ?? throw new ArgumentNullException(nameof(systemClock));
        
        // 初始化所有按钮为未按下状态
        foreach (PanelButtonType buttonType in Enum.GetValues<PanelButtonType>())
        {
            _buttonStates[buttonType] = new PanelButtonState
            {
                ButtonType = buttonType,
                IsPressed = false,
                LastChangedAt = _systemClock.LocalNowOffset,
                PressedDurationMs = 0
            };
        }
    }

    /// <inheritdoc/>
    public Task<PanelButtonState> ReadButtonStateAsync(
        PanelButtonType buttonType, 
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_buttonStates.GetValueOrDefault(buttonType, new PanelButtonState
        {
            ButtonType = buttonType,
            IsPressed = false,
            LastChangedAt = _systemClock.LocalNowOffset,
            PressedDurationMs = 0
        }));
    }

    /// <inheritdoc/>
    public Task<IDictionary<PanelButtonType, PanelButtonState>> ReadAllButtonStatesAsync(
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IDictionary<PanelButtonType, PanelButtonState>>(
            new Dictionary<PanelButtonType, PanelButtonState>(_buttonStates));
    }

    /// <summary>
    /// 模拟按下按钮（仅供仿真使用）。
    /// </summary>
    public void SimulatePressButton(PanelButtonType buttonType)
    {
        var currentState = _buttonStates.GetValueOrDefault(buttonType);
        _buttonStates[buttonType] = new PanelButtonState
        {
            ButtonType = buttonType,
            IsPressed = true,
            LastChangedAt = _systemClock.LocalNowOffset,
            PressedDurationMs = 0
        };
    }

    /// <summary>
    /// 模拟释放按钮（仅供仿真使用）。
    /// </summary>
    public void SimulateReleaseButton(PanelButtonType buttonType)
    {
        var currentState = _buttonStates.GetValueOrDefault(buttonType);
        var pressedDuration = currentState.IsPressed
            ? (int)(_systemClock.LocalNowOffset - currentState.LastChangedAt).TotalMilliseconds
            : 0;

        _buttonStates[buttonType] = new PanelButtonState
        {
            ButtonType = buttonType,
            IsPressed = false,
            LastChangedAt = _systemClock.LocalNowOffset,
            PressedDurationMs = pressedDuration
        };
    }

    /// <summary>
    /// 重置所有按钮状态（仅供仿真使用）。
    /// </summary>
    public void ResetAllButtons()
    {
        foreach (PanelButtonType buttonType in Enum.GetValues<PanelButtonType>())
        {
            _buttonStates[buttonType] = new PanelButtonState
            {
                ButtonType = buttonType,
                IsPressed = false,
                LastChangedAt = _systemClock.LocalNowOffset,
                PressedDurationMs = 0
            };
        }
    }
}
