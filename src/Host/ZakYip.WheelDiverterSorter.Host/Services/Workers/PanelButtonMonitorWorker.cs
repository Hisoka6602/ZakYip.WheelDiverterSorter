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
using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using ZakYip.WheelDiverterSorter.Core.Hardware.Ports;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Upstream;

namespace ZakYip.WheelDiverterSorter.Host.Services.Workers;

/// <summary>
/// 面板按钮监控后台服务
/// </summary>
/// <remarks>
/// 定期轮询面板按钮状态，当检测到按钮按下时触发相应的IO联动动作。
/// 轮询间隔已硬编码为10ms，以确保实时响应。
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
    private readonly IUpstreamRoutingClient? _upstreamClient;
    
    /// <summary>
    /// 面板按钮轮询间隔（毫秒），硬编码为10ms以确保实时响应
    /// </summary>
    private const int PollingIntervalMs = 10;
    
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
    
    /// <summary>
    /// 预警进行中标志，用于防止多次并发启动预警（0=未运行, 1=运行中）
    /// </summary>
    private int _preWarningInProgress = 0;

    public PanelButtonMonitorWorker(
        ILogger<PanelButtonMonitorWorker> logger,
        IPanelInputReader panelInputReader,
        IIoLinkageConfigService ioLinkageConfigService,
        ISystemStateManager stateManager,
        IPanelConfigurationRepository panelConfigRepository,
        ISafeExecutionService safeExecutor,
        ISystemClock systemClock,
        IOutputPort outputPort,
        IUpstreamRoutingClient? upstreamClient = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _panelInputReader = panelInputReader ?? throw new ArgumentNullException(nameof(panelInputReader));
        _ioLinkageConfigService = ioLinkageConfigService ?? throw new ArgumentNullException(nameof(ioLinkageConfigService));
        _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
        _panelConfigRepository = panelConfigRepository ?? throw new ArgumentNullException(nameof(panelConfigRepository));
        _safeExecutor = safeExecutor ?? throw new ArgumentNullException(nameof(safeExecutor));
        _systemClock = systemClock ?? throw new ArgumentNullException(nameof(systemClock));
        _outputPort = outputPort ?? throw new ArgumentNullException(nameof(outputPort));
        _upstreamClient = upstreamClient; // 可选依赖
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

                                // 检查：如果当前处于急停状态，且按下的不是急停按钮，则触发急停蜂鸣
                                var currentSystemState = _stateManager.CurrentState;
                                if (currentSystemState == SystemState.EmergencyStop && 
                                    buttonType != PanelButtonType.EmergencyStop)
                                {
                                    _logger.LogWarning(
                                        "系统处于急停状态时按下非急停按钮 {ButtonType}，触发急停蜂鸣提醒",
                                        buttonType);
                                    
                                    // 触发急停蜂鸣器
                                    await TriggerEmergencyStopBuzzerAsync(stoppingToken);
                                }

                                // 触发IO联动
                                await TriggerIoLinkageAsync(buttonType, stoppingToken);
                            }
                            // 检测急停按钮从按下到释放的状态变化（下降沿）
                            else if (!isPressed && wasPressed && buttonType == PanelButtonType.EmergencyStop)
                            {
                                _logger.LogInformation(
                                    "检测到急停按钮解除：{ButtonType}，触发Reset状态转换",
                                    buttonType);

                                // 急停解除时，触发Reset按钮的IO联动以自动恢复系统
                                await TriggerIoLinkageAsync(PanelButtonType.Reset, stoppingToken);
                            }

                            // 更新状态缓存
                            _lastButtonStates[buttonType] = isPressed;
                        }

                        // 使用硬编码的轮询间隔等待下一次轮询
                        await Task.Delay(PollingIntervalMs, stoppingToken);
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
    /// 按钮按下时会通知上游系统（包含按钮类型、时间、状态变化）
    /// 
    /// 修复: 启动按钮的IO联动在预警结束后触发，而不是按钮按下时触发
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
            var pressedAt = _systemClock.LocalNow;
            
            _logger.LogInformation(
                "[面板按钮] 用户按下按钮: {ButtonType}, 时间: {PressedAt:HH:mm:ss.fff}, 当前系统状态: {SystemState}",
                buttonType,
                pressedAt,
                currentState);

            // 首先处理按钮的主要功能（状态转换）
            await HandleButtonActionAsync(buttonType, currentState, cancellationToken);

            // 特殊处理启动按钮：上游通知立即发送，但IO联动在预警结束后触发
            if (buttonType == PanelButtonType.Start && currentState is SystemState.Ready or SystemState.Paused)
            {
                _logger.LogInformation(
                    "[面板按钮] 启动按钮按下，立即发送上游通知，IO联动将在预警结束后触发");
                
                // 启动按钮的上游通知需要立即发送（状态尚未改变，仍为 Ready/Paused）
                await NotifyUpstreamPanelButtonPressedAsync(
                    buttonType, 
                    pressedAt, 
                    currentState, 
                    currentState,  // 状态尚未改变
                    cancellationToken);
                
                return; // 跳过 IO 联动
            }

            // 状态转换后，获取新的系统状态用于IO联动和上游通知
            var newState = _stateManager.CurrentState;
            var operatingState = MapToOperatingState(newState);
            
            _logger.LogInformation(
                "[面板按钮] 按钮 {ButtonType} 状态已转换: {OldState} -> {NewState}，准备触发 {OperatingState} 状态的IO联动",
                buttonType,
                currentState,
                newState,
                operatingState);

            // 通知上游系统按钮按下事件（fire-and-forget）
            await NotifyUpstreamPanelButtonPressedAsync(buttonType, pressedAt, currentState, newState, cancellationToken);

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
    /// 修复Issue: 启动预警期间按下停止/急停应立即取消预警（使用fire-and-forget避免阻塞按钮监控循环）
    /// </remarks>
    private async Task HandleButtonActionAsync(PanelButtonType buttonType, SystemState currentState, CancellationToken cancellationToken)
    {
        try
        {
            // 特殊处理启动按钮：需要预警逻辑（使用fire-and-forget避免阻塞）
            if (buttonType == PanelButtonType.Start && currentState is SystemState.Ready or SystemState.Paused)
            {
                // 防止并发启动多个预警任务
                if (Interlocked.CompareExchange(ref _preWarningInProgress, 1, 0) == 1)
                {
                    _logger.LogWarning("启动按钮被按下，但预警任务已在进行中，忽略此次按下");
                    return;
                }
                
                // 启动预警过程（非阻塞，使用SafeExecutionService确保异常被捕获）
                _ = _safeExecutor.ExecuteAsync(
                    async () =>
                    {
                        try
                        {
                            await HandleStartButtonWithPreWarningAsync(currentState, cancellationToken);
                        }
                        finally
                        {
                            // 确保预警完成或取消后重置标志
                            Interlocked.Exchange(ref _preWarningInProgress, 0);
                            _logger.LogDebug("预警任务已结束，预警进行中标志已重置");
                        }
                    },
                    "StartButtonPreWarning",
                    cancellationToken);
                return;
            }

            // 其他按钮的正常处理
            SystemState? targetState = buttonType switch
            {
                PanelButtonType.Stop when currentState is SystemState.Running or SystemState.Paused =>
                    SystemState.Ready,
                
                PanelButtonType.Reset when currentState is SystemState.Faulted or SystemState.EmergencyStop =>
                    SystemState.Ready,
                
                // 急停按钮在任何状态下都必须生效（最高优先级，安全第一）
                PanelButtonType.EmergencyStop =>
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
                else if (targetState.Value == SystemState.EmergencyStop && currentState != SystemState.EmergencyStop)
                {
                    // 当从其他状态转换到急停状态时，触发蜂鸣
                    await TriggerEmergencyStopBuzzerAsync(cancellationToken);
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
    /// 
    /// 修复: 使用fire-and-forget模式调用，避免阻塞按钮监控循环
    /// - 在单独的任务中运行预警等待
    /// - 确保异常被正确捕获和记录
    /// - 确保取消时立即清理资源
    /// </remarks>
    private async Task HandleStartButtonWithPreWarningAsync(SystemState currentState, CancellationToken cancellationToken)
    {
        var panelConfig = _panelConfigRepository.Get();
        var preWarningDuration = panelConfig?.PreStartWarningDurationSeconds;
        var warningOutputActivated = false;
        var warningStartTime = _systemClock.LocalNow; // 记录预警开始时间（按钮按下时间）

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
                // 先创建所有对象，再在锁内赋值，避免竞态条件
                using var newSource = new CancellationTokenSource();
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    newSource.Token);
                
                lock (_preWarningLock)
                {
                    _preWarningCancellationSource = newSource;
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
            var stateChangeResult = await _stateManager.ChangeStateAsync(SystemState.Running, cancellationToken);
            
            if (!stateChangeResult.Success)
            {
                _logger.LogError(
                    "❌ 启动按钮状态转换失败: {ErrorMessage}",
                    stateChangeResult.ErrorMessage);
                return;
            }
            
            _logger.LogInformation("✅ 系统状态已成功转换到 Running，准备触发IO联动和发送状态变更通知");

            // 状态转换成功后，触发 Running 状态的 IO 联动
            try
            {
                // 注意：启动按钮的上游通知流程与其他按钮不同
                // - 第一次通知：在按钮按下时立即发送（Ready → Ready），通知上游"用户已按下启动按钮，预警开始"
                // - 第二次通知：在预警结束、状态转换到 Running 后发送（Ready → Running），通知上游"预警结束，系统已启动"
                // 这样上游系统可以完整追踪启动流程：按钮按下 → 预警中 → 实际启动
                
                // 发送状态变更通知（Ready/Paused → Running）
                await NotifyUpstreamPanelButtonPressedAsync(
                    PanelButtonType.Start,
                    warningStartTime,
                    currentState,
                    SystemState.Running,
                    cancellationToken);
                
                // 触发 Running 状态的 IO 联动
                var ioLinkageResult = await _ioLinkageConfigService.TriggerIoLinkageAsync(SystemState.Running);
                
                if (ioLinkageResult.Success)
                {
                    _logger.LogInformation(
                        "启动按钮的IO联动触发成功，触发了 {Count} 个IO点",
                        ioLinkageResult.TriggeredIoPoints.Count);
                }
                else
                {
                    _logger.LogWarning(
                        "启动按钮的IO联动触发失败：{ErrorMessage}",
                        ioLinkageResult.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "触发启动按钮的IO联动或状态变更通知异常");
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
    /// 将 SystemState 映射到 SystemState
    /// </summary>
    private static SystemState MapToOperatingState(SystemState state)
    {
        return state switch
        {
            SystemState.Booting => SystemState.Booting,
            SystemState.Ready => SystemState.Ready,
            SystemState.Running => SystemState.Running,
            SystemState.Paused => SystemState.Paused,
            SystemState.Faulted => SystemState.Faulted,
            SystemState.EmergencyStop => SystemState.EmergencyStop,
            _ => SystemState.Ready
        };
    }
    
    /// <summary>
    /// 触发急停蜂鸣
    /// </summary>
    /// <remarks>
    /// 当系统从其他状态转换到急停状态时触发蜂鸣器：
    /// 1. 读取面板配置获取蜂鸣器输出位和持续时间
    /// 2. 启动蜂鸣器输出
    /// 3. 等待配置的持续时间
    /// 4. 关闭蜂鸣器输出
    /// 
    /// 如果未配置蜂鸣器输出位或持续时间，则不执行蜂鸣。
    /// 蜂鸣过程在后台异步执行，不会阻塞急停状态转换。
    /// </remarks>
    private async Task TriggerEmergencyStopBuzzerAsync(CancellationToken cancellationToken)
    {
        try
        {
            var panelConfig = _panelConfigRepository.Get();
            
            // 检查是否配置了蜂鸣器
            if (panelConfig?.EmergencyStopBuzzerOutputBit == null || 
                panelConfig.EmergencyStopBuzzerDurationSeconds == null ||
                panelConfig.EmergencyStopBuzzerDurationSeconds.Value <= 0)
            {
                _logger.LogDebug("未配置急停蜂鸣器或蜂鸣持续时间，跳过蜂鸣");
                return;
            }

            var buzzerBit = panelConfig.EmergencyStopBuzzerOutputBit.Value;
            var buzzerDuration = panelConfig.EmergencyStopBuzzerDurationSeconds.Value;
            var buzzerLevel = panelConfig.EmergencyStopBuzzerOutputLevel;
            
            _logger.LogWarning(
                "⚠️ 急停触发！开启蜂鸣器: Bit={BuzzerBit}, Duration={Duration}秒, Level={Level}",
                buzzerBit,
                buzzerDuration,
                buzzerLevel);

            // 启动蜂鸣器（根据触发电平决定值）
            // ActiveHigh: 高电平有效 -> 写入 true（启动蜂鸣）
            // ActiveLow: 低电平有效 -> 写入 false（启动蜂鸣）
            var activateValue = buzzerLevel == TriggerLevel.ActiveHigh;
            
            var writeSuccess = await _outputPort.WriteAsync(buzzerBit, activateValue);
            
            if (!writeSuccess)
            {
                _logger.LogError("启动急停蜂鸣器失败: Bit={BuzzerBit}", buzzerBit);
                return;
            }
            
            _logger.LogInformation("急停蜂鸣器已启动，将在 {Duration} 秒后自动关闭", buzzerDuration);

            // 在后台异步等待并关闭蜂鸣器（不阻塞当前流程）
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(buzzerDuration), cancellationToken);
                    
                    // 关闭蜂鸣器（与启动时相反的值）
                    var deactivateValue = buzzerLevel != TriggerLevel.ActiveHigh;
                    var stopSuccess = await _outputPort.WriteAsync(buzzerBit, deactivateValue);
                    
                    if (stopSuccess)
                    {
                        _logger.LogInformation("急停蜂鸣器已自动关闭");
                    }
                    else
                    {
                        _logger.LogWarning("关闭急停蜂鸣器失败: Bit={BuzzerBit}", buzzerBit);
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogDebug("急停蜂鸣器关闭任务被取消");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "关闭急停蜂鸣器异常: Bit={BuzzerBit}", buzzerBit);
                }
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "触发急停蜂鸣器异常");
        }
    }
    
    /// <summary>
    /// 通知上游系统面板按钮按下事件
    /// </summary>
    /// <param name="buttonType">按钮类型</param>
    /// <param name="pressedAt">按下时间</param>
    /// <param name="stateBefore">按下前的系统状态</param>
    /// <param name="stateAfter">按下后的系统状态</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <remarks>
    /// Fire-and-forget模式：发送失败只记录日志，不影响按钮功能
    /// </remarks>
    private async Task NotifyUpstreamPanelButtonPressedAsync(
        PanelButtonType buttonType,
        DateTime pressedAt,
        SystemState stateBefore,
        SystemState stateAfter,
        CancellationToken cancellationToken)
    {
        if (_upstreamClient == null)
        {
            _logger.LogDebug("[面板按钮] 未配置上游客户端，跳过按钮按下通知");
            return;
        }

        try
        {
            var message = new PanelButtonPressedMessage
            {
                ButtonType = buttonType,
                PressedAt = new DateTimeOffset(pressedAt),
                SystemStateBefore = stateBefore,
                SystemStateAfter = stateAfter
            };

            _logger.LogInformation(
                "[面板按钮-上游通知] 发送按钮按下通知: Button={ButtonType}, Time={PressedAt:HH:mm:ss.fff}, State={Before}->{After}",
                buttonType,
                pressedAt,
                stateBefore,
                stateAfter);

            var success = await _upstreamClient.SendAsync(message, cancellationToken);

            if (success)
            {
                _logger.LogInformation(
                    "[面板按钮-上游通知] 按钮按下通知发送成功: {ButtonType}",
                    buttonType);
            }
            else
            {
                _logger.LogWarning(
                    "[面板按钮-上游通知] 按钮按下通知发送失败: {ButtonType}",
                    buttonType);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[面板按钮-上游通知] 发送按钮按下通知异常: {ButtonType}",
                buttonType);
        }
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
    
    /// <summary>
    /// 释放资源，确保取消令牌源被正确清理
    /// </summary>
    public override void Dispose()
    {
        CleanupPreWarningCancellationSource();
        Interlocked.Exchange(ref _preWarningInProgress, 0);
        base.Dispose();
    }
}
