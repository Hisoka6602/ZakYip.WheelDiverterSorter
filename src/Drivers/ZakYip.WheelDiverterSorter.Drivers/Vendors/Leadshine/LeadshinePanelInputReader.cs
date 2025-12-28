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
    /// <remarks>
    /// 注意：当前 IInputPort.ReadAsync 不支持取消令牌，因此此方法无法响应取消请求。
    /// 这与 SimulatedPanelInputReader 的行为一致。
    /// 技术债：考虑为 IInputPort 接口添加取消令牌支持。
    /// 
    /// 对于急停按钮（EmergencyStop），需要检查所有配置的急停按钮：
    /// - 如果任意一个急停按钮处于"按下"状态，则返回 IsPressed = true
    /// - 只有当所有急停按钮都处于"解除"状态时，才返回 IsPressed = false
    /// </remarks>
    public async Task<PanelButtonState> ReadButtonStateAsync(
        PanelButtonType buttonType,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var config = _panelConfigRepository.Get();

            // 特殊处理急停按钮：检查所有急停按钮
            if (buttonType == PanelButtonType.EmergencyStop)
            {
                return await ReadEmergencyStopStateAsync(config);
            }

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
    /// <remarks>
    /// 优化实现：收集所有需要读取的IO位，使用批量读取API一次性获取，
    /// 避免逐个读取导致的IO阻塞问题。
    /// </remarks>
    public async Task<IDictionary<PanelButtonType, PanelButtonState>> ReadAllButtonStatesAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 一次性读取配置，避免重复数据库访问
            var config = _panelConfigRepository.Get();
            var result = new Dictionary<PanelButtonType, PanelButtonState>();

            // 收集所有需要读取的按钮位（用于批量读取）
            var buttonBits = new Dictionary<int, (PanelButtonType ButtonType, TriggerLevel TriggerLevel)>();
            var emergencyStopBits = new List<int>();

            // 收集常规按钮的IO位
            foreach (PanelButtonType buttonType in Enum.GetValues<PanelButtonType>().Where(bt => bt != PanelButtonType.EmergencyStop))
            {
                var (bitNumber, triggerLevel) = GetButtonConfig(buttonType, config);
                
                if (bitNumber.HasValue && !buttonBits.ContainsKey(bitNumber.Value))
                {
                    buttonBits[bitNumber.Value] = (buttonType, triggerLevel);
                }
            }
            
            // 收集急停按钮的IO位（可能有多个）
            foreach (var emergencyButton in config.EmergencyStopButtons)
            {
                if (!emergencyStopBits.Contains(emergencyButton.InputBit))
                {
                    emergencyStopBits.Add(emergencyButton.InputBit);
                }
            }

            // 合并所有需要读取的位
            var allBitsToRead = buttonBits.Keys.Concat(emergencyStopBits).Distinct().OrderBy(b => b).ToList();

            // 批量读取所有IO位
            Dictionary<int, bool> ioValues = new Dictionary<int, bool>();
            
            if (allBitsToRead.Count > 0)
            {
                int minBit = allBitsToRead.Min();
                int maxBit = allBitsToRead.Max();
                int count = maxBit - minBit + 1;
                
                /// <summary>
                /// 批量读取范围扩展倍数阈值
                /// 当IO位范围不超过实际位数的此倍数时，使用批量读取更高效
                /// </summary>
                const int MaxBatchRangeMultiplier = 3;
                
                // 如果位分布比较集中（范围内的位数不超过实际需要读取位数的3倍），使用批量读取
                if (count <= allBitsToRead.Count * MaxBatchRangeMultiplier)
                {
                    bool[] batchValues = await _inputPort.ReadBatchAsync(minBit, count);
                    
                    for (int i = 0; i < count; i++)
                    {
                        ioValues[minBit + i] = batchValues[i];
                    }
                    
                    // 成功时不输出日志，仅在失败时输出（避免日志泛滥）
                }
                else
                {
                    // 位分布太分散，逐个读取
                    foreach (var bit in allBitsToRead)
                    {
                        ioValues[bit] = await _inputPort.ReadAsync(bit);
                    }
                }
            }

            // 处理常规按钮
            foreach (PanelButtonType buttonType in Enum.GetValues<PanelButtonType>())
            {
                if (buttonType == PanelButtonType.EmergencyStop)
                {
                    continue; // 急停按钮稍后单独处理
                }

                var (bitNumber, triggerLevel) = GetButtonConfig(buttonType, config);

                if (!bitNumber.HasValue)
                {
                    // 按钮未配置，返回未按下状态
                    result[buttonType] = new PanelButtonState
                    {
                        ButtonType = buttonType,
                        IsPressed = false,
                        LastChangedAt = _systemClock.LocalNowOffset,
                        PressedDurationMs = 0
                    };
                    continue;
                }

                try
                {
                    // 从批量读取的结果中获取IO值
                    bool rawValue = ioValues.TryGetValue(bitNumber.Value, out bool val) && val;

                    // 根据触发电平判断按钮是否按下
                    bool isPressed = triggerLevel == TriggerLevel.ActiveHigh ? rawValue : !rawValue;

                    result[buttonType] = new PanelButtonState
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

                    result[buttonType] = new PanelButtonState
                    {
                        ButtonType = buttonType,
                        IsPressed = false,
                        LastChangedAt = _systemClock.LocalNowOffset,
                        PressedDurationMs = 0
                    };
                }
            }

            // 处理急停按钮（特殊逻辑：任意一个按下就算按下）
            result[PanelButtonType.EmergencyStop] = await ReadEmergencyStopStateAsync(config, ioValues);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "读取所有按钮状态失败");
            
            // 返回所有按钮的默认未按下状态
            var fallbackResult = new Dictionary<PanelButtonType, PanelButtonState>();
            foreach (PanelButtonType buttonType in Enum.GetValues<PanelButtonType>())
            {
                fallbackResult[buttonType] = new PanelButtonState
                {
                    ButtonType = buttonType,
                    IsPressed = false,
                    LastChangedAt = _systemClock.LocalNowOffset,
                    PressedDurationMs = 0
                };
            }
            return fallbackResult;
        }
    }

    /// <summary>
    /// 读取急停按钮状态
    /// </summary>
    /// <param name="config">面板配置</param>
    /// <param name="ioValues">可选的已读取IO值缓存（用于批量读取优化）</param>
    /// <remarks>
    /// 系统只有在所有急停按钮都处于"解除"状态时才视为解除急停状态：
    /// - 如果任意一个急停按钮处于"按下"状态，则返回 IsPressed = true
    /// - 只有当所有急停按钮都处于"解除"状态时，才返回 IsPressed = false
    /// 
    /// 对于每个急停按钮：
    /// - 当 InputTriggerLevel 为 ActiveHigh 时，高电平表示按下急停，低电平表示解除急停
    /// - 当 InputTriggerLevel 为 ActiveLow 时，低电平表示按下急停，高电平表示解除急停
    /// 
    /// 优化：支持从预读取的IO值缓存中获取状态，避免重复读取
    /// </remarks>
    private async Task<PanelButtonState> ReadEmergencyStopStateAsync(
        Core.LineModel.Configuration.Models.PanelConfiguration config,
        Dictionary<int, bool>? ioValues = null)
    {
        try
        {
            // 如果没有配置任何急停按钮，返回未按下状态
            if (config.EmergencyStopButtons.Count == 0)
            {
                return new PanelButtonState
                {
                    ButtonType = PanelButtonType.EmergencyStop,
                    IsPressed = false,
                    LastChangedAt = _systemClock.LocalNowOffset,
                    PressedDurationMs = 0
                };
            }

            // 检查所有急停按钮，只要有一个按下就返回按下状态
            bool anyPressed = false;

            foreach (var emergencyButton in config.EmergencyStopButtons)
            {
                try
                {
                    // 优先从缓存中读取，如果没有缓存则从硬件读取
                    bool rawValue = ioValues != null && ioValues.TryGetValue(emergencyButton.InputBit, out bool cachedValue)
                        ? cachedValue
                        : await _inputPort.ReadAsync(emergencyButton.InputBit);

                    // 根据触发电平判断按钮是否按下
                    // ActiveHigh: 高电平=按下急停，低电平=解除急停
                    // ActiveLow: 低电平=按下急停，高电平=解除急停
                    bool isPressed = emergencyButton.InputTriggerLevel == TriggerLevel.ActiveHigh 
                        ? rawValue 
                        : !rawValue;

                    if (isPressed)
                    {
                        anyPressed = true;
                        _logger.LogDebug(
                            "急停按钮 IO位={InputBit} 触发电平={TriggerLevel} 原始值={RawValue} 状态=按下",
                            emergencyButton.InputBit,
                            emergencyButton.InputTriggerLevel,
                            rawValue);
                        break; // 有一个按下就足够了，不需要继续检查
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "读取急停按钮 IO位={InputBit} 状态失败，假定为按下状态以确保安全",
                        emergencyButton.InputBit);
                    
                    // 读取失败时，假定为按下状态以确保安全
                    anyPressed = true;
                    break;
                }
            }

            return new PanelButtonState
            {
                ButtonType = PanelButtonType.EmergencyStop,
                IsPressed = anyPressed,
                LastChangedAt = _systemClock.LocalNowOffset,
                PressedDurationMs = 0
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "读取急停按钮状态失败，假定为按下状态以确保安全");
            
            // 异常情况下假定为按下状态以确保安全
            return new PanelButtonState
            {
                ButtonType = PanelButtonType.EmergencyStop,
                IsPressed = true,
                LastChangedAt = _systemClock.LocalNowOffset,
                PressedDurationMs = 0
            };
        }
    }

    /// <summary>
    /// 获取指定按钮的 IO 配置
    /// </summary>
    /// <remarks>
    /// Reset 按钮类型存在于 PanelButtonType 枚举中，但当前 PanelConfiguration 模型中未定义其配置字段。
    /// 这是一个已知的设计决策，Reset 按钮保留用于将来扩展或特殊用途。
    /// 当前实现中，Reset 按钮始终返回未配置状态（null BitNumber）。
    /// 
    /// EmergencyStop 按钮不通过此方法处理，因为它支持多个按钮配置，
    /// 而是通过 ReadEmergencyStopStateAsync 方法专门处理。
    /// </remarks>
    private static (int? BitNumber, TriggerLevel TriggerLevel) GetButtonConfig(
        PanelButtonType buttonType,
        Core.LineModel.Configuration.Models.PanelConfiguration config)
    {
        return buttonType switch
        {
            PanelButtonType.Start => (config.StartButtonInputBit, config.StartButtonTriggerLevel),
            PanelButtonType.Stop => (config.StopButtonInputBit, config.StopButtonTriggerLevel),
            PanelButtonType.EmergencyStop => (null, TriggerLevel.ActiveHigh), // 急停按钮通过 ReadEmergencyStopStateAsync 专门处理
            PanelButtonType.Reset => (null, TriggerLevel.ActiveHigh), // Reset 按钮未在配置中定义（保留用于将来扩展）
            _ => (null, TriggerLevel.ActiveHigh)
        };
    }
}
