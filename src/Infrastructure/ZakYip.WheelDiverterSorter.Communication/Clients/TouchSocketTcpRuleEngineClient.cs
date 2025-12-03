using ZakYip.WheelDiverterSorter.Core.Events.Chute;
using ZakYip.WheelDiverterSorter.Communication.Models;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Upstream;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Communication.Configuration;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using TouchSocket.Core;
using TouchSocket.Sockets;

namespace ZakYip.WheelDiverterSorter.Communication.Clients;

/// <summary>
/// 基于TouchSocket的TCP上游路由通信客户端
/// </summary>
/// <remarks>
/// 使用TouchSocket库实现TCP通信，提供：
/// - 自动重连机制（指数退避，最大2秒）
/// - 无限重试策略
/// - 详细连接日志（成功/失败/重试）
/// - 配置热更新支持
/// </remarks>
public class TouchSocketTcpRuleEngineClient : RuleEngineClientBase
{
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private TcpClient? _client;
    private bool _isConnected;
    private CancellationTokenSource? _reconnectCts;
    private Task? _reconnectTask;
    
    /// <summary>
    /// 最大退避时间（硬编码2秒）
    /// </summary>
    private const int MaxBackoffMs = 2000;

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

            var parts = Options.TcpServer!.Split(':');
            var host = parts[0];
            var port = int.Parse(parts[1]);

            Logger.LogInformation(
                "[{LocalTime}] [客户端模式] 正在连接到RuleEngine TCP服务器 {Host}:{Port}...",
                SystemClock.LocalNow,
                host,
                port);

            _client = new TcpClient();
            
            // 配置TouchSocket客户端
            await _client.SetupAsync(new TouchSocketConfig()
                .SetRemoteIPHost($"{host}:{port}")
                .SetTcpDataHandlingAdapter(() => new TerminatorPackageAdapter("\n") 
                { 
                    CacheTimeout = TimeSpan.FromMilliseconds(Options.TimeoutMs)
                })
                .ConfigurePlugins(a =>
                {
                    // 接收消息处理插件
                    a.Add<TouchSocketReceivePlugin>();
                }));

            // 注册接收事件
            _client.Received += (ITcpClient client, ReceivedDataEventArgs e) => { OnMessageReceived((TcpClient)client, e); return Task.CompletedTask; };
            _client.Closed += (ITcpClient client, ClosedEventArgs e) => { OnDisconnected((TcpClient)client, e); return Task.CompletedTask; };
            _client.Connected += (ITcpClient client, ConnectedEventArgs e) => { OnConnected((TcpClient)client, e); return Task.CompletedTask; };

            // 尝试连接
            await _client.ConnectAsync(Options.TimeoutMs, cancellationToken);
            
            _isConnected = true;

            Logger.LogInformation(
                "[{LocalTime}] [客户端模式-连接成功] 成功连接到RuleEngine TCP服务器 {Host}:{Port} (缓冲区: {Buffer}KB)",
                SystemClock.LocalNow,
                host,
                port,
                Options.Tcp.ReceiveBufferSize / 1024);

            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError(
                ex,
                "[{LocalTime}] [客户端模式-连接失败] 连接到RuleEngine TCP服务器失败: {Message}",
                SystemClock.LocalNow,
                ex.Message);
            
            _isConnected = false;
            
            // 启动自动重连
            StartAutoReconnect();
            
            return false;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    private void OnConnected(TcpClient client, ConnectedEventArgs e)
    {
        _isConnected = true;
        Logger.LogInformation(
            "[{LocalTime}] [客户端模式-连接成功] TouchSocket客户端已连接到 {RemoteIPHost}",
            SystemClock.LocalNow,
            client.IP);
    }

    private void OnDisconnected(TcpClient client, ClosedEventArgs e)
    {
        _isConnected = false;
        Logger.LogWarning(
            "[{LocalTime}] [客户端模式-连接断开] TouchSocket客户端已断开连接: {Message}",
            SystemClock.LocalNow,
            e.Message);
        
        // 启动自动重连
        StartAutoReconnect();
    }

