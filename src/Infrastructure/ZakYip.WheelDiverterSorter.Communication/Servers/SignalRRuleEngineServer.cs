using ZakYip.WheelDiverterSorter.Core.Abstractions.Upstream;
using ZakYip.WheelDiverterSorter.Core.Events.Chute;
using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Communication.Abstractions;
using ZakYip.WheelDiverterSorter.Communication.Configuration;
using ZakYip.WheelDiverterSorter.Communication.Models;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Core.Sorting.Policies;

namespace ZakYip.WheelDiverterSorter.Communication.Servers;

/// <summary>
/// SignalR服务器实现 - Hub实现 + 客户端连接跟踪
/// SignalR Server implementation - Hub implementation + client connection tracking
/// </summary>
public sealed class SignalRRuleEngineServer : IRuleEngineServer
{
    private readonly ILogger<SignalRRuleEngineServer> _logger;
    private readonly UpstreamConnectionOptions _options;
    private readonly ISystemClock _systemClock;
    private readonly IRuleEngineHandler? _handler;
    private readonly IHubContext<RuleEngineHub>? _hubContext;
    
    private readonly ConcurrentDictionary<string, ClientInfo> _clients = new();
    private bool _isRunning;
    private bool _disposed;

    public SignalRRuleEngineServer(
        ILogger<SignalRRuleEngineServer> logger,
        UpstreamConnectionOptions options,
        ISystemClock systemClock,
        IHubContext<RuleEngineHub>? hubContext = null,
        IRuleEngineHandler? handler = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _systemClock = systemClock ?? throw new ArgumentNullException(nameof(systemClock));
        _hubContext = hubContext;
        _handler = handler;

        ValidateSignalRServerOptions(options);
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
            _logger.LogWarning("SignalR服务器已在运行");
            return Task.CompletedTask;
        }

        _isRunning = true;

        _logger.LogInformation(
            "[{LocalTime}] SignalR服务器已启动（Hub: {HubUrl}）",
            _systemClock.LocalNow,
            _options.SignalRHub);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (!_isRunning)
        {
            return Task.CompletedTask;
        }

        _isRunning = false;
        _clients.Clear();

        _logger.LogInformation(
            "[{LocalTime}] SignalR服务器已停止",
            _systemClock.LocalNow);

        return Task.CompletedTask;
    }

    public async Task BroadcastChuteAssignmentAsync(
        long parcelId,
        string chuteId,
        CancellationToken cancellationToken = default)
    {
        if (_hubContext == null)
        {
            _logger.LogWarning("HubContext未注入，无法广播消息");
            return;
        }

        if (!_isRunning)
        {
            _logger.LogWarning("SignalR服务器未运行，无法广播消息");
            return;
        }

        // PR-UPSTREAM02: 使用 ChuteAssignmentNotification 代替 ChuteAssignmentResponse
        var notification = new ChuteAssignmentNotification
        {
            ParcelId = parcelId,
            ChuteId = int.Parse(chuteId),
            AssignedAt = _systemClock.LocalNowOffset
        };

        await _hubContext.Clients.All.SendAsync(
            "ReceiveChuteAssignment",
            notification,
            cancellationToken);

        _logger.LogInformation(
            "[{LocalTime}] 已向所有SignalR客户端广播包裹 {ParcelId} 的格口分配: {ChuteId}",
            _systemClock.LocalNow,
            parcelId,
            chuteId);
    }

    internal void RegisterClient(string connectionId)
    {
        var clientInfo = new ClientInfo
        {
            ClientId = connectionId,
            ConnectedAt = _systemClock.LocalNowOffset
        };

        _clients[connectionId] = clientInfo;

        _logger.LogInformation(
            "[{LocalTime}] SignalR客户端已连接: {ClientId}",
            _systemClock.LocalNow,
            connectionId);

        // 触发客户端连接事件
        ClientConnected?.Invoke(this, new ClientConnectionEventArgs
        {
            ClientId = connectionId,
            ConnectedAt = clientInfo.ConnectedAt,
            ClientAddress = null
        });
    }

    internal void UnregisterClient(string connectionId)
    {
        if (_clients.TryRemove(connectionId, out var clientInfo))
        {
            _logger.LogInformation(
                "[{LocalTime}] SignalR客户端已断开: {ClientId}",
                _systemClock.LocalNow,
                connectionId);

            // 触发客户端断开事件
            ClientDisconnected?.Invoke(this, new ClientConnectionEventArgs
            {
                ClientId = connectionId,
                ConnectedAt = clientInfo.ConnectedAt,
                ClientAddress = null
            });
        }
    }

    /// <summary>
    /// 处理包裹检测通知
    /// </summary>
    /// <remarks>
    /// PR-UPSTREAM02: 从 ChuteAssignmentRequest 改为 ParcelDetectionNotification
    /// </remarks>
    internal async Task HandleParcelNotificationAsync(string connectionId, ParcelDetectionNotification notification)
    {
        _logger.LogInformation(
            "[{LocalTime}] 收到SignalR客户端 {ClientId} 的包裹检测通知: ParcelId={ParcelId}",
            _systemClock.LocalNow,
            connectionId,
            notification.ParcelId);

        // 触发包裹通知接收事件
        ParcelNotificationReceived?.Invoke(this, new ParcelNotificationReceivedEventArgs
        {
            ParcelId = notification.ParcelId,
            ClientId = connectionId,
            ReceivedAt = _systemClock.LocalNowOffset
        });

        // 如果有处理器，调用处理器
        if (_handler != null)
        {
            var eventArgs = new ChuteAssignmentEventArgs
            {
                ParcelId = notification.ParcelId,
                ChuteId = 0, // Server模式下由RuleEngine决定
                AssignedAt = notification.DetectionTime
            };
            await _handler.HandleChuteAssignmentAsync(eventArgs);
        }
    }

    private static void ValidateSignalRServerOptions(UpstreamConnectionOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.SignalRHub))
        {
            throw new ArgumentException("SignalR Hub URL不能为空", nameof(options));
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        StopAsync().GetAwaiter().GetResult();
        _disposed = true;
    }

    private sealed class ClientInfo
    {
        public required string ClientId { get; init; }
        public required DateTimeOffset ConnectedAt { get; init; }
    }
}

/// <summary>
/// SignalR Hub for RuleEngine communication
/// </summary>
public class RuleEngineHub : Hub
{
    private readonly ILogger<RuleEngineHub> _logger;
    private readonly SignalRRuleEngineServer? _server;

    public RuleEngineHub(
        ILogger<RuleEngineHub> logger,
        SignalRRuleEngineServer? server = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _server = server;
    }

    public override async Task OnConnectedAsync()
    {
        _server?.RegisterClient(Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _server?.UnregisterClient(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// 接收客户端的包裹检测通知
    /// </summary>
    /// <remarks>
    /// PR-UPSTREAM02: 从 ChuteAssignmentRequest 改为 ParcelDetectionNotification
    /// </remarks>
    public async Task NotifyParcelDetected(ParcelDetectionNotification notification)
    {
        if (_server != null)
        {
            await _server.HandleParcelNotificationAsync(Context.ConnectionId, notification);
        }
    }
}
