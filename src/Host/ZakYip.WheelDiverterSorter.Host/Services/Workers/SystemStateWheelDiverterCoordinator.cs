using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Application.Services.WheelDiverter;
using ZakYip.WheelDiverterSorter.Core.Enums.System;
using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using ZakYip.WheelDiverterSorter.Observability.Utilities;

namespace ZakYip.WheelDiverterSorter.Host.Services.Workers;

/// <summary>
/// 系统状态与摆轮协调后台服务
/// </summary>
/// <remarks>
/// 监控系统状态转换，当系统进入 Running 状态时自动将所有摆轮设置为直行（PassThrough）。
/// 
/// <para><b>设计目的</b>：</para>
/// <list type="bullet">
///   <item>确保系统启动时摆轮处于安全的直行状态</item>
///   <item>支持不通过 IO 联动控制摆轮的厂商（如某些厂商仅支持 Modbus/TCP 控制）</item>
///   <item>在 Ready→Running 和 Paused→Running 状态转换时都会触发</item>
/// </list>
/// 
/// <para><b>触发场景</b>：</para>
/// <list type="bullet">
///   <item>面板启动按钮按下（Ready → Running）</item>
///   <item>API 调用启动系统（Ready → Running）</item>
///   <item>系统从暂停恢复运行（Paused → Running）</item>
/// </list>
/// </remarks>
public sealed class SystemStateWheelDiverterCoordinator : BackgroundService
{
    private readonly ISystemStateManager _stateManager;
    private readonly IWheelDiverterConnectionService _wheelDiverterService;
    private readonly ISafeExecutionService _safeExecutor;
    private readonly ILogger<SystemStateWheelDiverterCoordinator> _logger;

    /// <summary>
    /// 轮询间隔（毫秒）
    /// </summary>
    private const int PollingIntervalMs = 200;

    /// <summary>
    /// 异常恢复延迟（毫秒）
    /// </summary>
    private const int ExceptionRetryDelayMs = 1000;

    /// <summary>
    /// 上次记录的系统状态
    /// </summary>
    private SystemState _lastKnownState = SystemState.Booting;

    public SystemStateWheelDiverterCoordinator(
        ISystemStateManager stateManager,
        IWheelDiverterConnectionService wheelDiverterService,
        ISafeExecutionService safeExecutor,
        ILogger<SystemStateWheelDiverterCoordinator> logger)
    {
        _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
        _wheelDiverterService = wheelDiverterService ?? throw new ArgumentNullException(nameof(wheelDiverterService));
        _safeExecutor = safeExecutor ?? throw new ArgumentNullException(nameof(safeExecutor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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

                            // 当系统进入 Running 状态时，设置所有摆轮为直行
                            if (currentState == SystemState.Running && _lastKnownState != SystemState.Running)
                            {
                                await InitializeWheelDivertersToPassThroughAsync(stoppingToken);
                            }

                            // 更新上次记录的状态
                            _lastKnownState = currentState;
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
    /// 初始化所有摆轮为直行状态
    /// </summary>
    /// <remarks>
    /// 通过调用 PassThroughAllAsync 将所有活动摆轮设置为直行（PassThrough）状态，
    /// 确保系统启动时摆轮处于安全的默认位置。
    /// 
    /// 此操作是异步的，如果部分摆轮设置失败，会记录警告日志但不会阻止系统运行。
    /// </remarks>
    private async Task InitializeWheelDivertersToPassThroughAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("⚙️ 系统进入 Running 状态，正在将所有摆轮设置为直行...");

            var result = await _wheelDiverterService.PassThroughAllAsync(cancellationToken);

            if (result.IsSuccess)
            {
                _logger.LogInformation(
                    "✅ 所有摆轮已成功设置为直行状态: {SuccessCount}/{TotalCount}",
                    result.SuccessCount,
                    result.TotalCount);
            }
            else
            {
                _logger.LogWarning(
                    "⚠️ 部分摆轮设置为直行失败: 成功={SuccessCount}/{TotalCount}, 失败={FailedCount}",
                    result.SuccessCount,
                    result.TotalCount,
                    result.FailedDriverIds.Count);

                if (result.FailedDriverIds.Any())
                {
                    _logger.LogWarning(
                        "失败的摆轮ID: {FailedIds}",
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
                "❌ 设置摆轮为直行状态时发生异常。系统将继续运行，但摆轮可能未处于直行状态。");
        }
    }
}
