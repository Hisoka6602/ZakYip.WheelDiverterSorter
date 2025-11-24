using ZakYip.WheelDiverterSorter.Communication.Models;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Communication.Abstractions;
using ZakYip.WheelDiverterSorter.Communication.Configuration;
using ZakYip.WheelDiverterSorter.Core.LineModel;
using ZakYip.WheelDiverterSorter.Core.LineModel.Chutes;
using ZakYip.WheelDiverterSorter.Core.Utilities;

namespace ZakYip.WheelDiverterSorter.Communication.Clients;

/// <summary>
/// 基于SignalR的RuleEngine通信客户端
/// </summary>
/// <remarks>
/// 推荐生产环境使用，提供实时双向通信和自动重连机制
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
    private void HandleChuteAssignmentReceived(ChuteAssignmentNotificationEventArgs notification)
    {
        Logger.LogInformation(
            "收到包裹 {ParcelId} 的格口分配: {ChuteId}",
            notification.ParcelId,
            notification.ChuteId);

        OnChuteAssignmentReceived(notification);
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
            Logger.LogError("无法连接到RuleEngine SignalR Hub，无法发送包裹检测通知");
            return false;
        }

        try
        {
            Logger.LogDebug("向RuleEngine发送包裹检测通知: {ParcelId}", parcelId);

            var notification = new ParcelDetectionNotification 
            { 
                ParcelId = parcelId,
                DetectionTime = SystemClock.LocalNowOffset
            };
            
            // 调用Hub方法通知包裹到达，不等待响应
            await _connection!.InvokeAsync(
                "NotifyParcelDetected",
                notification,
                cancellationToken);

            Logger.LogInformation("成功发送包裹检测通知: {ParcelId}", parcelId);
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "发送包裹检测通知失败: {ParcelId}", parcelId);
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
