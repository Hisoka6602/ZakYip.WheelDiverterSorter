using TouchSocket.Core;
using TouchSocket.Sockets;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using ZakYip.WheelDiverterSorter.Core.Events.Chute;
using ZakYip.WheelDiverterSorter.Communication.Models;
using ZakYip.WheelDiverterSorter.Communication.Configuration;
using ZakYip.WheelDiverterSorter.Core.Utilities;

namespace ZakYip.WheelDiverterSorter.Communication.Clients;

/// <summary>
/// 基于TouchSocket的TCP客户端实现
/// </summary>
/// <remarks>
/// 使用TouchSocket框架实现TCP通信，提供：
/// - 自动重连
/// - 心跳保活
/// - 数据适配器
/// - 插件系统
/// </remarks>
public class TouchSocketTcpRuleEngineClient : RuleEngineClientBase
{
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private TcpClient? _client;
    private bool _isConnected;

    /// <summary>
    /// 客户端是否已连接
    /// </summary>
    public override bool IsConnected => _isConnected && _client?.Online == true;

    /// <summary>
    /// 构造函数
    /// </summary>
    public TouchSocketTcpRuleEngineClient(
        ILogger<TouchSocketTcpRuleEngineClient> logger,
        RuleEngineConnectionOptions options,
        ISystemClock systemClock) : base(logger, options, systemClock)
    {
        ValidateTcpOptions(options);
    }

