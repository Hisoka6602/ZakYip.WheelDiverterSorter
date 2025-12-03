using ZakYip.WheelDiverterSorter.Core.Abstractions.Upstream;
using ZakYip.WheelDiverterSorter.Core.Sorting.Policies;
using ZakYip.WheelDiverterSorter.Core.Events.Chute;
using ZakYip.WheelDiverterSorter.Core.Sorting.Policies;
using System.Collections.Concurrent;
using ZakYip.WheelDiverterSorter.Core.Sorting.Policies;
using System.Text;
using ZakYip.WheelDiverterSorter.Core.Sorting.Policies;
using System.Text.Json;
using ZakYip.WheelDiverterSorter.Core.Sorting.Policies;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Sorting.Policies;
using ZakYip.WheelDiverterSorter.Communication.Abstractions;
using ZakYip.WheelDiverterSorter.Core.Sorting.Policies;
using ZakYip.WheelDiverterSorter.Communication.Models;
using ZakYip.WheelDiverterSorter.Core.Sorting.Policies;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Core.Sorting.Policies;
using TouchSocket.Core;
using ZakYip.WheelDiverterSorter.Core.Sorting.Policies;
using TouchSocket.Sockets;
using ZakYip.WheelDiverterSorter.Core.Sorting.Policies;

namespace ZakYip.WheelDiverterSorter.Communication.Servers;

/// <summary>
/// 基于TouchSocket的TCP服务器实现
/// </summary>
/// <remarks>
/// 使用TouchSocket库实现TCP服务器，提供：
/// - 客户端连接/断开日志记录
/// - 多客户端并发管理
/// - 消息广播到所有连接的客户端
/// </remarks>
public sealed class TouchSocketTcpRuleEngineServer : IRuleEngineServer
{
    private readonly ILogger<TouchSocketTcpRuleEngineServer> _logger;
    private readonly UpstreamConnectionOptions _options;
    private readonly ISystemClock _systemClock;
    private readonly IRuleEngineHandler? _handler;
    
    private readonly ConcurrentDictionary<string, ConnectedClientInfo> _clients = new();
    private TcpService? _service;
    private bool _isRunning;
    private bool _disposed;

    public TouchSocketTcpRuleEngineServer(
        ILogger<TouchSocketTcpRuleEngineServer> logger,
        UpstreamConnectionOptions options,
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

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_isRunning)
        {
            _logger.LogWarning(
                "[{LocalTime}] [服务端模式] TCP服务器已在运行",
                _systemClock.LocalNow);
            return;
        }

        var parts = _options.TcpServer!.Split(':');
        var port = int.Parse(parts[1]);
        var host = parts[0];
        var bindAddress = host == "localhost" || host == "127.0.0.1" ? "127.0.0.1" : host;

        _service = new TcpService();
        
        await _service.SetupAsync(new TouchSocketConfig()
            .SetListenIPHosts(new IPHost[] { new IPHost($"{bindAddress}:{port}") })
            .SetTcpDataHandlingAdapter(() => new TerminatorPackageAdapter("\n")
            {
                CacheTimeout = TimeSpan.FromMilliseconds(_options.TimeoutMs)
            })
            .ConfigurePlugins(a =>
            {
                a.Add<TouchSocketServerPlugin>();
            }));

        // 注册事件
        _service.Connected += OnClientConnected;
        _service.Closed += OnClientDisconnected;
        _service.Received += OnMessageReceived;

        // 启动服务
        await _service.StartAsync();
        _isRunning = true;

        _logger.LogInformation(
            "[{LocalTime}] [服务端模式] TCP服务器已启动，监听 {Host}:{Port}",
            _systemClock.LocalNow,
            bindAddress,
            port);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (!_isRunning)
        {
            return;
        }

        _isRunning = false;

        // 断开所有客户端
        foreach (var kvp in _clients)
        {
            try
            {
                if (_service?.Clients.TryGetClient(kvp.Key, out var socketClient) == true)
                {
                    await socketClient.CloseAsync("Server停止", cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "[{LocalTime}] [服务端模式] 断开客户端 {ClientId} 时发生异常",
                    _systemClock.LocalNow,
                    kvp.Key);
            }
        }
        _clients.Clear();

        // 停止服务
        if (_service != null)
        {
            await _service.StopAsync(cancellationToken);
            _service.Dispose();
            _service = null;
        }

        _logger.LogInformation(
            "[{LocalTime}] [服务端模式] TCP服务器已停止",
            _systemClock.LocalNow);
    }

    private Task OnClientConnected(TcpSessionClient client, ConnectedEventArgs e)
    {
        var clientId = client.Id;
        var clientAddress = client.IP + ":" + client.Port;
        var now = _systemClock.LocalNowOffset;

        var clientInfo = new ConnectedClientInfo
        {
            ClientId = clientId,
            ConnectedAt = now,
            ClientAddress = clientAddress
        };

        _clients[clientId] = clientInfo;

        _logger.LogInformation(
            "[{LocalTime}] [服务端模式-客户端连接] 客户端已连接: {ClientId} from {Address}",
            _systemClock.LocalNow,
            clientId,
            clientAddress);

        // 触发客户端连接事件
        ClientConnected?.Invoke(this, new ClientConnectionEventArgs
        {
            ClientId = clientId,
            ConnectedAt = now,
            ClientAddress = clientAddress
        });

        return Task.CompletedTask;
    }

