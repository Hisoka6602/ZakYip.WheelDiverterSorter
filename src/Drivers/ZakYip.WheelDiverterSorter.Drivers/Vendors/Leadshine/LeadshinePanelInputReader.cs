using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Hardware.Ports;
using ZakYip.WheelDiverterSorter.Core.LineModel.Bindings;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;
using ZakYip.WheelDiverterSorter.Core.Utilities;

namespace ZakYip.WheelDiverterSorter.Drivers.Vendors.Leadshine;

/// <summary>
/// 雷赛控制器面板输入读取器
/// </summary>
/// <remarks>
/// 从雷赛 IO 控制器读取面板按钮状态。
/// IO 位映射和触发电平通过 PanelConfiguration 配置。
/// </remarks>
public class LeadshinePanelInputReader : IPanelInputReader
{
    private readonly ILogger<LeadshinePanelInputReader> _logger;
    private readonly IInputPort _inputPort;
    private readonly IPanelConfigurationRepository _panelConfigRepository;
    private readonly ISystemClock _systemClock;

    public LeadshinePanelInputReader(
        ILogger<LeadshinePanelInputReader> logger,
        IInputPort inputPort,
        IPanelConfigurationRepository panelConfigRepository,
        ISystemClock systemClock)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _inputPort = inputPort ?? throw new ArgumentNullException(nameof(inputPort));
        _panelConfigRepository = panelConfigRepository ?? throw new ArgumentNullException(nameof(panelConfigRepository));
        _systemClock = systemClock ?? throw new ArgumentNullException(nameof(systemClock));
    }

    /// <inheritdoc/>
    public async Task<PanelButtonState> ReadButtonStateAsync(
        PanelButtonType buttonType,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var config = _panelConfigRepository.Get();
            var (bitNumber, triggerLevel) = GetButtonConfig(buttonType, config);

            if (!bitNumber.HasValue)
            {
                // 按钮未配置，返回未按下状态
                return new PanelButtonState
                {
                    ButtonType = buttonType,
                    IsPressed = false,
                    LastChangedAt = _systemClock.LocalNowOffset,
                    PressedDurationMs = 0
                };
            }

            // 从硬件读取 IO 位
            bool rawValue = await _inputPort.ReadAsync(bitNumber.Value);

            // 根据触发电平判断按钮是否按下
            bool isPressed = triggerLevel == TriggerLevel.ActiveHigh ? rawValue : !rawValue;

            return new PanelButtonState
            {
                ButtonType = buttonType,
                IsPressed = isPressed,
                LastChangedAt = _systemClock.LocalNowOffset,
                PressedDurationMs = 0
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "读取面板按钮 {ButtonType} 状态失败",
                buttonType);

            return new PanelButtonState
            {
                ButtonType = buttonType,
                IsPressed = false,
                LastChangedAt = _systemClock.LocalNowOffset,
                PressedDurationMs = 0
            };
        }
    }

    /// <inheritdoc/>
    public async Task<IDictionary<PanelButtonType, PanelButtonState>> ReadAllButtonStatesAsync(
        CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<PanelButtonType, PanelButtonState>();

        // 读取所有定义的按钮类型
        foreach (PanelButtonType buttonType in Enum.GetValues<PanelButtonType>())
        {
            var state = await ReadButtonStateAsync(buttonType, cancellationToken);
            result[buttonType] = state;
        }

        return result;
    }

    /// <summary>
    /// 获取指定按钮的 IO 配置
    /// </summary>
    private static (int? BitNumber, TriggerLevel TriggerLevel) GetButtonConfig(
        PanelButtonType buttonType,
        Core.LineModel.Configuration.Models.PanelConfiguration config)
    {
        return buttonType switch
        {
            PanelButtonType.Start => (config.StartButtonInputBit, config.StartButtonTriggerLevel),
            PanelButtonType.Stop => (config.StopButtonInputBit, config.StopButtonTriggerLevel),
            PanelButtonType.EmergencyStop => (config.EmergencyStopButtonInputBit, config.EmergencyStopButtonTriggerLevel),
            PanelButtonType.Reset => (null, TriggerLevel.ActiveHigh), // Reset 按钮未在配置中定义
            _ => (null, TriggerLevel.ActiveHigh)
        };
    }
}
