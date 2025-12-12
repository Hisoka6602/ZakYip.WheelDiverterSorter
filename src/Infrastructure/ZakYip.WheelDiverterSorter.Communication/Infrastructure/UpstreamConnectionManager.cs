using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Observability.Utilities;
using ZakYip.WheelDiverterSorter.Communication.Abstractions;
using ZakYip.WheelDiverterSorter.Communication.Configuration;
using ZakYip.WheelDiverterSorter.Core.Enums.Communication;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Upstream;
using ZakYip.WheelDiverterSorter.Core.Sorting.Policies;

namespace ZakYip.WheelDiverterSorter.Communication.Infrastructure;

/// <summary>
/// 上游连接管理器实现
/// Upstream connection manager implementation
/// </summary>
/// <remarks>
/// 实现客户端模式的无限重试逻辑，包括指数退避策略（最大2秒）
/// Implements client mode infinite retry logic with exponential backoff strategy (max 2 seconds)
/// PR-U1: 直接使用 IUpstreamRoutingClient 替代 IRuleEngineClient
/// PR-HOTRELOAD: 使用工厂模式支持配置热更新时重新创建客户端
/// </remarks>
public sealed class UpstreamConnectionManager : IUpstreamConnectionManager, IDisposable
{
    private const int HardMaxBackoffMs = 2000; // 硬编码上限 2 秒 / Hard-coded max 2 seconds

    private readonly ILogger<UpstreamConnectionManager> _logger;
    private readonly ISystemClock _systemClock;
    private readonly ILogDeduplicator _logDeduplicator;
    private readonly ISafeExecutionService _safeExecutor;
    private readonly IUpstreamRoutingClientFactory _clientFactory;

    private UpstreamConnectionOptions _currentOptions;
    private IUpstreamRoutingClient? _client;
    private Task? _connectionTask;
    private CancellationTokenSource? _cts;
    private readonly SemaphoreSlim _optionsLock = new(1, 1);
    private bool _disposed;

