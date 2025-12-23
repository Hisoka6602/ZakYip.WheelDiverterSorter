using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Communication.Abstractions;
using ZakYip.WheelDiverterSorter.Communication.Infrastructure;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Upstream;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Core.Sorting.Policies;

namespace ZakYip.WheelDiverterSorter.Communication.Adapters;

/// <summary>
/// 服务端模式客户端适配器
/// </summary>
/// <remarks>
/// 当系统运行在Server模式时，将IUpstreamRoutingClient接口调用转换为IRuleEngineServer的广播调用。
/// 这确保在服务端模式下，NotifyParcelDetectedAsync 和 NotifySortingCompletedAsync 
/// 会正确地广播给所有连接的上游客户端。
/// 
/// PR-DUAL-INSTANCE-FIX: 不再持有独立的服务器实例，而是引用 UpstreamServerBackgroundService.CurrentServer
/// 这确保整个系统只有一个服务器实例在运行。
/// </remarks>
public sealed class ServerModeClientAdapter : IUpstreamRoutingClient
{
    private readonly UpstreamServerBackgroundService _serverBackgroundService;
    private readonly ILogger<ServerModeClientAdapter> _logger;
    private readonly ISystemClock _systemClock;
    private bool _disposed;
    private bool _eventSubscribed; // PR-UPSTREAM-SERVER-FIX: 跟踪事件订阅状态

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="serverBackgroundService">服务器后台服务（提供服务器实例）</param>
    /// <param name="logger">日志记录器</param>
    /// <param name="systemClock">系统时钟</param>
    public ServerModeClientAdapter(
        UpstreamServerBackgroundService serverBackgroundService,
        ILogger<ServerModeClientAdapter> logger,
        ISystemClock systemClock)
    {
        _serverBackgroundService = serverBackgroundService ?? throw new ArgumentNullException(nameof(serverBackgroundService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _systemClock = systemClock ?? throw new ArgumentNullException(nameof(systemClock));
        
        // PR-UPSTREAM-SERVER-FIX: 在构造函数中立即尝试订阅事件
        // 关键修复：不等待 ConnectAsync 被调用（因为它可能永远不会被调用），主动订阅
        // 使用后台任务轮询服务器是否就绪，一旦就绪立即订阅
        _ = Task.Run(async () =>
        {
            // 等待服务器启动（最多等待30秒）
            var maxWaitTime = TimeSpan.FromSeconds(30);
            var startTime = DateTime.UtcNow;
            
            while (DateTime.UtcNow - startTime < maxWaitTime)
            {
                if (_serverBackgroundService.CurrentServer?.IsRunning == true)
                {
                    EnsureServerEventSubscription();
                    _logger.LogInformation(
                        "[{LocalTime}] [服务端模式-适配器-自动订阅] 服务器已就绪，已自动订阅 ChuteAssigned 事件",
                        _systemClock.LocalNow);
                    return;
                }
                
                await Task.Delay(500); // 每500ms检查一次
            }
            
            _logger.LogWarning(
                "[{LocalTime}] [服务端模式-适配器-自动订阅] 等待服务器启动超时（{MaxWaitSeconds}秒），事件订阅将在首次调用时完成",
                _systemClock.LocalNow,
                maxWaitTime.TotalSeconds);
        });
    }
    
    /// <summary>
    /// 确保已订阅服务器的 ChuteAssigned 事件
    /// </summary>
    private void EnsureServerEventSubscription()
    {
        // 使用局部变量捕获服务器实例，避免竞态条件
        var server = _serverBackgroundService.CurrentServer;
        if (server == null)
            return;
        
        // 如果已经订阅过，跳过（避免重复订阅）    
        if (_eventSubscribed)
        {
            _logger.LogDebug(
                "[{LocalTime}] [服务端模式-适配器] 已经订阅过 ChuteAssigned 事件，跳过重复订阅",
                _systemClock.LocalNow);
            return;
        }
            
        // 订阅服务器的 ChuteAssigned 事件（先取消订阅再重新订阅，避免重复订阅）
        server.ChuteAssigned -= OnServerChuteAssigned;
        server.ChuteAssigned += OnServerChuteAssigned;
        _eventSubscribed = true;
        
        _logger.LogInformation(
            "[{LocalTime}] [服务端模式-适配器] ✅ 已订阅服务器的 ChuteAssigned 事件",
            _systemClock.LocalNow);
    }
    
    /// <summary>
    /// 转发服务器收到的格口分配事件
    /// </summary>
    private void OnServerChuteAssigned(object? sender, ChuteAssignmentEventArgs e)
    {
        _logger.LogInformation(
            "[{LocalTime}] [服务端模式-适配器] 转发格口分配事件: ParcelId={ParcelId}, ChuteId={ChuteId}",
            _systemClock.LocalNow,
            e.ParcelId,
            e.ChuteId);
            
        ChuteAssigned?.Invoke(this, e);
    }

    /// <summary>
    /// 获取当前服务器实例
    /// </summary>
    /// <remarks>
    /// PR-DUAL-INSTANCE-FIX: 从 UpstreamServerBackgroundService 获取统一的服务器实例
    /// </remarks>
    private IRuleEngineServer Server => _serverBackgroundService.CurrentServer 
        ?? throw new InvalidOperationException(
            "服务器实例不可用。请确保 Server 模式下 UpstreamServerBackgroundService 已启动。");

    /// <summary>
    /// 服务器是否正在运行（等同于IsConnected）
    /// </summary>
    public bool IsConnected => _serverBackgroundService.CurrentServer?.IsRunning ?? false;

    /// <summary>
    /// 格口分配事件（从上游客户端接收格口分配通知）
    /// </summary>
    /// <remarks>
    /// PR-FIX: 在服务端模式下，当上游客户端发送格口分配通知时，
    /// 服务器会触发 ChuteAssigned 事件，适配器需要转发这个事件给 Orchestrator。
    /// 这对于 Formal 模式的正确运行至关重要。
    /// </remarks>
    public event EventHandler<ChuteAssignmentEventArgs>? ChuteAssigned;

    /// <summary>
    /// 连接到RuleEngine（启动服务器）
    /// </summary>
    /// <remarks>
    /// PR-DUAL-INSTANCE-FIX: Server由UpstreamServerBackgroundService管理，此处仅检查状态
    /// </remarks>
    public Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (_serverBackgroundService.CurrentServer?.IsRunning == true)
        {
            // 订阅服务器的事件
            EnsureServerEventSubscription();
            
            _logger.LogDebug(
                "[{LocalTime}] [服务端模式-适配器] 服务器已运行，当前连接客户端数: {ClientCount}",
                _systemClock.LocalNow,
                Server.ConnectedClientsCount);
            return Task.FromResult(true);
        }

        _logger.LogWarning(
            "[{LocalTime}] [服务端模式-适配器] 服务器未运行。服务器由 UpstreamServerBackgroundService 管理，请等待其启动。",
            _systemClock.LocalNow);
        
        return Task.FromResult(false);
    }

    /// <summary>
    /// 断开连接（停止服务器）
    /// </summary>
    /// <remarks>
    /// PR-DUAL-INSTANCE-FIX: Server由UpstreamServerBackgroundService管理，此处不执行停止操作
    /// </remarks>
    public Task DisconnectAsync()
    {
        ThrowIfDisposed();
        
        _logger.LogWarning(
            "[{LocalTime}] [服务端模式-适配器] 服务器由 UpstreamServerBackgroundService 管理，不能通过适配器停止。",
            _systemClock.LocalNow);
        
        return Task.CompletedTask;
    }

    /// <summary>
    /// 通知包裹检测（广播给所有连接的客户端）
    /// </summary>
    /// <summary>
    /// 发送消息到上游系统（统一发送接口）
    /// </summary>
    public async Task<bool> SendAsync(IUpstreamMessage message, CancellationToken cancellationToken = default)
    {
        return message switch
        {
            ParcelDetectedMessage detected => await NotifyParcelDetectedAsync(detected.ParcelId, cancellationToken),
            SortingCompletedMessage completed => await NotifySortingCompletedAsync(completed.Notification, cancellationToken),
            PanelButtonPressedMessage panelButton => await HandlePanelButtonPressedAsync(panelButton, cancellationToken),
            _ => throw new ArgumentException($"不支持的消息类型: {message.GetType().Name}", nameof(message))
        };
    }

    /// <summary>
    /// 处理面板按钮按下消息
    /// </summary>
    /// <param name="message">面板按钮按下消息</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>始终返回true，表示消息已被处理</returns>
    /// <remarks>
    /// 在服务端模式下，面板按钮事件是本地操作，不需要广播给上游客户端。
    /// 此方法记录日志并返回true，表示消息已被处理（虽然不需要实际发送）。
    /// </remarks>
    private Task<bool> HandlePanelButtonPressedAsync(
        PanelButtonPressedMessage message,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        _logger.LogInformation(
            "[{LocalTime}] [服务端模式-适配器] 面板按钮按下通知 (本地操作，不广播): Button={ButtonType}, State={Before}->{After}",
            _systemClock.LocalNow,
            message.ButtonType,
            message.SystemStateBefore,
            message.SystemStateAfter);
        
        // 面板按钮操作是本地的系统状态控制，在服务端模式下不需要广播给客户端
        // 返回true表示消息已被成功处理
        return Task.FromResult(true);
    }

    public async Task<bool> NotifyParcelDetectedAsync(
        long parcelId,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        // PR-UPSTREAM-SERVER-FIX: 确保每次发送前都已订阅事件（防御性编程）
        EnsureServerEventSubscription();

        try
        {
            _logger.LogInformation(
                "[{LocalTime}] [服务端模式-适配器] 转换NotifyParcelDetectedAsync为BroadcastParcelDetectedAsync: ParcelId={ParcelId}, ServerIsRunning={IsRunning}, ConnectedClientsCount={ClientCount}",
                _systemClock.LocalNow,
                parcelId,
                Server.IsRunning,
                Server.ConnectedClientsCount);

            await Server.BroadcastParcelDetectedAsync(parcelId, cancellationToken);
            
            _logger.LogInformation(
                "[{LocalTime}] [服务端模式-适配器] BroadcastParcelDetectedAsync调用完成: ParcelId={ParcelId}",
                _systemClock.LocalNow,
                parcelId);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[{LocalTime}] [服务端模式-适配器] 广播包裹检测通知失败: ParcelId={ParcelId}",
                _systemClock.LocalNow,
                parcelId);
            return false;
        }
    }

