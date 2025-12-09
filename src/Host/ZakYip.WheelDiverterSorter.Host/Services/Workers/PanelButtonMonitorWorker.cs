using System.Collections.Concurrent;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Enums.System;
using ZakYip.WheelDiverterSorter.Core.LineModel.Bindings;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Observability.Utilities;
using ZakYip.WheelDiverterSorter.Application.Services.Config;
using ZakYip.WheelDiverterSorter.Host.StateMachine;
using ZakYip.WheelDiverterSorter.Core.Hardware.Ports;

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
    private readonly ISystemClock _systemClock;
    private readonly IOutputPort _outputPort;
    
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
    
    /// <summary>
    /// 预警等待取消令牌源，用于在高优先级按钮按下时取消预警等待
    /// </summary>
    private CancellationTokenSource? _preWarningCancellationSource;
    
    /// <summary>
    /// 预警等待锁，用于同步预警状态的访问
    /// </summary>
    private readonly object _preWarningLock = new();

    public PanelButtonMonitorWorker(
        ILogger<PanelButtonMonitorWorker> logger,
        IPanelInputReader panelInputReader,
        IIoLinkageConfigService ioLinkageConfigService,
        ISystemStateManager stateManager,
        IPanelConfigurationRepository panelConfigRepository,
        ISafeExecutionService safeExecutor,
        ISystemClock systemClock,
        IOutputPort outputPort)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _panelInputReader = panelInputReader ?? throw new ArgumentNullException(nameof(panelInputReader));
        _ioLinkageConfigService = ioLinkageConfigService ?? throw new ArgumentNullException(nameof(ioLinkageConfigService));
        _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
        _panelConfigRepository = panelConfigRepository ?? throw new ArgumentNullException(nameof(panelConfigRepository));
        _safeExecutor = safeExecutor ?? throw new ArgumentNullException(nameof(safeExecutor));
        _systemClock = systemClock ?? throw new ArgumentNullException(nameof(systemClock));
        _outputPort = outputPort ?? throw new ArgumentNullException(nameof(outputPort));
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
    /// <remarks>
    /// 按钮优先级：急停 > 停止 > 启动
    /// 如果当前正在预警等待期间，高优先级按钮（停止、急停）会立即取消预警等待
    /// </remarks>
    private async Task TriggerIoLinkageAsync(PanelButtonType buttonType, CancellationToken cancellationToken)
    {
        try
        {
            // 按钮优先级处理：如果按下的是停止或急停，取消正在进行的预警等待
            if (buttonType is PanelButtonType.Stop or PanelButtonType.EmergencyStop)
            {
                lock (_preWarningLock)
                {
                    if (_preWarningCancellationSource != null && !_preWarningCancellationSource.IsCancellationRequested)
                    {
                        _logger.LogWarning(
                            "检测到高优先级按钮 {ButtonType}，取消正在进行的启动预警等待",
                            buttonType);
                        _preWarningCancellationSource.Cancel();
                    }
                }
            }
            
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
    /// 4. 无论是否正常结束，都确保关闭预警输出
    /// 5. 在预警等待期间，如果按下停止或急停按钮，预警等待会被取消
    /// </remarks>
    private async Task HandleStartButtonWithPreWarningAsync(SystemState currentState, CancellationToken cancellationToken)
    {
        var panelConfig = _panelConfigRepository.Get();
        var preWarningDuration = panelConfig?.PreStartWarningDurationSeconds;
        var warningOutputActivated = false;
        CancellationTokenSource? linkedCts = null;

        _logger.LogInformation(
            "启动按钮处理开始 - 当前状态: {CurrentState}, 配置的预警时间: {PreWarningDuration} 秒",
            currentState,
            preWarningDuration?.ToString() ?? "未配置");

        try
        {
            if (preWarningDuration.HasValue && preWarningDuration.Value > 0)
            {
                _logger.LogWarning(
                    "⚠️ 启动按钮按下，开始预警 {Duration} 秒，当前状态保持为 {CurrentState}，摆轮将在预警结束后启动",
                    preWarningDuration.Value,
                    currentState);

                // 记录预警开始时间用于验证
                var warningStartTime = _systemClock.LocalNow;

                // 触发预警输出
                if (panelConfig?.PreStartWarningOutputBit.HasValue == true)
                {
                    try
                    {
                        // 根据触发电平计算应该写入的值
                        // ActiveHigh: 高电平有效 -> 写入 true（点亮/启用）
                        // ActiveLow: 低电平有效 -> 写入 false（点亮/启用）
                        var outputValue = panelConfig.PreStartWarningOutputLevel == TriggerLevel.ActiveHigh;
                        
                        _logger.LogInformation(
                            "开启预警输出: Bit={OutputBit}, Level={OutputLevel}, Value={OutputValue}",
                            panelConfig.PreStartWarningOutputBit.Value,
                            panelConfig.PreStartWarningOutputLevel,
                            outputValue);

                        var writeSuccess = await _outputPort.WriteAsync(
                            panelConfig.PreStartWarningOutputBit.Value,
                            outputValue);

                        if (writeSuccess)
                        {
                            warningOutputActivated = true;
                        }
                        else
                        {
                            _logger.LogError(
                                "预警输出写入失败: Bit={OutputBit}",
                                panelConfig.PreStartWarningOutputBit.Value);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(
                            ex,
                            "写入预警输出异常: Bit={OutputBit}",
                            panelConfig.PreStartWarningOutputBit.Value);
                    }
                }

                // 创建可以被高优先级按钮取消的预警等待令牌
                lock (_preWarningLock)
                {
                    _preWarningCancellationSource = new CancellationTokenSource();
                    linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                        cancellationToken,
                        _preWarningCancellationSource.Token);
                }

                try
                {
                    // 等待预警时间（可被高优先级按钮取消）
                    _logger.LogInformation("开始等待预警时间: {Duration} 秒...", preWarningDuration.Value);
                    await Task.Delay(TimeSpan.FromSeconds(preWarningDuration.Value), linkedCts.Token);

                    var actualWaitTime = (_systemClock.LocalNow - warningStartTime).TotalSeconds;
                    _logger.LogWarning(
                        "✅ 预警时间结束，实际等待: {ActualWait:F2} 秒，准备转换到 Running 状态并启动摆轮",
                        actualWaitTime);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    // 预警等待被高优先级按钮取消（不是系统停止导致的取消）
                    // 判断逻辑：
                    // - 如果 cancellationToken 被取消，说明是系统停止，应该让异常继续传播
                    // - 如果 cancellationToken 未被取消，说明是内部预警取消源被触发（高优先级按钮），捕获并处理
                    var actualWaitTime = (_systemClock.LocalNow - warningStartTime).TotalSeconds;
                    _logger.LogWarning(
                        "⚠️ 预警等待被高优先级按钮（停止/急停）取消，实际等待: {ActualWait:F2} 秒",
                        actualWaitTime);
                    
                    // 不继续执行状态转换，由高优先级按钮处理
                    return;
                }
                finally
                {
                    // 清理预警取消令牌源
                    CleanupPreWarningCancellationSource();
                }
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
        finally
        {
            // 清理 linked cancellation token source
            linkedCts?.Dispose();
            
            // 无论如何都要关闭预警输出
            if (warningOutputActivated)
            {
                try
                {
                    // 关闭输出：与开启时相反的值
                    // ActiveHigh: 高电平有效 -> 写入 false（熄灭/禁用）
                    // ActiveLow: 低电平有效 -> 写入 true（熄灭/禁用）
                    var outputValue = panelConfig!.PreStartWarningOutputLevel != TriggerLevel.ActiveHigh;
                    
                    _logger.LogInformation(
                        "关闭预警输出: Bit={OutputBit}, Level={OutputLevel}, Value={OutputValue}",
                        panelConfig.PreStartWarningOutputBit!.Value,
                        panelConfig.PreStartWarningOutputLevel,
                        outputValue);

                    var writeSuccess = await _outputPort.WriteAsync(
                        panelConfig.PreStartWarningOutputBit.Value,
                        outputValue);

                    if (!writeSuccess)
                    {
                        _logger.LogError(
                            "关闭预警输出失败: Bit={OutputBit}",
                            panelConfig.PreStartWarningOutputBit.Value);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "关闭预警输出异常: Bit={OutputBit}",
                        panelConfig!.PreStartWarningOutputBit!.Value);
                }
            }
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
    
    /// <summary>
    /// 清理预警取消令牌源
    /// </summary>
    private void CleanupPreWarningCancellationSource()
    {
        lock (_preWarningLock)
        {
            _preWarningCancellationSource?.Dispose();
            _preWarningCancellationSource = null;
        }
    }
}
