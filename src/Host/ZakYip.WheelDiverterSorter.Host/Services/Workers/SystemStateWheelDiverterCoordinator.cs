using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Application.Services.Config;
using ZakYip.WheelDiverterSorter.Application.Services.WheelDiverter;
using ZakYip.WheelDiverterSorter.Core.Enums.System;
using ZakYip.WheelDiverterSorter.Host.StateMachine;
using ZakYip.WheelDiverterSorter.Observability.Utilities;

namespace ZakYip.WheelDiverterSorter.Host.Services.Workers;

/// <summary>
/// 系统状态与摆轮联动协调器后台服务
/// </summary>
/// <remarks>
/// 监控系统状态变化，自动控制摆轮设备的运行/停止。
/// 
/// **功能说明**：
/// - 定期检查系统状态，检测状态转换
/// - 当系统转换到 Running 状态时，自动调用所有摆轮的 Run 命令
/// - 当系统转换到 Stopped/Ready/EmergencyStop 状态时，自动调用所有摆轮的 Stop 命令
/// - 支持可配置的轮询间隔
/// 
/// **修复问题**：
/// - Issue 3: 运行时摆轮并没有进行联动（调用 POST /api/config/io-linkage/trigger?systemState=Running 时摆轮不 run）
/// 
/// **实施要求**：
/// - 使用 ISafeExecutionService 包裹后台任务循环，符合 copilot-instructions.md 第一节第3条
/// - 使用 ISystemClock 获取时间，符合 copilot-instructions.md 第一节第2条
/// </remarks>
public sealed class SystemStateWheelDiverterCoordinator : BackgroundService
{
    private readonly ILogger<SystemStateWheelDiverterCoordinator> _logger;
    private readonly ISystemStateManager _stateManager;
    private readonly IWheelDiverterConnectionService _wheelDiverterService;
    private readonly ISafeExecutionService _safeExecutor;
    private readonly ISystemConfigService _systemConfigService;
    
    /// <summary>
    /// 上次已知系统状态
    /// </summary>
    private SystemState _lastKnownState = SystemState.Booting;

