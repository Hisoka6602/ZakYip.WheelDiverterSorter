using System.Collections.Concurrent;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Enums.System;
using ZakYip.WheelDiverterSorter.Core.LineModel.Bindings;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;
using ZakYip.WheelDiverterSorter.Observability.Utilities;
using ZakYip.WheelDiverterSorter.Application.Services.Config;
using ZakYip.WheelDiverterSorter.Host.StateMachine;

namespace ZakYip.WheelDiverterSorter.Host.Services.Workers;

/// <summary>
/// 面板按钮监控后台服务
/// </summary>
/// <remarks>
/// 定期轮询面板按钮状态，当检测到按钮按下时触发相应的IO联动动作。
/// 轮询间隔可通过面板配置中的 PollingIntervalMs 参数配置（范围：10~5000ms）。
/// </remarks>
public sealed class PanelButtonMonitorWorker : BackgroundService
{
    private readonly ILogger<PanelButtonMonitorWorker> _logger;
    private readonly IPanelInputReader _panelInputReader;
    private readonly IIoLinkageConfigService _ioLinkageConfigService;
    private readonly ISystemStateManager _stateManager;
    private readonly IPanelConfigurationRepository _panelConfigRepository;
    private readonly ISafeExecutionService _safeExecutor;
    
    /// <summary>
    /// 默认按钮轮询间隔（毫秒）
    /// </summary>
    private const int DefaultPollingIntervalMs = 100;
    
    /// <summary>
    /// 异常恢复延迟（毫秒）
    /// </summary>
    private const int ErrorRecoveryDelayMs = 1000;
    
    /// <summary>
    /// 上次按钮状态缓存，用于检测状态变化
    /// </summary>
    private readonly ConcurrentDictionary<PanelButtonType, bool> _lastButtonStates = new();

    public PanelButtonMonitorWorker(
        ILogger<PanelButtonMonitorWorker> logger,
        IPanelInputReader panelInputReader,
        IIoLinkageConfigService ioLinkageConfigService,
        ISystemStateManager stateManager,
        IPanelConfigurationRepository panelConfigRepository,
        ISafeExecutionService safeExecutor)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _panelInputReader = panelInputReader ?? throw new ArgumentNullException(nameof(panelInputReader));
        _ioLinkageConfigService = ioLinkageConfigService ?? throw new ArgumentNullException(nameof(ioLinkageConfigService));
        _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
        _panelConfigRepository = panelConfigRepository ?? throw new ArgumentNullException(nameof(panelConfigRepository));
        _safeExecutor = safeExecutor ?? throw new ArgumentNullException(nameof(safeExecutor));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _safeExecutor.ExecuteAsync(
            async () =>
            {
                _logger.LogInformation("面板按钮监控服务已启动");

                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        // 从配置中读取轮询间隔
                        var panelConfig = _panelConfigRepository.Get();
                        int pollingInterval = panelConfig?.PollingIntervalMs ?? DefaultPollingIntervalMs;
                        
                        // 确保轮询间隔在有效范围内（10~5000ms）
                        if (pollingInterval < 10 || pollingInterval > 5000)
                        {
                            _logger.LogWarning(
                                "面板轮询间隔 {PollingInterval}ms 超出有效范围（10~5000ms），使用默认值 {DefaultInterval}ms",
                                pollingInterval,
                                DefaultPollingIntervalMs);
                            pollingInterval = DefaultPollingIntervalMs;
                        }

                        // 读取所有按钮状态
                        var buttonStates = await _panelInputReader.ReadAllButtonStatesAsync(stoppingToken);

                        // 检查每个按钮的状态变化
                        foreach (var (buttonType, buttonState) in buttonStates)
                        {
                            // 获取上次状态
                            bool wasPressed = _lastButtonStates.GetValueOrDefault(buttonType, false);
                            bool isPressed = buttonState.IsPressed;

                            // 检测按钮从未按下到按下的状态变化（上升沿）
                            if (isPressed && !wasPressed)
                            {
                                _logger.LogInformation(
                                    "检测到面板按钮按下：{ButtonType}",
                                    buttonType);

                                // 触发IO联动
                                await TriggerIoLinkageAsync(buttonType, stoppingToken);
                            }

                            // 更新状态缓存
                            _lastButtonStates[buttonType] = isPressed;
                        }

                        // 使用配置的轮询间隔等待下一次轮询
                        await Task.Delay(pollingInterval, stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        // 正常取消，退出循环
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "面板按钮监控异常");
                        
                        // 发生异常后稍作延迟再继续
                        await Task.Delay(ErrorRecoveryDelayMs, stoppingToken);
                    }
                }

