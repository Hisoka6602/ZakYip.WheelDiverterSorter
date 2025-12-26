using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Enums.System;
using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using ZakYip.WheelDiverterSorter.Observability.Utilities;
using ZakYip.WheelDiverterSorter.Application.Services.WheelDiverter;
using ZakYip.WheelDiverterSorter.Core.Utilities;

namespace ZakYip.WheelDiverterSorter.Host.Services.Workers;

/// <summary>
/// 系统状态与摆轮协调后台服务
/// </summary>
/// <remarks>
/// 监控系统状态转换，当系统进入 Running 状态时自动将所有摆轮设置为直行（PassThrough）。
/// 在非Running状态下（Ready, Paused, Faulted, EmergencyStop）也会定期检查并自动重新连接摆轮。
///
/// <para><b>设计目的</b>：</para>
/// <list type="bullet">
///   <item>确保系统启动时摆轮处于安全的直行状态</item>
///   <item>支持不通过 IO 联动控制摆轮的厂商（如某些厂商仅支持 Modbus/TCP 控制）</item>
///   <item>在 Ready→Running 和 Paused→Running 状态转换时都会触发</item>
///   <item>在非Running状态下自动重新连接摆轮（PR-stopped-auto-reconnect）</item>
/// </list>
///
/// <para><b>触发场景</b>：</para>
/// <list type="bullet">
///   <item>面板启动按钮按下（Ready → Running）</item>
///   <item>API 调用启动系统（Ready → Running）</item>
///   <item>系统从暂停恢复运行（Paused → Running）</item>
///   <item>系统在非Running状态（Ready, Paused等）下定期重新连接摆轮（PR-stopped-auto-reconnect）</item>
/// </list>
/// </remarks>
public sealed class SystemStateWheelDiverterCoordinator : BackgroundService
{
    private readonly ISystemStateManager _stateManager;
    private readonly IWheelDiverterConnectionService _wheelDiverterService;
    private readonly ISafeExecutionService _safeExecutor;
    private readonly ILogger<SystemStateWheelDiverterCoordinator> _logger;
    private readonly ISystemClock _clock;

    /// <summary>
    /// 轮询间隔（毫秒）
    /// </summary>
    private const int PollingIntervalMs = 200;

    /// <summary>
    /// 非Running状态下重新连接摆轮的间隔（毫秒）
    /// PR-stopped-auto-reconnect: 在非Running状态（Ready, Paused等）下也定期重新连接摆轮
    /// </summary>
    private const int StoppedStateReconnectIntervalMs = 5000;

    /// <summary>
    /// 异常恢复延迟（毫秒）
    /// </summary>
    private const int ExceptionRetryDelayMs = 1000;

    /// <summary>
    /// 上次记录的系统状态
    /// </summary>
    private SystemState _lastKnownState = SystemState.Booting;
    
    /// <summary>
    /// 上次在非Running状态下尝试重新连接的时间
    /// PR-stopped-auto-reconnect
    /// </summary>
    private DateTime _lastStoppedReconnectAttempt;

