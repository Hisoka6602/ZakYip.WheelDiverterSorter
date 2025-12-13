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
    /// 格口分配事件（服务端模式下不使用）
    /// </summary>
    /// <remarks>
    /// 在服务端模式下，格口分配是由本地业务逻辑决定的，不需要从上游接收。
    /// 此事件仅为实现接口而保留，不会被触发。
    /// </remarks>
#pragma warning disable CS0067 // Event is never used (intentional for server mode)
    public event EventHandler<ChuteAssignmentEventArgs>? ChuteAssigned;
#pragma warning restore CS0067

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
            _ => throw new ArgumentException($"不支持的消息类型: {message.GetType().Name}", nameof(message))
        };
    }

    public async Task<bool> NotifyParcelDetectedAsync(
        long parcelId,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

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
