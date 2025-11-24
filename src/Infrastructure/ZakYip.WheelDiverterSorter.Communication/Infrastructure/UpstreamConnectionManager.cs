using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Observability.Utilities;
using ZakYip.WheelDiverterSorter.Communication.Abstractions;
using ZakYip.WheelDiverterSorter.Communication.Configuration;

namespace ZakYip.WheelDiverterSorter.Communication.Infrastructure;

/// <summary>
/// 上游连接管理器实现
/// Upstream connection manager implementation
/// </summary>
/// <remarks>
/// 实现客户端模式的无限重试逻辑，包括指数退避策略（最大2秒）
/// Implements client mode infinite retry logic with exponential backoff strategy (max 2 seconds)
/// </remarks>
public sealed class UpstreamConnectionManager : IUpstreamConnectionManager, IDisposable
{
    private const int HardMaxBackoffMs = 2000; // 硬编码上限 2 秒 / Hard-coded max 2 seconds

    private readonly ILogger<UpstreamConnectionManager> _logger;
    private readonly ISystemClock _systemClock;
    private readonly ILogDeduplicator _logDeduplicator;
    private readonly ISafeExecutionService _safeExecutor;
    private readonly IRuleEngineClient _client;

    private RuleEngineConnectionOptions _currentOptions;
    private Task? _connectionTask;
    private CancellationTokenSource? _cts;
    private readonly SemaphoreSlim _optionsLock = new(1, 1);
    private bool _isConnected;
    private bool _disposed;

    public UpstreamConnectionManager(
        ILogger<UpstreamConnectionManager> logger,
        ISystemClock systemClock,
        ILogDeduplicator logDeduplicator,
        ISafeExecutionService safeExecutor,
        IRuleEngineClient client,
        RuleEngineConnectionOptions initialOptions)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _systemClock = systemClock ?? throw new ArgumentNullException(nameof(systemClock));
        _logDeduplicator = logDeduplicator ?? throw new ArgumentNullException(nameof(logDeduplicator));
        _safeExecutor = safeExecutor ?? throw new ArgumentNullException(nameof(safeExecutor));
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _currentOptions = initialOptions ?? throw new ArgumentNullException(nameof(initialOptions));
    }

    public bool IsConnected => _isConnected;

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

    public async Task UpdateConnectionOptionsAsync(RuleEngineConnectionOptions options)
    {
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        await _optionsLock.WaitAsync().ConfigureAwait(false);
        try
        {
            _currentOptions = options;
            _logger.LogInformation(
                "[{LocalTime}] Connection options updated. Mode={Mode}, ConnectionMode={ConnectionMode}, Server={Server}",
                _systemClock.LocalNow,
                options.Mode,
                options.ConnectionMode,
                GetServerAddress(options));

            // 如果当前在运行，触发重新连接
            // If currently running, trigger reconnection
            if (_connectionTask != null && !_connectionTask.IsCompleted)
            {
                // 连接循环会自动检测到新的配置并使用新参数
                // Connection loop will automatically detect new config and use new parameters
                _logger.LogInformation(
                    "[{LocalTime}] Active connection will switch to new parameters in next retry cycle",
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
                RuleEngineConnectionOptions options;
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

                    if (!_isConnected)
                    {
                        SetConnectionState(true, null);
                    }

                    // 连接成功，重置退避时间
                    // Connection successful, reset backoff
                    currentBackoffMs = options.InitialBackoffMs;

                    // 保持连接，直到断开或取消
                    // Maintain connection until disconnected or cancelled
                    while (!cancellationToken.IsCancellationRequested && _isConnected)
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

                    if (_isConnected)
                    {
                        SetConnectionState(false, ex.Message);
                    }

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

    private async Task ConnectAsync(RuleEngineConnectionOptions options, CancellationToken cancellationToken)
    {
        // 实际调用客户端的连接方法
        // Actually call the client's connect method
        var connected = await _client.ConnectAsync(cancellationToken).ConfigureAwait(false);
        
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
        _isConnected = isConnected;

        ConnectionStateChanged?.Invoke(this, new ConnectionStateChangedEventArgs
        {
            IsConnected = isConnected,
            ChangedAt = _systemClock.LocalNow,
            ErrorMessage = errorMessage
        });

        var status = isConnected ? "connected" : "disconnected";
        _logger.LogInformation(
            "[{LocalTime}] Connection state changed to: {Status}",
            _systemClock.LocalNow,
            status);
    }

    private static string GetServerAddress(RuleEngineConnectionOptions options)
    {
        return options.Mode switch
        {
            CommunicationMode.Tcp => options.TcpServer ?? "unknown",
            CommunicationMode.SignalR => options.SignalRHub ?? "unknown",
            CommunicationMode.Mqtt => options.MqttBroker ?? "unknown",
            CommunicationMode.Http => options.HttpApi ?? "unknown",
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