    public SystemStateWheelDiverterCoordinator(
        ISystemStateManager stateManager,
        IWheelDiverterConnectionService wheelDiverterService,
        ISafeExecutionService safeExecutor,
        ILogger<SystemStateWheelDiverterCoordinator> logger,
        ISystemClock clock)
    {
        _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
        _wheelDiverterService = wheelDiverterService ?? throw new ArgumentNullException(nameof(wheelDiverterService));
        _safeExecutor = safeExecutor ?? throw new ArgumentNullException(nameof(safeExecutor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        
        // 初始化为一个足够早的时间，确保首次检查时会立即触发重连
        _lastStoppedReconnectAttempt = _clock.LocalNow.AddSeconds(-StoppedStateReconnectIntervalMs / 1000.0 - 1);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _safeExecutor.ExecuteAsync(
            async () =>
            {
                _logger.LogInformation("系统状态与摆轮协调服务已启动");

                // 初始化时记录当前状态
                _lastKnownState = _stateManager.CurrentState;
                _logger.LogDebug("初始系统状态: {State}", _lastKnownState);

                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        var currentState = _stateManager.CurrentState;

                        // 检测状态变化
                        if (currentState != _lastKnownState)
                        {
                            _logger.LogInformation(
                                "检测到系统状态变化: {FromState} → {ToState}",
                                _lastKnownState,
                                currentState);

                            // 当系统进入 Running 状态时，启动所有摆轮并设置为直行
                            // 注意：外层已检查 currentState != _lastKnownState，确保只在状态转换时触发
                            if (currentState == SystemState.Running)
                            {
                                _logger.LogInformation(
                                    "系统状态转换到 Running，准备启动摆轮 (从 {FromState} → {ToState})",
                                    _lastKnownState,
                                    currentState);
                                await StartAndInitializeWheelDivertersAsync(stoppingToken);
                            }
                            // 当系统从 Running 状态切换到其他状态时，停止所有摆轮
                            else if (_lastKnownState == SystemState.Running)
                            {
                                _logger.LogInformation(
                                    "系统状态离开 Running，准备停止摆轮 (从 {FromState} → {ToState})",
                                    _lastKnownState,
                                    currentState);
                                await StopAllWheelDivertersAsync(stoppingToken);
                            }

                            // 更新上次记录的状态
                            _lastKnownState = currentState;
                        }
                        // PR-stopped-auto-reconnect: 在非Running状态下定期重新连接摆轮
                        // 包括 Ready, Paused, Faulted, EmergencyStop 等状态
                        else if (currentState != SystemState.Running)
                        {
                            var timeSinceLastReconnect = _clock.LocalNow - _lastStoppedReconnectAttempt;
                            if (timeSinceLastReconnect.TotalMilliseconds >= StoppedStateReconnectIntervalMs)
                            {
                                _logger.LogDebug(
                                    "系统处于非Running状态（{CurrentState}），定期重新连接摆轮（上次尝试: {TimeSince:F1}秒前）",
                                    currentState,
                                    timeSinceLastReconnect.TotalSeconds);
                                
                                await ReconnectWheelDivertersInNonRunningStateAsync(currentState, stoppingToken);
                                _lastStoppedReconnectAttempt = _clock.LocalNow;
                            }
                        }

                        // 等待下一次轮询
                        await Task.Delay(PollingIntervalMs, stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        // 正常取消，退出循环
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "系统状态监控异常");

                        // 异常后稍作延迟再继续
                        await Task.Delay(ExceptionRetryDelayMs, stoppingToken);
                    }
                }

                _logger.LogInformation("系统状态与摆轮协调服务已停止");
            },
            "SystemStateWheelDiverterCoordinatorLoop",
            stoppingToken);
    }

    /// <summary>
    /// 启动并初始化所有摆轮为直行状态
    /// </summary>
    /// <remarks>
    /// 当系统进入 Running 状态时调用，执行以下步骤：
    /// 1. 先调用 RunAsync 启动所有摆轮运行
    /// 2. 再调用 PassThroughAsync 将所有摆轮设置为直行状态
    ///
    /// 此操作是异步的，如果部分摆轮操作失败，会记录警告日志但不会阻止系统运行。
    /// </remarks>
    private async Task StartAndInitializeWheelDivertersAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("⚙️ 系统进入 Running 状态，正在启动所有摆轮并设置为直行...");

            // 步骤 1: 启动所有摆轮运行
            var runResult = await _wheelDiverterService.RunAllAsync(cancellationToken);

