using ZakYip.WheelDiverterSorter.Core.Events.Chute;
using ZakYip.WheelDiverterSorter.Communication.Models;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Upstream;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Communication.Configuration;
using ZakYip.WheelDiverterSorter.Core.Utilities;

namespace ZakYip.WheelDiverterSorter.Communication.Clients;

/// <summary>
/// 基于SignalR的上游路由通信客户端
/// </summary>
/// <remarks>
/// 推荐生产环境使用，提供实时双向通信和自动重连机制
/// PR-U1: 直接实现 IUpstreamRoutingClient（通过基类）
/// PR-UPSTREAM02: 添加落格完成通知，更新消息处理以支持新的格口分配通知格式
/// </remarks>
public class SignalRRuleEngineClient : RuleEngineClientBase
{
    private HubConnection? _connection;

    /// <summary>
    /// 客户端是否已连接
    /// </summary>
    public override bool IsConnected => _connection?.State == HubConnectionState.Connected;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="options">连接配置</param>
    /// <param name="systemClock">系统时钟</param>
    public SignalRRuleEngineClient(
        ILogger<SignalRRuleEngineClient> logger,
        RuleEngineConnectionOptions options,
        ISystemClock systemClock) : base(logger, options, systemClock)
    {
        if (string.IsNullOrWhiteSpace(options.SignalRHub))
        {
            throw new ArgumentException("SignalR Hub URL不能为空", nameof(options));
        }

        InitializeConnection();
    }

    /// <summary>
    /// 初始化SignalR连接
    /// </summary>
    private void InitializeConnection()
    {
        var builder = new HubConnectionBuilder()
            .WithUrl(Options.SignalRHub!)
            .ConfigureLogging(logging =>
            {
                logging.AddFilter("Microsoft.AspNetCore.SignalR", LogLevel.Information);
            });

        // 配置自动重连
        if (Options.EnableAutoReconnect)
        {
            if (Options.SignalR.ReconnectIntervals != null && Options.SignalR.ReconnectIntervals.Length > 0)
            {
                // 使用自定义重连间隔
                var delays = Options.SignalR.ReconnectIntervals.Select(i => TimeSpan.FromMilliseconds(i)).ToArray();
                builder.WithAutomaticReconnect(delays);
            }
            else
            {
                // 使用默认重连策略
                builder.WithAutomaticReconnect();
            }
        }

        _connection = builder.Build();

        // 配置连接超时
        _connection.HandshakeTimeout = TimeSpan.FromSeconds(Options.SignalR.HandshakeTimeout);
        _connection.KeepAliveInterval = TimeSpan.FromSeconds(Options.SignalR.KeepAliveInterval);
        _connection.ServerTimeout = TimeSpan.FromSeconds(Options.SignalR.ServerTimeout);

        // 注册格口分配推送的处理程序
        _connection.On<ChuteAssignmentNotificationEventArgs>(
            "ReceiveChuteAssignment",
            HandleChuteAssignmentReceived);

        _connection.Closed += async (error) =>
        {
            Logger.LogWarning(error, "SignalR连接已关闭");
            if (Options.EnableAutoReconnect)
            {
                await Task.Delay(Options.RetryDelayMs);
                await ConnectAsync();
            }
        };

        _connection.Reconnecting += (error) =>
        {
            Logger.LogWarning(error, "SignalR正在重新连接...");
            return Task.CompletedTask;
        };

        _connection.Reconnected += (connectionId) =>
        {
            Logger.LogInformation("SignalR重新连接成功，连接ID: {ConnectionId}", connectionId);
            return Task.CompletedTask;
        };
    }

    /// <summary>
    /// 处理接收到的格口分配通知
    /// </summary>
    /// <remarks>
    /// PR-UPSTREAM02: 更新以使用新的 AssignedAt 字段和 DWS 数据
    /// </remarks>
    private void HandleChuteAssignmentReceived(ChuteAssignmentNotificationEventArgs notification)
    {
        // 记录接收到的完整消息内容
        Logger.LogInformation(
            "[上游通信-接收] SignalR通道收到格口分配通知 | ParcelId={ParcelId} | ChuteId={ChuteId} | AssignedAt={AssignedAt}",
            notification.ParcelId,
            notification.ChuteId,
            notification.AssignedAt);

        // PR-UPSTREAM02: 使用共享方法转换 DWS 数据
        OnChuteAssignmentReceived(
            notification.ParcelId,
            notification.ChuteId,
            notification.AssignedAt,
            MapDwsPayload(notification.DwsPayload),
            notification.Metadata);
    }