    /// <summary>
    /// 通知分拣完成（广播给所有连接的客户端）
    /// </summary>
    public async Task<bool> NotifySortingCompletedAsync(
        SortingCompletedNotification notification,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(notification);

        try
        {
            _logger.LogDebug(
                "[{LocalTime}] [服务端模式-适配器] 转换NotifySortingCompletedAsync为BroadcastSortingCompletedAsync: ParcelId={ParcelId}",
                _systemClock.LocalNow,
                notification.ParcelId);

            await Server.BroadcastSortingCompletedAsync(notification, cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[{LocalTime}] [服务端模式-适配器] 广播分拣完成通知失败: ParcelId={ParcelId}",
                _systemClock.LocalNow,
                notification.ParcelId);
            return false;
        }
    }

    /// <summary>
    /// Ping上游系统进行健康检查
    /// </summary>
    public Task<bool> PingAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        // Server模式：始终返回true（服务器自身总是"连接"的）
        return Task.FromResult(true);
    }

    /// <summary>
    /// 热更新连接参数
    /// </summary>
    public Task UpdateOptionsAsync(UpstreamConnectionOptions options)
    {
        ThrowIfDisposed();
        _logger.LogWarning("Server模式不支持热更新连接参数");
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        // PR-DUAL-INSTANCE-FIX: 不再持有服务器实例，无需 Dispose
        // 服务器由 UpstreamServerBackgroundService 管理
        _disposed = true;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ServerModeClientAdapter));
        }
    }
}