            if (runResult.IsSuccess)
            {
                _logger.LogInformation(
                    "✅ 所有摆轮已成功启动运行: {SuccessCount}/{TotalCount}",
                    runResult.SuccessCount,
                    runResult.TotalCount);
            }
            else
            {
                _logger.LogWarning(
                    "⚠️ 部分摆轮启动失败: 成功={SuccessCount}/{TotalCount}, 失败={FailedCount}",
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

            await Task.Delay(100, cancellationToken);
            // 步骤 2: 设置所有摆轮为直行状态
            var passThroughResult = await _wheelDiverterService.PassThroughAllAsync(cancellationToken);

            if (passThroughResult.IsSuccess)
            {
                _logger.LogInformation(
                    "✅ 所有摆轮已成功设置为直行状态: {SuccessCount}/{TotalCount}",
                    passThroughResult.SuccessCount,
                    passThroughResult.TotalCount);
            }
            else
            {
                _logger.LogWarning(
                    "⚠️ 部分摆轮设置为直行失败: 成功={SuccessCount}/{TotalCount}, 失败={FailedCount}",
                    passThroughResult.SuccessCount,
                    passThroughResult.TotalCount,
                    passThroughResult.FailedDriverIds.Count);

                if (passThroughResult.FailedDriverIds.Any())
                {
                    _logger.LogWarning(
                        "设置失败的摆轮ID: {FailedIds}",
                        string.Join(", ", passThroughResult.FailedDriverIds));
                }

                if (!string.IsNullOrEmpty(passThroughResult.ErrorMessage))
                {
                    _logger.LogWarning("错误信息: {ErrorMessage}", passThroughResult.ErrorMessage);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "❌ 启动并初始化摆轮时发生异常。系统将继续运行，但摆轮可能未处于正确状态。");
        }
    }

    /// <summary>
    /// 停止所有摆轮
    /// </summary>
    /// <remarks>
    /// 当系统从 Running 状态切换到 Stopped/EmergencyStop/Fault 等状态时调用。
    /// 调用所有摆轮的 StopAsync 方法以停止运行。
    ///
    /// 此操作是异步的，如果部分摆轮停止失败，会记录警告日志。
    /// </remarks>
    private async Task StopAllWheelDivertersAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation(
                "🛑 系统退出 Running 状态（当前状态: {CurrentState}），正在停止所有摆轮...",
                _lastKnownState);

            var result = await _wheelDiverterService.StopAllAsync(cancellationToken);

            if (result.IsSuccess)
            {
                _logger.LogInformation(
                    "✅ 所有摆轮已成功停止: {SuccessCount}/{TotalCount}",
                    result.SuccessCount,
                    result.TotalCount);
            }
            else
            {
                _logger.LogWarning(
                    "⚠️ 部分摆轮停止失败: 成功={SuccessCount}/{TotalCount}, 失败={FailedCount}",
                    result.SuccessCount,
                    result.TotalCount,
                    result.FailedDriverIds.Count);

                if (result.FailedDriverIds.Any())
                {
                    _logger.LogWarning(
                        "停止失败的摆轮ID: {FailedIds}",
                        string.Join(", ", result.FailedDriverIds));
                }

                if (!string.IsNullOrEmpty(result.ErrorMessage))
                {
                    _logger.LogWarning("错误信息: {ErrorMessage}", result.ErrorMessage);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "❌ 停止摆轮时发生异常。部分摆轮可能仍在运行。");
        }
    }

    /// <summary>
    /// 在非Running状态下重新连接摆轮
    /// </summary>
    /// <remarks>
    /// PR-stopped-auto-reconnect: 在系统处于非Running状态时（Ready, Paused, Faulted, EmergencyStop等），
    /// 定期尝试重新连接摆轮。这确保了即使在系统停止状态下，摆轮也能自动重新连接（例如摆轮重启或网络恢复后）。
    ///
    /// <para><b>执行步骤</b>：</para>
    /// <list type="number">
    ///   <item>尝试连接所有摆轮（ConnectAllAsync）</item>
    ///   <item>不启动摆轮运行（仅连接，不调用 RunAsync）</item>
    ///   <item>保持摆轮处于停止状态，等待系统进入 Running 状态时再启动</item>
    /// </list>
    /// </remarks>
    private async Task ReconnectWheelDivertersInNonRunningStateAsync(SystemState currentState, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("🔄 非Running状态（{CurrentState}）：尝试重新连接摆轮...", currentState);

            var connectResult = await _wheelDiverterService.ConnectAllAsync(cancellationToken);

            if (connectResult.IsSuccess)
            {
                if (connectResult.ConnectedCount > 0)
                {
                    _logger.LogInformation(
                        "✅ 非Running状态（{CurrentState}）：摆轮重新连接成功 {ConnectedCount}/{TotalCount}",
                        currentState,
                        connectResult.ConnectedCount,
                        connectResult.TotalCount);
                }
                else
                {
                    _logger.LogDebug("非Running状态（{CurrentState}）：无摆轮需要连接", currentState);
                }
            }
            else
            {
                _logger.LogDebug(
                    "⚠️ 非Running状态（{CurrentState}）：摆轮重新连接部分成功 成功={ConnectedCount}/{TotalCount}, 失败={FailedCount}",
                    currentState,
                    connectResult.ConnectedCount,
                    connectResult.TotalCount,
                    connectResult.FailedDriverIds.Count);

                if (connectResult.FailedDriverIds.Any())
                {
                    _logger.LogDebug(
                        "连接失败的摆轮ID: {FailedIds}",
                        string.Join(", ", connectResult.FailedDriverIds));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(
                ex,
                "非Running状态（{CurrentState}）：重新连接摆轮时发生异常（将在下次轮询时重试）",
                currentState);
        }
    }

}
