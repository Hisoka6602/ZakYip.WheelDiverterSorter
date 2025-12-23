using ZakYip.WheelDiverterSorter.Core.Abstractions.Upstream;
using ZakYip.WheelDiverterSorter.Observability.Utilities;
using ZakYip.WheelDiverterSorter.Core.Events.Chute;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Communication.Abstractions;
using ZakYip.WheelDiverterSorter.Communication.Configuration;
using ZakYip.WheelDiverterSorter.Communication.Models;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using TouchSocket.Core;
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
    
#pragma warning disable CS0067 // Event is never used - Legacy event, kept for interface compatibility
    public event EventHandler<ParcelNotificationReceivedEventArgs>? ParcelNotificationReceived;
#pragma warning restore CS0067

    /// <summary>
    /// 格口分配事件（与客户端模式保持一致）
    /// </summary>
    public event EventHandler<Core.Abstractions.Upstream.ChuteAssignmentEventArgs>? ChuteAssigned;

    /// <summary>
    /// 获取所有已连接的客户端信息
    /// </summary>
    public IReadOnlyList<ClientConnectionEventArgs> GetConnectedClients()
    {
        return _clients.Values
            .Select(c => new ClientConnectionEventArgs
            {
                ClientId = c.ClientId,
                ConnectedAt = c.ConnectedAt,
                ClientAddress = c.ClientAddress
            })
            .ToList()
            .AsReadOnly();
    }

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
            // 取消事件订阅（防止内存泄漏）
            _service.Connected -= OnClientConnected;
            _service.Closed -= OnClientDisconnected;
            _service.Received -= OnMessageReceived;
            
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
        ClientConnected.SafeInvoke(this, new ClientConnectionEventArgs
        {
            ClientId = clientId,
            ConnectedAt = now,
            ClientAddress = clientAddress
        }, _logger, nameof(ClientConnected));

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
            ClientDisconnected.SafeInvoke(this, new ClientConnectionEventArgs
            {
                ClientId = clientId,
                ConnectedAt = clientInfo.ConnectedAt,
                ClientAddress = clientInfo.ClientAddress
            }, _logger, nameof(ClientDisconnected));
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

            // 尝试解析为格口分配通知（上游主动推送）
            var notification = JsonSerializer.Deserialize<ChuteAssignmentNotification>(json);
            if (notification != null)
            {
                _logger.LogInformation(
                    "[{LocalTime}] [服务端模式-处理通知] 解析到格口分配通知 | ClientId={ClientId} | ParcelId={ParcelId} | ChuteId={ChuteId}",
                    _systemClock.LocalNow,
                    client.Id,
                    notification.ParcelId,
                    notification.ChuteId);

                // 转换 DwsPayload from DTO to domain model
                DwsMeasurement? dwsPayload = null;
                if (notification.DwsPayload != null)
                {
                    dwsPayload = new DwsMeasurement
                    {
                        WeightGrams = notification.DwsPayload.WeightGrams,
                        LengthMm = notification.DwsPayload.LengthMm,
                        WidthMm = notification.DwsPayload.WidthMm,
                        HeightMm = notification.DwsPayload.HeightMm,
                        VolumetricWeightGrams = notification.DwsPayload.VolumetricWeightGrams,
                        Barcode = notification.DwsPayload.Barcode,
                        MeasuredAt = notification.DwsPayload.MeasuredAt
                    };
                }

                // 触发格口分配事件，SortingOrchestrator 订阅此事件并更新 RoutePlan
                var eventArgs = new ChuteAssignmentEventArgs
                {
                    ParcelId = notification.ParcelId,
                    ChuteId = notification.ChuteId,
                    AssignedAt = notification.AssignedAt,
                    DwsPayload = dwsPayload,
                    Metadata = notification.Metadata
                };
                
                ChuteAssigned.SafeInvoke(this, eventArgs, _logger, nameof(ChuteAssigned));
                
                // 同时调用 handler（如果存在）以保持向后兼容
                if (_handler != null)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _handler.HandleChuteAssignmentAsync(eventArgs);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(
                                ex,
                                "[{LocalTime}] [服务端模式-处理错误] 处理包裹 {ParcelId} 的格口分配时发生错误",
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
                if (_service?.Clients.TryGetClient(kvp.Key, out var socketClient) ?? false)
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

    public async Task BroadcastParcelDetectedAsync(
        long parcelId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[{LocalTime}] [服务端模式-广播-开始] BroadcastParcelDetectedAsync: ParcelId={ParcelId}, ClientsCount={ClientCount}, IsRunning={IsRunning}",
            _systemClock.LocalNow,
            parcelId,
            _clients.Count,
            _isRunning);

        var notification = new ParcelDetectionNotification
        {
            ParcelId = parcelId,
            DetectionTime = _systemClock.LocalNowOffset
        };

        var json = JsonSerializer.Serialize(notification);
        var bytes = Encoding.UTF8.GetBytes(json + "\n");

        var disconnectedClients = new List<string>();
        var successCount = 0;

        foreach (var kvp in _clients)
        {
            try
            {
                if (_service?.Clients.TryGetClient(kvp.Key, out var socketClient) ?? false)
                {
                    await socketClient.SendAsync(bytes);
                    successCount++;

                    _logger.LogInformation(
                        "[{LocalTime}] [服务端模式-广播-成功] 已向客户端 {ClientId} 广播包裹检测通知: ParcelId={ParcelId}",
                        _systemClock.LocalNow,
                        kvp.Key,
                        parcelId);
                }
                else
                {
                    _logger.LogWarning(
                        "[{LocalTime}] [服务端模式-广播-跳过] 无法获取客户端 {ClientId} 的socket连接: ParcelId={ParcelId}",
                        _systemClock.LocalNow,
                        kvp.Key,
                        parcelId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "[{LocalTime}] [服务端模式-广播失败] 向客户端 {ClientId} 广播包裹检测通知失败: {Message}",
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

        _logger.LogInformation(
            "[{LocalTime}] [服务端模式-广播-完成] BroadcastParcelDetectedAsync: ParcelId={ParcelId}, SuccessCount={SuccessCount}, FailedCount={FailedCount}, RemainingClientsCount={RemainingCount}",
            _systemClock.LocalNow,
            parcelId,
            successCount,
            disconnectedClients.Count,
            _clients.Count);
    }

    public async Task BroadcastSortingCompletedAsync(
        Core.Abstractions.Upstream.SortingCompletedNotification notification,
        CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(TouchSocketTcpRuleEngineServer));
        }

        if (!_isRunning || _service == null)
        {
            _logger.LogWarning(
                "[{LocalTime}] [服务端模式-广播失败] TCP服务器未运行，无法广播分拣完成通知: ParcelId={ParcelId}",
                _systemClock.LocalNow,
                notification.ParcelId);
            return;
        }

        _logger.LogInformation(
            "[{LocalTime}] [服务端模式-广播] 准备向 {ClientCount} 个客户端广播分拣完成通知: ParcelId={ParcelId}, ActualChuteId={ActualChuteId}, IsSuccess={IsSuccess}",
            _systemClock.LocalNow,
            _clients.Count,
            notification.ParcelId,
            notification.ActualChuteId,
            notification.IsSuccess);

        var notificationDto = new SortingCompletedNotificationDto
        {
            ParcelId = notification.ParcelId,
            ActualChuteId = notification.ActualChuteId,
            CompletedAt = notification.CompletedAt,
            IsSuccess = notification.IsSuccess,
            FinalStatus = notification.FinalStatus,
            FailureReason = notification.FailureReason
        };

        var json = JsonSerializer.Serialize(notificationDto);
        var bytes = Encoding.UTF8.GetBytes(json + "\n");

        var disconnectedClients = new List<string>();

        foreach (var kvp in _clients)
        {
            try
            {
                if (_service?.Clients.TryGetClient(kvp.Key, out var socketClient) ?? false)
                {
                    await socketClient.SendAsync(bytes);

                    _logger.LogDebug(
                        "[{LocalTime}] [服务端模式-广播] 已向客户端 {ClientId} 广播分拣完成通知: ParcelId={ParcelId}",
                        _systemClock.LocalNow,
                        kvp.Key,
                        notification.ParcelId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "[{LocalTime}] [服务端模式-广播失败] 向客户端 {ClientId} 广播分拣完成通知失败: {Message}",
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