    private Task OnMessageReceived(TcpClient client, ReceivedDataEventArgs e)
    {
        try
        {
            var json = Encoding.UTF8.GetString(e.ByteBlock.Span).Trim();

            Logger.LogInformation(
                "[{LocalTime}] [上游通信-接收] TouchSocket TCP通道接收消息 | 消息内容={MessageContent} | 字节数={ByteCount}",
                SystemClock.LocalNow,
                json,
                e.ByteBlock.Length);

            // 尝试反序列化为格口分配通知
            var notification = JsonSerializer.Deserialize<ChuteAssignmentNotification>(json);
            if (notification != null)
            {
                Logger.LogInformation(
                    "[{LocalTime}] [上游通信-接收] 解析到格口分配通知 | ParcelId={ParcelId} | ChuteId={ChuteId}",
                    SystemClock.LocalNow,
                    notification.ParcelId,
                    notification.ChuteId);

                // 触发格口分配事件
                DwsMeasurement? dwsMeasurement = null;
                if (notification.DwsPayload != null)
                {
                    dwsMeasurement = new DwsMeasurement
                    {
                        WeightGrams = notification.DwsPayload.WeightGrams,
                        LengthMm = notification.DwsPayload.LengthMm,
                        WidthMm = notification.DwsPayload.WidthMm,
                        HeightMm = notification.DwsPayload.HeightMm
                    };
                }
                
                OnChuteAssignmentReceived(
                    notification.ParcelId, 
                    notification.ChuteId, 
                    notification.AssignedAt,
                    dwsMeasurement);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(
                ex,
                "[{LocalTime}] [上游通信-接收错误] 处理TouchSocket接收消息时发生错误",
                SystemClock.LocalNow);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 启动自动重连任务
    /// </summary>
    private void StartAutoReconnect()
    {
        if (_reconnectTask != null && !_reconnectTask.IsCompleted)
        {
            return; // 已有重连任务在运行
        }

        _reconnectCts?.Cancel();
        _reconnectCts = new CancellationTokenSource();
        _reconnectTask = ReconnectLoopAsync(_reconnectCts.Token);
    }

    /// <summary>
    /// 无限重连循环（指数退避，最大2秒）
    /// </summary>
    private async Task ReconnectLoopAsync(CancellationToken cancellationToken)
    {
        int backoffMs = 200; // 初始退避时间

        while (!cancellationToken.IsCancellationRequested && !IsConnected)
        {
            try
            {
                Logger.LogInformation(
                    "[{LocalTime}] [客户端模式-重试连接] 尝试重新连接到RuleEngine (退避时间: {BackoffMs}ms)",
                    SystemClock.LocalNow,
                    backoffMs);

                await Task.Delay(backoffMs, cancellationToken);

                var success = await ConnectAsync(cancellationToken);
                if (success)
                {
                    Logger.LogInformation(
                        "[{LocalTime}] [客户端模式-重连成功] 成功重新连接到RuleEngine",
                        SystemClock.LocalNow);
                    break;
                }

                // 指数退避，最大2秒
                backoffMs = Math.Min(backoffMs * 2, MaxBackoffMs);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Logger.LogWarning(
                    ex,
                    "[{LocalTime}] [客户端模式-重连失败] 重连尝试失败: {Message}",
                    SystemClock.LocalNow,
                    ex.Message);

                // 继续重试
                backoffMs = Math.Min(backoffMs * 2, MaxBackoffMs);
            }
        }
    }

    /// <summary>
    /// 断开与RuleEngine的连接
    /// </summary>
    public override async Task DisconnectAsync()
    {
        ThrowIfDisposed();

        try
        {
            _reconnectCts?.Cancel();
            
            if (_client != null)
            {
                await _client.CloseAsync();
                _client.Dispose();
            }
            
            _isConnected = false;
            
            Logger.LogInformation(
                "[{LocalTime}] [客户端模式] 已断开与RuleEngine TCP服务器的连接",
                SystemClock.LocalNow);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(
                ex,
                "[{LocalTime}] [客户端模式] 断开连接时发生异常",
                SystemClock.LocalNow);
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
                "[{LocalTime}] 向RuleEngine发送包裹 {ParcelId} 通知失败: {Message}",
                SystemClock.LocalNow,
                parcelId,
                ex.Message);
            return false;
        }
    }

    private async Task SendNotificationAsync(long parcelId, CancellationToken cancellationToken)
    {
        var notification = new ParcelDetectionNotification
        {
            ParcelId = parcelId,
            DetectionTime = SystemClock.LocalNowOffset
        };
        var notificationJson = JsonSerializer.Serialize(notification);
        var notificationBytes = Encoding.UTF8.GetBytes(notificationJson + "\n");

        Logger.LogInformation(
            "[{LocalTime}] [上游通信-发送] TouchSocket TCP通道发送包裹检测通知 | ParcelId={ParcelId} | 消息内容={MessageContent}",
            SystemClock.LocalNow,
            parcelId,
            notificationJson);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(Options.TimeoutMs);

        await _client!.SendAsync(notificationBytes);

        Logger.LogInformation(
            "[{LocalTime}] [上游通信-发送完成] TouchSocket TCP通道成功发送包裹检测通知 | ParcelId={ParcelId} | 字节数={ByteCount}",
            SystemClock.LocalNow,
            parcelId,
            notificationBytes.Length);
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
            var notificationDto = new SortingCompletedNotificationDto
            {
                ParcelId = notification.ParcelId,
                ActualChuteId = notification.ActualChuteId,
                CompletedAt = notification.CompletedAt,
                IsSuccess = notification.IsSuccess,
                FailureReason = notification.FailureReason
            };

            var json = JsonSerializer.Serialize(notificationDto);
            var bytes = Encoding.UTF8.GetBytes(json + "\n");

            Logger.LogInformation(
                "[{LocalTime}] [上游通信-发送] TouchSocket TCP通道发送落格完成通知 | ParcelId={ParcelId} | IsSuccess={IsSuccess}",
                SystemClock.LocalNow,
                notification.ParcelId,
                notification.IsSuccess);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(Options.TimeoutMs);

            await _client!.SendAsync(bytes);

            Logger.LogInformation(
                "[{LocalTime}] [上游通信-发送完成] TouchSocket TCP通道成功发送落格完成通知 | ParcelId={ParcelId}",
                SystemClock.LocalNow,
                notification.ParcelId);

            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError(
                ex,
                "[{LocalTime}] 向RuleEngine发送包裹 {ParcelId} 落格完成通知失败: {Message}",
                SystemClock.LocalNow,
                notification.ParcelId,
                ex.Message);
            return false;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _reconnectCts?.Cancel();
            _reconnectCts?.Dispose();
            _client?.Dispose();
            _connectionLock.Dispose();
        }
        base.Dispose(disposing);
    }
}

/// <summary>
/// TouchSocket接收消息处理插件
/// </summary>
file class TouchSocketReceivePlugin : PluginBase, ITcpReceivedPlugin
{
    public Task OnTcpReceived(ITcpSession client, ReceivedDataEventArgs e)
    {
        // 消息已经由 TerminatorPackageAdapter 处理，这里只需要传递
        return e.InvokeNext();
    }
}
