using Microsoft.Extensions.Hosting;
using ZakYip.WheelDiverterSorter.Core.Sorting.Policies;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Sorting.Policies;
using ZakYip.WheelDiverterSorter.Communication.Abstractions;
using ZakYip.WheelDiverterSorter.Core.Sorting.Policies;
using ZakYip.WheelDiverterSorter.Core.Enums.Communication;
using ZakYip.WheelDiverterSorter.Core.Sorting.Policies;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Core.Sorting.Policies;
using ZakYip.WheelDiverterSorter.Observability.Utilities;
using ZakYip.WheelDiverterSorter.Core.Sorting.Policies;

namespace ZakYip.WheelDiverterSorter.Communication.Infrastructure;

/// <summary>
/// 上游服务器后台服务 - 管理服务器生命周期和配置热重启
/// Upstream server background service - manages server lifecycle and hot configuration restart
/// </summary>
/// <remarks>
/// 负责在程序启动时自动启动 IRuleEngineServer（Server模式下）
/// Responsible for auto-starting IRuleEngineServer on program startup (in Server mode)
/// 
/// 支持配置热重启流程：
/// 1. 接收配置更新
/// 2. 调用 StopAsync() 停止当前服务器
/// 3. 使用新配置创建新的服务器实例
/// 4. 调用 StartAsync() 启动新服务器
/// </remarks>
public sealed class UpstreamServerBackgroundService : BackgroundService
{
    private readonly ILogger<UpstreamServerBackgroundService> _logger;
    private readonly ISystemClock _systemClock;
    private readonly ISafeExecutionService _safeExecutor;
    private readonly UpstreamConnectionOptions _initialOptions;
    private readonly RuleEngineServerFactory _serverFactory;

    private IRuleEngineServer? _currentServer;
    private UpstreamConnectionOptions _currentOptions;
    private readonly SemaphoreSlim _serverLock = new(1, 1);

    public UpstreamServerBackgroundService(
        ILogger<UpstreamServerBackgroundService> logger,
        ISystemClock systemClock,
        ISafeExecutionService safeExecutor,
        UpstreamConnectionOptions initialOptions,
        RuleEngineServerFactory serverFactory)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _systemClock = systemClock ?? throw new ArgumentNullException(nameof(systemClock));
        _safeExecutor = safeExecutor ?? throw new ArgumentNullException(nameof(safeExecutor));
        _initialOptions = initialOptions ?? throw new ArgumentNullException(nameof(initialOptions));
        _serverFactory = serverFactory ?? throw new ArgumentNullException(nameof(serverFactory));
        _currentOptions = initialOptions;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 只在Server模式下启动服务器
        if (_currentOptions.ConnectionMode != ConnectionMode.Server)
        {
            _logger.LogInformation(
                "[{LocalTime}] Client mode detected, server will not start",
                _systemClock.LocalNow);
            return;
        }

        _logger.LogInformation(
            "[{LocalTime}] Upstream server background service starting in Server mode",
            _systemClock.LocalNow);

        await _safeExecutor.ExecuteAsync(async () =>
        {
            try
            {
                // 创建并启动服务器
                await StartServerAsync(stoppingToken);

                _logger.LogInformation(
                    "[{LocalTime}] Upstream server started successfully",
                    _systemClock.LocalNow);

                // 等待取消信号
                var tcs = new TaskCompletionSource<bool>();
                await using (stoppingToken.Register(() => tcs.TrySetResult(true)))
                {
                    await tcs.Task;
                }
            }
            catch (OperationCanceledException)
            {
                // 预期的取消
                _logger.LogInformation(
                    "[{LocalTime}] Upstream server background service is stopping",
                    _systemClock.LocalNow);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "[{LocalTime}] Upstream server background service encountered an error",
                    _systemClock.LocalNow);
                throw;
            }
        }, "UpstreamServerBackgroundService", stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "[{LocalTime}] Stopping upstream server background service",
            _systemClock.LocalNow);

        await _serverLock.WaitAsync(cancellationToken);
        try
        {
            if (_currentServer != null)
            {
                await _currentServer.StopAsync(cancellationToken);
                _currentServer.Dispose();
                _currentServer = null;
            }
        }
        finally
        {
            _serverLock.Release();
        }

        await base.StopAsync(cancellationToken);

        _logger.LogInformation(
            "[{LocalTime}] Upstream server background service stopped",
            _systemClock.LocalNow);
    }

    /// <summary>
    /// 更新服务器配置并执行热重启
    /// Update server configuration and perform hot restart
    /// </summary>
    /// <param name="newOptions">新的连接配置 / New connection options</param>
    public async Task UpdateServerConfigurationAsync(UpstreamConnectionOptions newOptions)
    {
        if (newOptions == null)
        {
            throw new ArgumentNullException(nameof(newOptions));
        }

        await _serverLock.WaitAsync();
        try
        {
            _logger.LogInformation(
                "[{LocalTime}] Updating server configuration. Mode={Mode}, ConnectionMode={ConnectionMode}",
                _systemClock.LocalNow,
                newOptions.Mode,
                newOptions.ConnectionMode);

            // 停止当前服务器
            if (_currentServer != null)
            {
                _logger.LogInformation(
                    "[{LocalTime}] Stopping current server for configuration update",
                    _systemClock.LocalNow);

                await _currentServer.StopAsync();
                _currentServer.Dispose();
                _currentServer = null;
            }

            // 更新配置
            _currentOptions = newOptions;

            // 如果是Server模式，启动新服务器
            if (newOptions.ConnectionMode == ConnectionMode.Server)
            {
                _logger.LogInformation(
                    "[{LocalTime}] Starting new server with updated configuration",
                    _systemClock.LocalNow);

                await StartServerAsync(CancellationToken.None);

                _logger.LogInformation(
                    "[{LocalTime}] Server restarted successfully with new configuration",
                    _systemClock.LocalNow);
            }
            else
            {
                _logger.LogInformation(
                    "[{LocalTime}] Client mode configured, server will not start",
                    _systemClock.LocalNow);
            }
        }
        finally
        {
            _serverLock.Release();
        }
    }

    private async Task StartServerAsync(CancellationToken cancellationToken)
    {
        // 根据通信模式创建相应的服务器实例
        // Create server instance based on communication mode
        _currentServer = _serverFactory.CreateServer(_currentOptions);

        if (_currentServer != null)
        {
            await _currentServer.StartAsync(cancellationToken);
        }
    }

    public override void Dispose()
    {
        _currentServer?.Dispose();
        _serverLock.Dispose();
        base.Dispose();
    }
}
