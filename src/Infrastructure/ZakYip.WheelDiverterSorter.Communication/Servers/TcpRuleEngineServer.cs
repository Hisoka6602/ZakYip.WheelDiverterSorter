using ZakYip.WheelDiverterSorter.Core.Events.Chute;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Communication.Abstractions;
using ZakYip.WheelDiverterSorter.Communication.Configuration;
using ZakYip.WheelDiverterSorter.Communication.Models;
using ZakYip.WheelDiverterSorter.Core.Utilities;

namespace ZakYip.WheelDiverterSorter.Communication.Servers;

/// <summary>
/// TCP服务器实现 - 使用TcpListener接受客户端连接
/// TCP Server implementation - using TcpListener to accept client connections
/// </summary>
public sealed class TcpRuleEngineServer : IRuleEngineServer
{
    private readonly ILogger<TcpRuleEngineServer> _logger;
    private readonly RuleEngineConnectionOptions _options;
    private readonly ISystemClock _systemClock;
    private readonly IRuleEngineHandler? _handler;
    
    private readonly ConcurrentDictionary<string, ConnectedClient> _clients = new();
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _acceptTask;
    private bool _isRunning;
    private bool _disposed;

    public TcpRuleEngineServer(
        ILogger<TcpRuleEngineServer> logger,
        RuleEngineConnectionOptions options,
        ISystemClock systemClock,
        IRuleEngineHandler? handler = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _systemClock = systemClock ?? throw new ArgumentNullException(nameof(systemClock));
        _handler = handler;

        ValidateTcpServerOptions(options);
    }

    public bool IsRunning => _isRunning;
    public int ConnectedClientsCount => _clients.Count;