    public UpstreamConnectionManager(
        ILogger<UpstreamConnectionManager> logger,
        ISystemClock systemClock,
        ILogDeduplicator logDeduplicator,
        ISafeExecutionService safeExecutor,
        IUpstreamRoutingClientFactory clientFactory,
        UpstreamConnectionOptions initialOptions)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _systemClock = systemClock ?? throw new ArgumentNullException(nameof(systemClock));
        _logDeduplicator = logDeduplicator ?? throw new ArgumentNullException(nameof(logDeduplicator));
        _safeExecutor = safeExecutor ?? throw new ArgumentNullException(nameof(safeExecutor));
        _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
        _currentOptions = initialOptions ?? throw new ArgumentNullException(nameof(initialOptions));
    }

    public bool IsConnected => _client?.IsConnected ?? false;

    public event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_currentOptions.ConnectionMode != ConnectionMode.Client)
        {
            _logger.LogInformation(
                "[{LocalTime}] Server mode detected, connection manager will not start reconnection loop",
                _systemClock.LocalNow);
            return Task.CompletedTask;
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _connectionTask = Task.Run(() => ConnectionLoopAsync(_cts.Token), _cts.Token);

        _logger.LogInformation(
            "[{LocalTime}] Upstream connection manager started in client mode",
            _systemClock.LocalNow);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_cts != null)
        {
            _cts.Cancel();

            if (_connectionTask != null)
            {
                try
                {
                    await _connectionTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Expected
                }
            }
        }

        _logger.LogInformation(
            "[{LocalTime}] Upstream connection manager stopped",
            _systemClock.LocalNow);
    }

    public async Task UpdateConnectionOptionsAsync(UpstreamConnectionOptions options)
    {
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        await _optionsLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var oldOptions = _currentOptions;
            _currentOptions = options;
            
            _logger.LogInformation(
                "[{LocalTime}] 🔄 连接配置已更新 - Connection options updated. " +
                "Old: Mode={OldMode}, Server={OldServer} → " +
                "New: Mode={NewMode}, Server={NewServer}",
                _systemClock.LocalNow,
                oldOptions.Mode,
                GetServerAddress(oldOptions),
                options.Mode,
                GetServerAddress(options));

            // 🔴 关键修复：断开旧客户端，创建新客户端，使用新配置重新连接
            // Critical fix: disconnect old client, create new client with new configuration
            if (_connectionTask != null && !_connectionTask.IsCompleted)
            {
                try
                {
                    _logger.LogInformation(
                        "[{LocalTime}] 🔌 断开当前连接以应用新配置 - Disconnecting current connection to apply new configuration",
                        _systemClock.LocalNow);
                    
                    // 断开当前连接
                    if (_client != null)
                    {
                        // 连接由Client自动管理
                        // await _client.DisconnectAsync().ConfigureAwait(false);
                        
                        // 如果客户端实现了 IDisposable，释放资源
                        if (_client is IDisposable disposableClient)
                        {
                            disposableClient.Dispose();
                        }
                    }
                    
                    // PR-HOTRELOAD: 使用工厂创建新客户端实例，确保使用最新配置
                    // Create new client instance with updated configuration
                    _client = _clientFactory.CreateClient();
                    
                    _logger.LogInformation(
                        "[{LocalTime}] ✅ 已创建新客户端实例，将立即使用新配置重新连接 - " +
                        "New client instance created, will reconnect immediately with new configuration",
                        _systemClock.LocalNow);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "[{LocalTime}] ⚠️ 断开连接或创建新客户端时发生异常（将继续尝试使用新配置重连） - " +
                        "Exception while disconnecting or creating new client (will continue to reconnect with new config)",
                        _systemClock.LocalNow);
                }
            }
            else
            {
                // 如果没有活动连接，直接创建新客户端
                // If no active connection, create new client directly
                _client = _clientFactory.CreateClient();
                
                _logger.LogInformation(
                    "[{LocalTime}] ℹ️ 当前无活动连接，已创建新客户端实例，新配置将在下次连接时生效 - " +
                    "No active connection, new client instance created, new configuration will take effect on next connection",
                    _systemClock.LocalNow);
            }
        }
        finally
        {
            _optionsLock.Release();
        }
    }

    private async Task ConnectionLoopAsync(CancellationToken cancellationToken)
    {
        var currentBackoffMs = _currentOptions.InitialBackoffMs;

        while (!cancellationToken.IsCancellationRequested)
        {
            await _safeExecutor.ExecuteAsync(async () =>
            {
                UpstreamConnectionOptions options;
                await _optionsLock.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    options = _currentOptions;
                    // 重置退避时间（使用最新配置）
                    // Reset backoff time (use latest config)
                    currentBackoffMs = options.InitialBackoffMs;
                }
                finally
                {
                    _optionsLock.Release();
                }

                try
                {
                    // 尝试连接
                    // Attempt to connect
                    await ConnectAsync(options, cancellationToken).ConfigureAwait(false);

                    // 通知连接状态变化
                    // Notify connection state change
                    SetConnectionState(true, null);

                    // 连接成功，重置退避时间
                    // Connection successful, reset backoff
                    currentBackoffMs = options.InitialBackoffMs;

                    // 保持连接，直到断开或取消
                    // Maintain connection until disconnected or cancelled
                    while (!cancellationToken.IsCancellationRequested && _client?.IsConnected == true)
                    {
                        await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw; // 允许取消传播 / Allow cancellation to propagate
                }
                catch (Exception ex)
                {
                    // 连接失败，记录日志（使用去重避免刷屏）
                    // Connection failed, log error (use deduplication to avoid log spam)
                    var logKey = $"ConnectionFailure_{options.Mode}_{GetServerAddress(options)}";
                    if (_logDeduplicator.ShouldLog(LogLevel.Warning, logKey, ex.GetType().Name))
                    {
                        _logger.LogWarning(
                            ex,
                            "[{LocalTime}] Connection to upstream failed: {Message}. Will retry in {BackoffMs}ms",
                            _systemClock.LocalNow,
                            ex.Message,
                            currentBackoffMs);
                        _logDeduplicator.RecordLog(LogLevel.Warning, logKey, ex.GetType().Name);
                    }

                    // 通知连接状态变化（如果之前是连接状态）
                    // Notify connection state change (if previously connected)
                    SetConnectionState(false, ex.Message);

                    // 应用退避策略
                    // Apply backoff strategy
                    await Task.Delay(currentBackoffMs, cancellationToken).ConfigureAwait(false);

                    // 指数增长，但限制在硬编码的最大值
                    // Exponential growth, but cap at hard-coded max
                    currentBackoffMs = Math.Min(currentBackoffMs * 2, Math.Min(options.MaxBackoffMs, HardMaxBackoffMs));
                }
            }, "UpstreamConnectionLoop", cancellationToken).ConfigureAwait(false);

            // 如果未启用无限重试，则退出循环
            // If infinite retry is not enabled, exit loop
            if (!_currentOptions.EnableInfiniteRetry)
            {
                _logger.LogInformation(
                    "[{LocalTime}] Infinite retry is disabled, stopping connection loop",
                    _systemClock.LocalNow);
                break;
            }
        }
    }

    private async Task ConnectAsync(UpstreamConnectionOptions options, CancellationToken cancellationToken)
    {
        // PR-HOTRELOAD: 确保客户端实例已创建
        // Ensure client instance is created
        if (_client == null)
        {
            await _optionsLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (_client == null)
                {
                    _client = _clientFactory.CreateClient();
                }
            }
            finally
            {
                _optionsLock.Release();
            }
        }
        
        // 实际调用客户端的连接方法
        // Actually call the client's connect method
        var connected = // 连接测试改用PingAsync
        var connected = await _client.PingAsync(cancellationToken).ConfigureAwait(false);
        
        if (!connected)
        {
            throw new InvalidOperationException(
                $"Failed to connect to RuleEngine using {options.Mode} mode at {GetServerAddress(options)}");
        }
        
        _logger.LogInformation(
            "[{LocalTime}] Successfully connected to RuleEngine using {Mode} mode at {Server}",
            _systemClock.LocalNow,
            options.Mode,
            GetServerAddress(options));
    }

    private void SetConnectionState(bool isConnected, string? errorMessage)
    {
        ConnectionStateChanged.SafeInvoke(this, new ConnectionStateChangedEventArgs
        {
            IsConnected = isConnected,
            ChangedAt = _systemClock.LocalNowOffset,
            ErrorMessage = errorMessage
        }, _logger, nameof(ConnectionStateChanged));

        var status = isConnected ? "connected" : "disconnected";
        _logger.LogInformation(
            "[{LocalTime}] Connection state changed to: {Status}",
            _systemClock.LocalNow,
            status);
    }

    /// <summary>
    /// 获取服务器地址（用于日志）
    /// </summary>
    /// <remarks>
    /// PR-UPSTREAM01: 移除 HTTP 地址获取。
    /// </remarks>
    private static string GetServerAddress(UpstreamConnectionOptions options)
    {
        return options.Mode switch
        {
            CommunicationMode.Tcp => options.TcpServer ?? "unknown",
            CommunicationMode.SignalR => options.SignalRHub ?? "unknown",
            CommunicationMode.Mqtt => options.MqttBroker ?? "unknown",
            _ => "unknown"
        };
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _cts?.Cancel();
        _cts?.Dispose();
        _optionsLock.Dispose();
        _disposed = true;
    }
}