    public SystemStateWheelDiverterCoordinator(
        ILogger<SystemStateWheelDiverterCoordinator> logger,
        ISystemStateManager stateManager,
        IWheelDiverterConnectionService wheelDiverterService,
        ISafeExecutionService safeExecutor,
        ISystemConfigService systemConfigService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
        _wheelDiverterService = wheelDiverterService ?? throw new ArgumentNullException(nameof(wheelDiverterService));
        _safeExecutor = safeExecutor ?? throw new ArgumentNullException(nameof(safeExecutor));
        _systemConfigService = systemConfigService ?? throw new ArgumentNullException(nameof(systemConfigService));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _safeExecutor.ExecuteAsync(
            async () =>
            {
                _logger.LogInformation("系统状态与摆轮联动协调器已启动");

                // 等待一小段时间，确保系统初始化完成
                await Task.Delay(1000, stoppingToken);
                
                // 初始化：获取当前状态并立即同步摆轮状态
                var initialState = _stateManager.CurrentState;
                _logger.LogInformation("初始系统状态: {State}，正在同步摆轮状态...", initialState);
                _lastKnownState = initialState;
                
                // 根据初始状态同步摆轮（确保摆轮状态与系统状态一致）
                await HandleStateTransitionAsync(initialState, stoppingToken);

                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        // 获取当前系统状态
                        var currentState = _stateManager.CurrentState;

                        // 检测状态变化
                        if (currentState != _lastKnownState)
                        {
                            _logger.LogInformation(
                                "检测到系统状态变化：{FromState} -> {ToState}",
                                _lastKnownState,
                                currentState);

                            // 处理状态转换
                            await HandleStateTransitionAsync(currentState, stoppingToken);

                            // 更新已知状态
                            _lastKnownState = currentState;
                        }

                        // 等待下一次检查（从数据库读取配置）
                        var workerConfig = _systemConfigService.GetSystemConfig().Worker;
                        await Task.Delay(workerConfig.StateCheckIntervalMs, stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        // 正常取消，退出循环
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "系统状态与摆轮联动协调器异常");
                        
                        // 发生异常后稍作延迟再继续（从数据库读取配置）
                        var workerConfig = _systemConfigService.GetSystemConfig().Worker;
                        await Task.Delay(workerConfig.ErrorRecoveryDelayMs, stoppingToken);
                    }
                }

                _logger.LogInformation("系统状态与摆轮联动协调器已停止");
            },
            "SystemStateWheelDiverterCoordinatorLoop",
            stoppingToken);
    }

    /// <summary>
    /// 处理系统状态转换
    /// </summary>
    private async Task HandleStateTransitionAsync(SystemState newState, CancellationToken cancellationToken)
    {
        try
        {
            switch (newState)
            {
                case SystemState.Running:
                    // 系统开始运行，启动所有摆轮并让它们向前（直通）
                    _logger.LogInformation("系统进入运行状态，启动所有摆轮设备...");
                    
                    // 先调用 Run 命令启动摆轮（仅对支持的设备）
                    var runResult = await _wheelDiverterService.RunAllAsync(cancellationToken);
                    
                    if (runResult.IsSuccess)
                    {
                        _logger.LogInformation(
                            "✅ 所有摆轮设备已启动: 成功={SuccessCount}/{TotalCount}",
                            runResult.SuccessCount,
                            runResult.TotalCount);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "⚠️ 摆轮设备部分启动: 成功={SuccessCount}/{TotalCount}, 失败={FailedCount}",
                            runResult.SuccessCount,
                            runResult.TotalCount,
                            runResult.FailedDriverIds.Count);
                        
                        if (runResult.FailedDriverIds.Any())
                        {
                            _logger.LogWarning(
                                "启动失败的摆轮ID: {FailedIds}",
                                string.Join(", ", runResult.FailedDriverIds));
                        }
                    }
                    
                    // 然后让所有摆轮向前（直通）
                    _logger.LogInformation("设置所有摆轮为向前（直通）状态...");
                    var passThroughResult = await _wheelDiverterService.PassThroughAllAsync(cancellationToken);
                    
                    if (passThroughResult.IsSuccess)
                    {
                        _logger.LogInformation(
                            "✅ 所有摆轮已设置为向前: 成功={SuccessCount}/{TotalCount}",
                            passThroughResult.SuccessCount,
                            passThroughResult.TotalCount);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "⚠️ 摆轮部分设置为向前: 成功={SuccessCount}/{TotalCount}, 失败={FailedCount}",
                            passThroughResult.SuccessCount,
                            passThroughResult.TotalCount,
                            passThroughResult.FailedDriverIds.Count);
                        
                        if (passThroughResult.FailedDriverIds.Any())
                        {
                            _logger.LogWarning(
                                "设置向前失败的摆轮ID: {FailedIds}",
                                string.Join(", ", passThroughResult.FailedDriverIds));
                        }
                    }
                    break;

                case SystemState.Ready:
                case SystemState.EmergencyStop:
                case SystemState.Faulted:
                    // 系统停止/急停/故障，停止所有摆轮
                    _logger.LogInformation(
                        "系统进入{State}状态，停止所有摆轮设备...",
                        newState);
                    
                    var stopResult = await _wheelDiverterService.StopAllAsync(cancellationToken);
                    
                    if (stopResult.IsSuccess)
                    {
                        _logger.LogInformation(
                            "✅ 所有摆轮设备已停止: 成功={SuccessCount}/{TotalCount}",
                            stopResult.SuccessCount,
                            stopResult.TotalCount);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "⚠️ 摆轮设备部分停止: 成功={SuccessCount}/{TotalCount}, 失败={FailedCount}",
                            stopResult.SuccessCount,
                            stopResult.TotalCount,
                            stopResult.FailedDriverIds.Count);
                        
                        if (stopResult.FailedDriverIds.Any())
                        {
                            _logger.LogWarning(
                                "停止失败的摆轮ID: {FailedIds}",
                                string.Join(", ", stopResult.FailedDriverIds));
                        }
                    }
                    break;

                case SystemState.Paused:
                    // 系统暂停，可选择停止摆轮或保持运行
                    // 当前实现：保持摆轮运行状态不变
                    _logger.LogDebug("系统进入暂停状态，摆轮设备保持当前状态");
                    break;

                case SystemState.Booting:
                    // 系统启动中，摆轮由 WheelDiverterInitHostedService 处理连接
                    _logger.LogDebug("系统启动中，摆轮设备由初始化服务处理");
                    break;

                default:
                    _logger.LogDebug("系统状态 {State} 无需摆轮联动处理", newState);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "处理系统状态转换异常: {State}",
                newState);
        }
    }
}
