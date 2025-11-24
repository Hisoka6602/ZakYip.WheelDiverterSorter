using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Communication.Abstractions;
using ZakYip.WheelDiverterSorter.Core.Utilities;

namespace ZakYip.WheelDiverterSorter.Communication.Infrastructure;

/// <summary>
/// 上游连接后台服务
/// Upstream connection background service
/// </summary>
/// <remarks>
/// 负责在程序启动时自动启动 UpstreamConnectionManager（Client模式下）
/// Responsible for auto-starting UpstreamConnectionManager on program startup (in Client mode)
/// </remarks>
public sealed class UpstreamConnectionBackgroundService : BackgroundService
{
    private readonly ILogger<UpstreamConnectionBackgroundService> _logger;
    private readonly IUpstreamConnectionManager _connectionManager;
    private readonly ISystemClock _systemClock;

    public UpstreamConnectionBackgroundService(
        ILogger<UpstreamConnectionBackgroundService> logger,
        IUpstreamConnectionManager connectionManager,
        ISystemClock systemClock)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));
        _systemClock = systemClock ?? throw new ArgumentNullException(nameof(systemClock));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "[{LocalTime}] Upstream connection background service starting",
            _systemClock.LocalNow);

        try
        {
            // 启动连接管理器（如果是Client模式，会开始无限重试连接）
            // Start connection manager (if Client mode, will begin infinite retry loop)
            await _connectionManager.StartAsync(stoppingToken);

            _logger.LogInformation(
                "[{LocalTime}] Upstream connection manager started successfully",
                _systemClock.LocalNow);

            // 等待取消信号 - 使用 TaskCompletionSource 避免阻塞线程
            // Wait for cancellation signal - using TaskCompletionSource to avoid blocking thread
            var tcs = new TaskCompletionSource<bool>();
            await using (stoppingToken.Register(() => tcs.TrySetResult(true)))
            {
                await tcs.Task;
            }
        }
        catch (OperationCanceledException)
        {
            // 预期的取消
            // Expected cancellation
            _logger.LogInformation(
                "[{LocalTime}] Upstream connection background service is stopping",
                _systemClock.LocalNow);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[{LocalTime}] Upstream connection background service encountered an error",
                _systemClock.LocalNow);
            throw;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "[{LocalTime}] Stopping upstream connection background service",
            _systemClock.LocalNow);

        // 停止连接管理器
        // Stop connection manager
        await _connectionManager.StopAsync(cancellationToken);

        await base.StopAsync(cancellationToken);

        _logger.LogInformation(
            "[{LocalTime}] Upstream connection background service stopped",
            _systemClock.LocalNow);
    }
}