                _logger.LogInformation("面板按钮监控服务已停止");
            },
            "PanelButtonMonitorLoop",
            stoppingToken);
    }

    /// <summary>
    /// 触发IO联动
    /// </summary>
    private async Task TriggerIoLinkageAsync(PanelButtonType buttonType, CancellationToken cancellationToken)
    {
        try
        {
            // 获取当前系统状态
            var currentState = _stateManager.CurrentState;
            
            _logger.LogInformation(
                "触发按钮 {ButtonType} 的IO联动，当前系统状态：{SystemState}",
                buttonType,
                currentState);

            // 首先处理按钮的主要功能（状态转换）
            await HandleButtonActionAsync(buttonType, currentState, cancellationToken);

            // 状态转换后，获取新的系统状态用于IO联动
            var newState = _stateManager.CurrentState;
            var operatingState = MapToOperatingState(newState);
            
            _logger.LogInformation(
                "按钮 {ButtonType} 状态已转换: {OldState} -> {NewState}，准备触发 {OperatingState} 状态的IO联动",
                buttonType,
                currentState,
                newState,
                operatingState);

            // 根据新的系统状态触发IO联动
            var result = await _ioLinkageConfigService.TriggerIoLinkageAsync(operatingState);
            
            if (result.Success)
            {
                _logger.LogInformation(
                    "按钮 {ButtonType} 的IO联动触发成功，触发了 {Count} 个IO点",
                    buttonType,
                    result.TriggeredIoPoints.Count);
            }
            else
            {
                _logger.LogWarning(
                    "按钮 {ButtonType} 的IO联动触发失败：{ErrorMessage}",
                    buttonType,
                    result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "触发按钮 {ButtonType} 的IO联动异常",
                buttonType);
        }
    }

    /// <summary>
    /// 处理按钮动作（状态转换）
    /// </summary>
    /// <remarks>
    /// 修复Issue 2: 面板按钮按下时应该触发相应的系统状态转换
    /// 新需求: 启动按钮按下时先触发预警，等待预警时间后再进入Running状态
    /// </remarks>
    private async Task HandleButtonActionAsync(PanelButtonType buttonType, SystemState currentState, CancellationToken cancellationToken)
    {
        try
        {
            // 特殊处理启动按钮：需要预警逻辑
            if (buttonType == PanelButtonType.Start && currentState is SystemState.Ready or SystemState.Paused)
            {
                await HandleStartButtonWithPreWarningAsync(currentState, cancellationToken);
                return;
            }

            // 其他按钮的正常处理
            SystemState? targetState = buttonType switch
            {
                PanelButtonType.Stop when currentState is SystemState.Running or SystemState.Paused =>
                    SystemState.Ready,
                
                PanelButtonType.Reset when currentState is SystemState.Faulted or SystemState.EmergencyStop =>
                    SystemState.Ready,
                
                PanelButtonType.EmergencyStop when currentState != SystemState.EmergencyStop =>
                    SystemState.EmergencyStop,
                
                _ => null
            };

            if (targetState.HasValue)
            {
                _logger.LogInformation(
                    "按钮 {ButtonType} 触发状态转换: {FromState} -> {ToState}",
                    buttonType,
                    currentState,
                    targetState.Value);

                var result = await _stateManager.ChangeStateAsync(targetState.Value, cancellationToken);
                
                if (!result.Success)
                {
                    _logger.LogWarning(
                        "按钮 {ButtonType} 状态转换失败: {ErrorMessage}",
                        buttonType,
                        result.ErrorMessage);
                }
            }
            else
            {
                _logger.LogDebug(
                    "按钮 {ButtonType} 在当前状态 {CurrentState} 下无需状态转换",
                    buttonType,
                    currentState);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "处理按钮 {ButtonType} 动作异常",
                buttonType);
        }
    }

    /// <summary>
    /// 处理启动按钮的预警逻辑
    /// </summary>
    /// <remarks>
    /// 启动按钮按下时：
    /// 1. 保持在Ready状态
    /// 2. 如果配置了预警时间，触发预警输出并等待
    /// 3. 预警时间结束后转换到Running状态
    /// </remarks>
    private async Task HandleStartButtonWithPreWarningAsync(SystemState currentState, CancellationToken cancellationToken)
    {
        try
        {
            var panelConfig = _panelConfigRepository.Get();
            var preWarningDuration = panelConfig?.PreStartWarningDurationSeconds;

            _logger.LogInformation(
                "启动按钮处理开始 - 当前状态: {CurrentState}, 配置的预警时间: {PreWarningDuration} 秒",
                currentState,
                preWarningDuration?.ToString() ?? "未配置");

            if (preWarningDuration.HasValue && preWarningDuration.Value > 0)
            {
                _logger.LogWarning(
                    "⚠️ 启动按钮按下，开始预警 {Duration} 秒，当前状态保持为 {CurrentState}，摆轮将在预警结束后启动",
                    preWarningDuration.Value,
                    currentState);

                // 记录预警开始时间用于验证
                var warningStartTime = DateTime.Now;

                // TODO: 触发预警输出（PreStartWarningOutputBit）
                // 这需要通过输出端口服务来实现
                // 如果配置了预警输出位，应该在这里设置输出为高/低电平
                if (panelConfig?.PreStartWarningOutputBit.HasValue == true)
                {
                    _logger.LogInformation(
                        "预警输出位已配置: Bit={OutputBit}, Level={OutputLevel}（注：当前版本暂未实现物理输出控制）",
                        panelConfig.PreStartWarningOutputBit.Value,
                        panelConfig.PreStartWarningOutputLevel);
                }

                // 等待预警时间
                _logger.LogInformation("开始等待预警时间: {Duration} 秒...", preWarningDuration.Value);
                await Task.Delay(TimeSpan.FromSeconds(preWarningDuration.Value), cancellationToken);

                var actualWaitTime = (DateTime.Now - warningStartTime).TotalSeconds;
                _logger.LogWarning(
                    "✅ 预警时间结束，实际等待: {ActualWait:F2} 秒，准备转换到 Running 状态并启动摆轮",
                    actualWaitTime);
            }
            else
            {
                _logger.LogInformation("未配置预警时间或预警时间为0，直接转换到 Running 状态");
            }

            // 预警结束后，转换到Running状态
            _logger.LogInformation("正在将系统状态从 {CurrentState} 转换到 Running...", currentState);
            var result = await _stateManager.ChangeStateAsync(SystemState.Running, cancellationToken);
            
            if (!result.Success)
            {
                _logger.LogError(
                    "❌ 启动按钮状态转换失败: {ErrorMessage}",
                    result.ErrorMessage);
            }
            else
            {
                _logger.LogInformation("✅ 系统状态已成功转换到 Running，摆轮应该开始启动");
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("⚠️ 预警过程被取消（可能是系统停止或急停）");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ 处理启动按钮预警逻辑异常");
            throw;
        }
    }

    /// <summary>
    /// 将 SystemState 映射到 SystemOperatingState
    /// </summary>
    private static SystemOperatingState MapToOperatingState(SystemState state)
    {
        return state switch
        {
            SystemState.Booting => SystemOperatingState.Initializing,
            SystemState.Ready => SystemOperatingState.Standby,
            SystemState.Running => SystemOperatingState.Running,
            SystemState.Paused => SystemOperatingState.Paused,
            SystemState.Faulted => SystemOperatingState.Faulted,
            SystemState.EmergencyStop => SystemOperatingState.EmergencyStopped,
            _ => SystemOperatingState.Standby
        };
    }
}
