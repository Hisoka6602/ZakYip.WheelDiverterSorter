using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Application.Services.Config;
using ZakYip.WheelDiverterSorter.Core.Enums.System;
using ZakYip.WheelDiverterSorter.Host.StateMachine;
using ZakYip.WheelDiverterSorter.Ingress;
using ZakYip.WheelDiverterSorter.Observability.Utilities;

namespace ZakYip.WheelDiverterSorter.Host.Services.Workers;

/// <summary>
/// 传感器激活后台服务
/// </summary>
/// <remarks>
/// 在程序启动时立即启动传感器服务，无需等待系统进入 Running 状态。
/// 
/// **功能说明**：
/// - 程序启动时立即启动传感器检测（与面板IO监控一致）
/// - 包裹创建逻辑会在后续流程中根据系统状态进行拦截
/// - 摆轮执行逻辑也会在后续流程中根据系统状态进行拦截
/// - 传感器保持持续运行，不受系统状态变化影响
/// 
/// **设计原则**：
/// - 传感器和面板IO属于同一性质的监控，都在程序启动时立即生效
/// - 状态拦截在业务逻辑层实现，而非传感器层
/// 
/// **修复问题**：
/// - Issue 4: 创建包裹感应器没有创建包裹（传感器服务未被启动）
/// - Issue 5: 所有感应器好像都没有被用上（传感器服务未被激活）
/// 
/// **实施要求**：
/// - 使用 ISafeExecutionService 包裹后台任务循环，符合 copilot-instructions.md 第一节第3条
/// </remarks>
public sealed class SensorActivationWorker : BackgroundService
{
    private readonly ILogger<SensorActivationWorker> _logger;
    private readonly ISystemStateManager _stateManager;
    private readonly IParcelDetectionService _parcelDetectionService;
    private readonly ISafeExecutionService _safeExecutor;
    private readonly ISystemConfigService _systemConfigService;
    
    /// <summary>
    /// 上次已知系统状态
    /// </summary>
    private SystemState _lastKnownState = SystemState.Booting;
    
    /// <summary>
    /// 传感器服务是否正在运行
    /// </summary>
    private bool _sensorsRunning = false;

    public SensorActivationWorker(
        ILogger<SensorActivationWorker> logger,
        ISystemStateManager stateManager,
        IParcelDetectionService parcelDetectionService,
        ISafeExecutionService safeExecutor,
        ISystemConfigService systemConfigService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
        _parcelDetectionService = parcelDetectionService ?? throw new ArgumentNullException(nameof(parcelDetectionService));
        _safeExecutor = safeExecutor ?? throw new ArgumentNullException(nameof(safeExecutor));
        _systemConfigService = systemConfigService ?? throw new ArgumentNullException(nameof(systemConfigService));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _safeExecutor.ExecuteAsync(
            async () =>
            {
                _logger.LogInformation("传感器激活服务已启动");

                // 等待一小段时间，确保系统初始化完成
                await Task.Delay(1000, stoppingToken);
                
                // 初始化：获取当前状态
                var initialState = _stateManager.CurrentState;
                _logger.LogInformation("初始系统状态: {State}", initialState);
                _lastKnownState = initialState;
                
                // 立即启动传感器服务，无需等待系统进入 Running 状态
                // 包裹创建逻辑会在后续流程中根据系统状态进行拦截
                _logger.LogInformation("程序启动时立即启动传感器检测");
                await StartSensorsAsync(stoppingToken);

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
                        _logger.LogError(ex, "传感器激活服务异常");
                        
                        // 发生异常后稍作延迟再继续（从数据库读取配置）
                        var workerConfig = _systemConfigService.GetSystemConfig().Worker;
                        await Task.Delay(workerConfig.ErrorRecoveryDelayMs, stoppingToken);
                    }
                }

                // 停止时确保传感器服务也停止
                if (_sensorsRunning)
                {
                    _logger.LogInformation("服务停止，正在停止传感器...");
                    await StopSensorsAsync();
                }

                _logger.LogInformation("传感器激活服务已停止");
            },
            "SensorActivationWorkerLoop",
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
                    // 系统开始运行，确保传感器服务已启动
                    // 注意：传感器在程序启动时已经启动，此处仅作为保险
                    if (!_sensorsRunning)
                    {
                        _logger.LogWarning("传感器服务未运行，立即启动");
                        await StartSensorsAsync(cancellationToken);
                    }
                    break;

                case SystemState.Ready:
                case SystemState.EmergencyStop:
                case SystemState.Faulted:
                    // 注意：传感器保持运行状态，不停止
                    // 包裹创建逻辑会在后续流程中根据系统状态进行拦截
                    _logger.LogInformation(
                        "系统进入{State}状态，传感器保持运行状态（包裹创建会被拦截）",
                        newState);
                    break;

                case SystemState.Paused:
                    // 系统暂停，传感器保持运行
                    _logger.LogDebug("系统进入暂停状态，传感器保持运行");
                    break;

                case SystemState.Booting:
                    // 系统启动中，传感器已在程序启动时启动
                    _logger.LogDebug("系统启动中，传感器已在程序启动时启动");
                    break;

                default:
                    _logger.LogDebug("系统状态 {State}，传感器保持运行", newState);
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

    /// <summary>
    /// 启动传感器服务
    /// </summary>
    private async Task StartSensorsAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("系统进入运行状态，启动传感器服务...");
            
            await _parcelDetectionService.StartAsync(cancellationToken);
            _sensorsRunning = true;
            
            _logger.LogInformation("✅ 传感器服务已启动");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "启动传感器服务失败");
            _sensorsRunning = false;
        }
    }

    /// <summary>
    /// 停止传感器服务
    /// </summary>
    private async Task StopSensorsAsync()
    {
        try
        {
            await _parcelDetectionService.StopAsync();
            _sensorsRunning = false;
            
            _logger.LogInformation("✅ 传感器服务已停止");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "停止传感器服务失败");
        }
    }
}