    /// <summary>
    /// 连接到RuleEngine
    /// </summary>
    public override async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (IsConnected)
        {
            return true;
        }

        try
        {
            Logger.LogInformation("正在连接到RuleEngine SignalR Hub: {HubUrl}...", Options.SignalRHub);
            
            await _connection!.StartAsync(cancellationToken);
            
            Logger.LogInformation(
                "成功连接到RuleEngine SignalR Hub (Timeout: {Timeout}s, KeepAlive: {KeepAlive}s)",
                Options.SignalR.ServerTimeout,
                Options.SignalR.KeepAliveInterval);
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "连接到RuleEngine SignalR Hub失败");
            return false;
        }
    }

    /// <summary>
    /// 断开与RuleEngine的连接
    /// </summary>
    public override async Task DisconnectAsync()
    {
        try
        {
            if (_connection != null)
            {
                await _connection.StopAsync();
                Logger.LogInformation("已断开与RuleEngine SignalR Hub的连接");
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "断开连接时发生异常");
        }
    }

    /// <summary>
    /// 通知RuleEngine包裹已到达
    /// </summary>
    public override async Task<bool> NotifyParcelDetectedAsync(
        long parcelId,
        CancellationToken cancellationToken = default)
    {
        ValidateParcelId(parcelId);

        // 尝试连接（如果未连接）
        if (!await EnsureConnectedAsync(cancellationToken))
        {
            Logger.LogError("[上游通信-发送] SignalR通道无法连接 | ParcelId={ParcelId}", parcelId);
            return false;
        }

        try
        {
            var notification = new ParcelDetectionNotification 
            { 
                ParcelId = parcelId,
                DetectionTime = SystemClock.LocalNowOffset
            };
            
            // 记录发送的完整消息内容
            Logger.LogInformation(
                "[上游通信-发送] SignalR通道发送包裹检测通知 | ParcelId={ParcelId} | DetectionTime={DetectionTime} | HubMethod=NotifyParcelDetected",
                notification.ParcelId,
                notification.DetectionTime);

            // 调用Hub方法通知包裹到达，不等待响应
            await _connection!.InvokeAsync(
                "NotifyParcelDetected",
                notification,
                cancellationToken);

            Logger.LogInformation(
                "[上游通信-发送完成] SignalR通道成功发送包裹检测通知 | ParcelId={ParcelId}",
                parcelId);
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError(
                ex,
                "[上游通信-发送] SignalR通道发送包裹检测通知失败 | ParcelId={ParcelId}",
                parcelId);
            return false;
        }
    }

    /// <summary>
    /// 通知RuleEngine包裹已完成落格
    /// </summary>
    /// <remarks>
    /// PR-UPSTREAM02: 新增方法，发送落格完成通知（fire-and-forget）
    /// </remarks>
    public override async Task<bool> NotifySortingCompletedAsync(
        SortingCompletedNotification notification,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        // 尝试连接（如果未连接）
        if (!await EnsureConnectedAsync(cancellationToken))
        {
            Logger.LogError(
                "[上游通信-发送] SignalR通道无法连接 | ParcelId={ParcelId}",
                notification.ParcelId);
            return false;
        }

        try
        {
            var dto = new SortingCompletedNotificationDto
            {
                ParcelId = notification.ParcelId,
                ActualChuteId = notification.ActualChuteId,
                CompletedAt = notification.CompletedAt,
                IsSuccess = notification.IsSuccess,
                FailureReason = notification.FailureReason
            };

            Logger.LogInformation(
                "[上游通信-发送] SignalR通道发送落格完成通知 | ParcelId={ParcelId} | ChuteId={ChuteId} | IsSuccess={IsSuccess} | HubMethod=NotifySortingCompleted",
                dto.ParcelId,
                dto.ActualChuteId,
                dto.IsSuccess);

            await _connection!.InvokeAsync(
                "NotifySortingCompleted",
                dto,
                cancellationToken);

            Logger.LogInformation(
                "[上游通信-发送完成] SignalR通道成功发送落格完成通知 | ParcelId={ParcelId}",
                notification.ParcelId);
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError(
                ex,
                "[上游通信-发送] SignalR通道发送落格完成通知失败 | ParcelId={ParcelId}",
                notification.ParcelId);
            return false;
        }
    }

    /// <summary>
    /// 释放托管和非托管资源
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            DisconnectAsync().ConfigureAwait(false).GetAwaiter().GetResult();
            _connection?.DisposeAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        base.Dispose(disposing);
    }
}