    private static void ValidateTcpOptions(RuleEngineConnectionOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.TcpServer))
        {
            throw new ArgumentException("TCP服务器地址不能为空", nameof(options));
        }

        var parts = options.TcpServer.Split(':');
        if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
        {
            throw new ArgumentException($"无效的TCP服务器地址格式，必须为 'host:port' 格式: {options.TcpServer}", nameof(options));
        }

        if (!int.TryParse(parts[1], out var port) || port <= 0 || port > 65535)
        {
            throw new ArgumentException($"无效的端口号: {parts[1]}", nameof(options));
        }

        if (options.TimeoutMs <= 0)
        {
            throw new ArgumentException($"超时时间必须大于0: {options.TimeoutMs}ms", nameof(options));
        }
    }

    /// <summary>
    /// 连接到RuleEngine
    /// </summary>
    public override async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (IsConnected)
        {
            return true;
        }

        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            if (IsConnected)
            {
                return true;
            }

            _client = new TcpClient();
            
            // 配置TouchSocket客户端
            var config = new TouchSocketConfig()
                .SetRemoteIPHost(Options.TcpServer!)
                .SetBufferLength(Options.Tcp.ReceiveBufferSize)
                .SetSendTimeout(Options.TimeoutMs)
                // 使用换行符作为消息分隔符
                .SetTcpDataHandlingAdapter(() => new TerminatorPackageAdapter("\n"))
                // 配置TCP KeepAlive
                .ConfigurePlugins(p =>
                {
                    if (Options.Tcp.EnableKeepAlive)
                    {
                        // TouchSocket的心跳插件
                        p.UseHeartbeat()
                            .SetTick(TimeSpan.FromSeconds(Options.Tcp.KeepAliveTime))
                            .SetMaxFailCount(Options.Tcp.KeepAliveRetryCount);
                    }
                    
                    // 自动重连插件
                    if (Options.EnableAutoReconnect)
                    {
                        p.UseReconnection()
                            .SetTick(TimeSpan.FromMilliseconds(Options.InitialBackoffMs))
                            .UsePolling()
                            .SetActionForCheck(async (c, i) =>
                            {
                                // 检查连接状态
                                if (!c.Online)
                                {
                                    await c.ConnectAsync(Options.TimeoutMs, cancellationToken);
                                }
                            });
                    }
                });

            // 订阅接收事件
            _client.Received = (client, e) =>
            {
                var message = Encoding.UTF8.GetString(e.ByteBlock.Buffer, 0, e.ByteBlock.Len);
                ProcessReceivedMessage(message);
                return Task.CompletedTask;
            };

            // 订阅连接事件
            _client.Connected = (client, e) =>
            {
                _isConnected = true;
                Logger.LogInformation("TouchSocket TCP客户端已连接到 {Server}", Options.TcpServer);
                return Task.CompletedTask;
            };

            // 订阅断开事件
            _client.Disconnected = (client, e) =>
            {
                _isConnected = false;
                Logger.LogWarning("TouchSocket TCP客户端已断开连接: {Message}", e.Message);
                return Task.CompletedTask;
            };

            await _client.SetupAsync(config);
            await _client.ConnectAsync(Options.TimeoutMs, cancellationToken);

            _isConnected = true;
            Logger.LogInformation(
                "成功连接到RuleEngine TCP服务器 (TouchSocket模式, 缓冲区: {Buffer}KB, KeepAlive: {KeepAlive})",
                Options.Tcp.ReceiveBufferSize / 1024,
                Options.Tcp.EnableKeepAlive);
            
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "TouchSocket TCP客户端连接失败");
            _isConnected = false;
            return false;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    /// <summary>
    /// 断开与RuleEngine的连接
    /// </summary>
    public override Task DisconnectAsync()
    {
        ThrowIfDisposed();

        try
        {
            _client?.Close();
            _isConnected = false;
            Logger.LogInformation("TouchSocket TCP客户端已断开连接");
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "断开连接时发生异常");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 通知RuleEngine包裹已到达
    /// </summary>
    public override async Task<bool> NotifyParcelDetectedAsync(
        long parcelId,
        CancellationToken cancellationToken = default)
    {
        ValidateParcelId(parcelId);

        if (!await EnsureConnectedAsync(cancellationToken))
        {
            return false;
        }

        try
        {
            await SendNotificationAsync(parcelId, cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError(
                ex,
                "向RuleEngine发送包裹 {ParcelId} 通知失败: {Message}",
                parcelId,
                ex.Message);
            return false;
        }
    }

    /// <summary>
    /// 发送包裹检测通知
    /// </summary>
    private async Task SendNotificationAsync(long parcelId, CancellationToken cancellationToken)
    {
        var notification = new ParcelDetectionNotification
        {
            ParcelId = parcelId,
            DetectionTime = SystemClock.LocalNowOffset
        };
        var notificationJson = JsonSerializer.Serialize(notification);

        Logger.LogInformation(
            "[上游通信-发送] TouchSocket TCP通道发送包裹检测通知 | ParcelId={ParcelId} | 消息内容={MessageContent}",
            parcelId,
            notificationJson);

        // TouchSocket自动添加终止符，不需要手动添加\n
        await _client!.SendAsync(notificationJson);

        Logger.LogInformation(
            "[上游通信-发送完成] TouchSocket TCP通道成功发送包裹检测通知 | ParcelId={ParcelId}",
            parcelId);
    }

    /// <summary>
    /// 通知RuleEngine包裹已完成落格
    /// </summary>
    public override async Task<bool> NotifySortingCompletedAsync(
        SortingCompletedNotification notification,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        if (!await EnsureConnectedAsync(cancellationToken))
        {
            return false;
        }

        try
        {
            await SendSortingCompletedNotificationAsync(notification, cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError(
                ex,
                "[上游通信-发送] TouchSocket TCP通道发送落格完成通知失败 | ParcelId={ParcelId} | ChuteId={ChuteId}",
                notification.ParcelId,
                notification.ActualChuteId);
            return false;
        }
    }

    /// <summary>
    /// 发送落格完成通知
    /// </summary>
    private async Task SendSortingCompletedNotificationAsync(
        SortingCompletedNotification notification,
        CancellationToken cancellationToken)
    {
        var dto = new SortingCompletedNotificationDto
        {
            ParcelId = notification.ParcelId,
            ActualChuteId = notification.ActualChuteId,
            CompletedAt = notification.CompletedAt,
            IsSuccess = notification.IsSuccess,
            FailureReason = notification.FailureReason
        };

        var notificationJson = JsonSerializer.Serialize(dto);

        Logger.LogInformation(
            "[上游通信-发送] TouchSocket TCP通道发送落格完成通知 | ParcelId={ParcelId} | ChuteId={ChuteId} | IsSuccess={IsSuccess}",
            notification.ParcelId,
            notification.ActualChuteId,
            notification.IsSuccess);

        await _client!.SendAsync(notificationJson);

        Logger.LogInformation(
            "[上游通信-发送完成] TouchSocket TCP通道成功发送落格完成通知 | ParcelId={ParcelId}",
            notification.ParcelId);
    }

    /// <summary>
    /// 处理接收到的消息
    /// </summary>
    private void ProcessReceivedMessage(string messageJson)
    {
        try
        {
            Logger.LogInformation(
                "[上游通信-接收] TouchSocket TCP通道收到消息 | 消息内容={MessageContent}",
                messageJson);

            var notification = JsonSerializer.Deserialize<ChuteAssignmentNotificationEventArgs>(messageJson);

            if (notification != null)
            {
                Logger.LogInformation(
                    "[上游通信-接收] TouchSocket TCP通道收到格口分配通知 | ParcelId={ParcelId} | ChuteId={ChuteId}",
                    notification.ParcelId,
                    notification.ChuteId);

                OnChuteAssignmentReceived(
                    notification.ParcelId,
                    notification.ChuteId,
                    notification.AssignedAt,
                    MapDwsPayload(notification.DwsPayload),
                    notification.Metadata);
            }
            else
            {
                Logger.LogWarning(
                    "[上游通信-接收] TouchSocket TCP通道无法解析消息为格口分配通知 | 消息内容={MessageContent}",
                    messageJson);
            }
        }
        catch (JsonException ex)
        {
            Logger.LogError(
                ex,
                "[上游通信-接收] TouchSocket TCP通道解析消息时发生JSON异常 | 消息内容={MessageContent}",
                messageJson);
        }
        catch (Exception ex)
        {
            Logger.LogError(
                ex,
                "[上游通信-接收] TouchSocket TCP通道处理消息时发生异常 | 消息内容={MessageContent}",
                messageJson);
        }
    }

    /// <summary>
    /// 释放托管和非托管资源
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try
            {
                _client?.Close();
                _client?.Dispose();
                _isConnected = false;
            }
            catch
            {
                // 忽略dispose过程中的异常
            }

            _connectionLock?.Dispose();
        }

        base.Dispose(disposing);
    }
}