    private Task OnClientDisconnected(TcpSessionClient client, ClosedEventArgs e)
    {
        var clientId = client.Id;
        
        if (_clients.TryRemove(clientId, out var clientInfo))
        {
            _logger.LogInformation(
                "[{LocalTime}] [服务端模式-客户端断开] 客户端已断开: {ClientId} from {Address} (连接时长: {Duration})",
                _systemClock.LocalNow,
                clientId,
                clientInfo.ClientAddress,
                _systemClock.LocalNowOffset - clientInfo.ConnectedAt);

            // 触发客户端断开事件
            ClientDisconnected?.Invoke(this, new ClientConnectionEventArgs
            {
                ClientId = clientId,
                ConnectedAt = clientInfo.ConnectedAt,
                ClientAddress = clientInfo.ClientAddress
            });
        }

        return Task.CompletedTask;
    }

    private Task OnMessageReceived(TcpSessionClient client, ReceivedDataEventArgs e)
    {
        try
        {
            var json = Encoding.UTF8.GetString(e.ByteBlock.Span).Trim();
            
            _logger.LogInformation(
                "[{LocalTime}] [服务端模式-接收消息] 收到客户端 {ClientId} 的消息 | 消息内容={MessageContent}",
                _systemClock.LocalNow,
                client.Id,
                json);

            // 尝试解析为包裹检测通知
            var notification = JsonSerializer.Deserialize<ParcelDetectionNotification>(json);
            if (notification != null)
            {
                _logger.LogInformation(
                    "[{LocalTime}] [服务端模式-处理通知] 解析到包裹检测通知 | ClientId={ClientId} | ParcelId={ParcelId}",
                    _systemClock.LocalNow,
                    client.Id,
                    notification.ParcelId);

                // 触发包裹通知接收事件
                ParcelNotificationReceived?.Invoke(this, new ParcelNotificationReceivedEventArgs
                {
                    ParcelId = notification.ParcelId,
                    ReceivedAt = _systemClock.LocalNowOffset,
                    ClientId = client.Id
                });

                // 如果有处理器，调用处理器
                if (_handler != null)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var eventArgs = new ChuteAssignmentEventArgs
                            {
                                ParcelId = notification.ParcelId,
                                ChuteId = 0, // 服务器端接收到的是检测通知，没有格口分配信息
                                AssignedAt = notification.DetectionTime
                            };
                            await _handler.HandleChuteAssignmentAsync(eventArgs);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(
                                ex,
                                "[{LocalTime}] [服务端模式-处理错误] 处理包裹 {ParcelId} 时发生错误",
                                _systemClock.LocalNow,
                                notification.ParcelId);
                        }
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[{LocalTime}] [服务端模式-接收错误] 处理客户端 {ClientId} 消息时发生错误",
                _systemClock.LocalNow,
                client.Id);
        }

        return Task.CompletedTask;
    }

    public async Task BroadcastChuteAssignmentAsync(
        long parcelId,
        string chuteId,
        CancellationToken cancellationToken = default)
    {
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
                var socketClient = _service?.GetClient(kvp.Key);
                if (socketClient != null)
                {
                    await socketClient.SendAsync(bytes);

                    _logger.LogDebug(
                        "[{LocalTime}] [服务端模式-广播] 已向客户端 {ClientId} 广播包裹 {ParcelId} 的格口分配: {ChuteId}",
                        _systemClock.LocalNow,
                        kvp.Key,
                        parcelId,
                        chuteId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "[{LocalTime}] [服务端模式-广播失败] 向客户端 {ClientId} 广播消息失败: {Message}",
                    _systemClock.LocalNow,
                    kvp.Key,
                    ex.Message);
                disconnectedClients.Add(kvp.Key);
            }
        }

        // 清理断开的客户端
        foreach (var clientId in disconnectedClients)
        {
            _clients.TryRemove(clientId, out _);
        }
    }

    private static void ValidateTcpServerOptions(UpstreamConnectionOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.TcpServer))
        {
            throw new ArgumentException("TCP服务器地址不能为空", nameof(options));
        }

        var parts = options.TcpServer.Split(':');
        if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[1]))
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

        _disposed = true;
        StopAsync().GetAwaiter().GetResult();
    }
}

/// <summary>
/// TouchSocket服务器插件
/// </summary>
file class TouchSocketServerPlugin : PluginBase, ITcpReceivedPlugin
{
    public Task OnTcpReceived(ITcpSession client, ReceivedDataEventArgs e)
    {
        // 消息已经由 TerminatorPackageAdapter 处理，这里只需要传递
        return e.InvokeNext();
    }
}

/// <summary>
/// 已连接客户端信息
/// </summary>
internal sealed class ConnectedClientInfo
{
    public required string ClientId { get; init; }
    public DateTimeOffset ConnectedAt { get; init; }
    public string? ClientAddress { get; init; }
}