    public event EventHandler<ClientConnectionEventArgs>? ClientConnected;
    public event EventHandler<ClientConnectionEventArgs>? ClientDisconnected;
    public event EventHandler<ParcelNotificationReceivedEventArgs>? ParcelNotificationReceived;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_isRunning)
        {
            _logger.LogWarning("TCP服务器已在运行");
            return Task.CompletedTask;
        }

        var parts = _options.TcpServer!.Split(':');
        var port = int.Parse(parts[1]);
        var host = parts[0];

        _listener = new TcpListener(IPAddress.Parse(host == "localhost" ? "127.0.0.1" : host), port);
        _listener.Start(100); // 默认最大等待连接数

        _cts = new CancellationTokenSource();
        _acceptTask = Task.Run(() => AcceptClientsLoopAsync(_cts.Token), _cts.Token);
        _isRunning = true;

        _logger.LogInformation(
            "[{LocalTime}] TCP服务器已启动，监听 {Host}:{Port}",
            _systemClock.LocalNow,
            host,
            port);

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (!_isRunning)
        {
            return;
        }

        _isRunning = false;
        _cts?.Cancel();
        _listener?.Stop();

        // 断开所有客户端
        foreach (var client in _clients.Values)
        {
            await DisconnectClientAsync(client);
        }
        _clients.Clear();

        if (_acceptTask != null)
        {
            try
            {
                await _acceptTask;
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        }

        _logger.LogInformation(
            "[{LocalTime}] TCP服务器已停止",
            _systemClock.LocalNow);
    }

    public async Task BroadcastChuteAssignmentAsync(
        long parcelId,
        string chuteId,
        CancellationToken cancellationToken = default)
    {
        // PR-UPSTREAM02: 使用 ChuteAssignmentNotification 代替 ChuteAssignmentResponse
        var notification = new ChuteAssignmentNotification
        {
            ParcelId = parcelId,
            ChuteId = int.Parse(chuteId),
            AssignedAt = _systemClock.LocalNowOffset
        };

        var json = JsonSerializer.Serialize(notification);
        var bytes = Encoding.UTF8.GetBytes(json + "\n");

        var disconnectedClients = new List<string>();

        foreach (var kvp in _clients)
        {
            try
            {
                await kvp.Value.Stream.WriteAsync(bytes, cancellationToken);
                await kvp.Value.Stream.FlushAsync(cancellationToken);

                _logger.LogDebug(
                    "[{LocalTime}] 已向客户端 {ClientId} 广播包裹 {ParcelId} 的格口分配: {ChuteId}",
                    _systemClock.LocalNow,
                    kvp.Key,
                    parcelId,
                    chuteId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "[{LocalTime}] 向客户端 {ClientId} 广播消息失败: {Message}",
                    _systemClock.LocalNow,
                    kvp.Key,
                    ex.Message);
                disconnectedClients.Add(kvp.Key);
            }
        }

        // 清理断开的客户端
        foreach (var clientId in disconnectedClients)
        {
            if (_clients.TryRemove(clientId, out var client))
            {
                await DisconnectClientAsync(client);
            }
        }
    }

    private async Task AcceptClientsLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _isRunning)
        {
            try
            {
                var tcpClient = await _listener!.AcceptTcpClientAsync(cancellationToken);
                var clientId = Guid.NewGuid().ToString("N");
                
                var client = new ConnectedClient
                {
                    ClientId = clientId,
                    TcpClient = tcpClient,
                    Stream = tcpClient.GetStream(),
                    ConnectedAt = _systemClock.LocalNowOffset,
                    ClientAddress = tcpClient.Client.RemoteEndPoint?.ToString()
                };

                _clients[clientId] = client;

                _logger.LogInformation(
                    "[{LocalTime}] 客户端已连接: {ClientId} from {Address}",
                    _systemClock.LocalNow,
                    clientId,
                    client.ClientAddress);

                // 触发客户端连接事件
                ClientConnected?.Invoke(this, new ClientConnectionEventArgs
                {
                    ClientId = clientId,
                    ConnectedAt = client.ConnectedAt,
                    ClientAddress = client.ClientAddress
                });

                // 为每个客户端启动消息处理循环
                _ = Task.Run(() => HandleClientMessagesAsync(client, cancellationToken), cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Expected
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "[{LocalTime}] 接受客户端连接时发生错误: {Message}",
                    _systemClock.LocalNow,
                    ex.Message);
            }
        }
    }

    private async Task HandleClientMessagesAsync(ConnectedClient client, CancellationToken cancellationToken)
    {
        var buffer = new byte[_options.Tcp.ReceiveBufferSize];

        try
        {
            while (!cancellationToken.IsCancellationRequested && client.TcpClient.Connected)
            {
                var bytesRead = await client.Stream.ReadAsync(buffer, cancellationToken);
                
                if (bytesRead == 0)
                {
                    // 客户端关闭连接
                    break;
                }

                var json = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();
                
                try
                {
                    // PR-UPSTREAM02: 使用 ParcelDetectionNotification 代替 ChuteAssignmentRequest
                    var notification = JsonSerializer.Deserialize<ParcelDetectionNotification>(json);
                    if (notification != null)
                    {
                        _logger.LogInformation(
                            "[{LocalTime}] 收到客户端 {ClientId} 的包裹检测通知: ParcelId={ParcelId}",
                            _systemClock.LocalNow,
                            client.ClientId,
                            notification.ParcelId);

                        // 触发包裹通知接收事件
                        ParcelNotificationReceived?.Invoke(this, new ParcelNotificationReceivedEventArgs
                        {
                            ParcelId = notification.ParcelId,
                            ClientId = client.ClientId,
                            ReceivedAt = _systemClock.LocalNowOffset
                        });

                        // 如果有处理器，调用处理器
                        if (_handler != null)
                        {
                            var eventArgs = new ChuteAssignmentNotificationEventArgs
                            {
                                ParcelId = notification.ParcelId,
                                ChuteId = 0, // Server模式下由RuleEngine决定
                                AssignedAt = notification.DetectionTime
                            };
                            await _handler.HandleChuteAssignmentAsync(eventArgs);
                        }
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(
                        ex,
                        "[{LocalTime}] 解析客户端 {ClientId} 的消息失败: {Json}",
                        _systemClock.LocalNow,
                        client.ClientId,
                        json);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Expected
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "[{LocalTime}] 处理客户端 {ClientId} 消息时发生错误: {Message}",
                _systemClock.LocalNow,
                client.ClientId,
                ex.Message);
        }
        finally
        {
            // 客户端断开
            if (_clients.TryRemove(client.ClientId, out _))
            {
                await DisconnectClientAsync(client);
            }
        }
    }

    private async Task DisconnectClientAsync(ConnectedClient client)
    {
        try
        {
            client.Stream.Close();
            client.TcpClient.Close();

            _logger.LogInformation(
                "[{LocalTime}] 客户端已断开: {ClientId}",
                _systemClock.LocalNow,
                client.ClientId);

            // 触发客户端断开事件
            ClientDisconnected?.Invoke(this, new ClientConnectionEventArgs
            {
                ClientId = client.ClientId,
                ConnectedAt = client.ConnectedAt,
                ClientAddress = client.ClientAddress
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "[{LocalTime}] 断开客户端 {ClientId} 时发生错误: {Message}",
                _systemClock.LocalNow,
                client.ClientId,
                ex.Message);
        }

        await Task.CompletedTask;
    }

    private static void ValidateTcpServerOptions(RuleEngineConnectionOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.TcpServer))
        {
            throw new ArgumentException("TCP服务器地址不能为空", nameof(options));
        }

        var parts = options.TcpServer.Split(':');
        if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
        {
            throw new ArgumentException($"无效的TCP服务器地址格式: {options.TcpServer}", nameof(options));
        }

        if (!int.TryParse(parts[1], out var port) || port <= 0 || port > 65535)
        {
            throw new ArgumentException($"无效的端口号: {parts[1]}", nameof(options));
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        StopAsync().GetAwaiter().GetResult();
        _cts?.Dispose();
        _listener?.Stop();
        _disposed = true;
    }

    private sealed class ConnectedClient
    {
        public required string ClientId { get; init; }
        public required TcpClient TcpClient { get; init; }
        public required NetworkStream Stream { get; init; }
        public required DateTimeOffset ConnectedAt { get; init; }
        public required string? ClientAddress { get; init; }
    }
}
