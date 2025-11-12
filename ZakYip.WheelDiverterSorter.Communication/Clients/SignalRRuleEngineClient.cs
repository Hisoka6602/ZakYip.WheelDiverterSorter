using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Communication.Abstractions;
using ZakYip.WheelDiverterSorter.Communication.Configuration;
using ZakYip.WheelDiverterSorter.Core;

namespace ZakYip.WheelDiverterSorter.Communication.Clients;

/// <summary>
/// 基于SignalR的RuleEngine通信客户端
/// </summary>
/// <remarks>
/// 推荐生产环境使用，提供实时双向通信和自动重连机制
/// </remarks>
public class SignalRRuleEngineClient : IRuleEngineClient
{
    private readonly ILogger<SignalRRuleEngineClient> _logger;
    private readonly RuleEngineConnectionOptions _options;
    private HubConnection? _connection;

    /// <summary>
    /// 客户端是否已连接
    /// </summary>
    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    /// <summary>
    /// 格口分配通知事件
    /// </summary>
    public event EventHandler<ChuteAssignmentNotificationEventArgs>? ChuteAssignmentReceived;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="options">连接配置</param>
    public SignalRRuleEngineClient(
        ILogger<SignalRRuleEngineClient> logger,
        RuleEngineConnectionOptions options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? throw new ArgumentNullException(nameof(options));

        if (string.IsNullOrWhiteSpace(_options.SignalRHub))
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
        _connection = new HubConnectionBuilder()
            .WithUrl(_options.SignalRHub!)
            .WithAutomaticReconnect()
            .Build();

        // 注册格口分配推送的处理程序
        _connection.On<ChuteAssignmentNotificationEventArgs>(
            "ReceiveChuteAssignment",
            OnChuteAssignmentReceived);

        _connection.Closed += async (error) =>
        {
            _logger.LogWarning(error, "SignalR连接已关闭");
            if (_options.EnableAutoReconnect)
            {
                await Task.Delay(_options.RetryDelayMs);
                await ConnectAsync();
            }
        };

        _connection.Reconnecting += (error) =>
        {
            _logger.LogWarning(error, "SignalR正在重新连接...");
            return Task.CompletedTask;
        };

        _connection.Reconnected += (connectionId) =>
        {
            _logger.LogInformation("SignalR重新连接成功，连接ID: {ConnectionId}", connectionId);
            return Task.CompletedTask;
        };
    }

    /// <summary>
    /// 处理接收到的格口分配通知
    /// </summary>
    private void OnChuteAssignmentReceived(ChuteAssignmentNotificationEventArgs notification)
    {
        _logger.LogInformation(
            "收到包裹 {ParcelId} 的格口分配: {ChuteNumber}",
            notification.ParcelId,
            notification.ChuteNumber);

        ChuteAssignmentReceived?.Invoke(this, notification);
    }

    /// <summary>
    /// 连接到RuleEngine
    /// </summary>
    public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (IsConnected)
        {
            return true;
        }

        try
        {
            _logger.LogInformation("正在连接到RuleEngine SignalR Hub: {HubUrl}...", _options.SignalRHub);
            
            await _connection!.StartAsync(cancellationToken);
            
            _logger.LogInformation("成功连接到RuleEngine SignalR Hub");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "连接到RuleEngine SignalR Hub失败");
            return false;
        }
    }

    /// <summary>
    /// 断开与RuleEngine的连接
    /// </summary>
    public async Task DisconnectAsync()
    {
        try
        {
            if (_connection != null)
            {
                await _connection.StopAsync();
                _logger.LogInformation("已断开与RuleEngine SignalR Hub的连接");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "断开连接时发生异常");
        }
    }

    /// <summary>
    /// 通知RuleEngine包裹已到达
    /// </summary>
    public async Task<bool> NotifyParcelDetectedAsync(
        string parcelId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(parcelId))
        {
            throw new ArgumentException("包裹ID不能为空", nameof(parcelId));
        }

        // 尝试连接（如果未连接）
        if (!IsConnected)
        {
            var connected = await ConnectAsync(cancellationToken);
            if (!connected)
            {
                _logger.LogError("无法连接到RuleEngine SignalR Hub，无法发送包裹检测通知");
                return false;
            }
        }

        try
        {
            _logger.LogDebug("向RuleEngine发送包裹检测通知: {ParcelId}", parcelId);

            var notification = new ParcelDetectionNotification { ParcelId = parcelId };
            
            // 调用Hub方法通知包裹到达，不等待响应
            await _connection!.InvokeAsync(
                "NotifyParcelDetected",
                notification,
                cancellationToken);

            _logger.LogInformation("成功发送包裹检测通知: {ParcelId}", parcelId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发送包裹检测通知失败: {ParcelId}", parcelId);
            return false;
        }
    }

    /// <summary>
    /// 请求包裹的格口号（已废弃，保留用于兼容性）
    /// </summary>
    [Obsolete("使用NotifyParcelDetectedAsync配合ChuteAssignmentReceived事件代替")]
    public async Task<ChuteAssignmentResponse> RequestChuteAssignmentAsync(
        string parcelId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(parcelId))
        {
            throw new ArgumentException("包裹ID不能为空", nameof(parcelId));
        }

        // 尝试连接（如果未连接）
        if (!IsConnected)
        {
            var connected = await ConnectAsync(cancellationToken);
            if (!connected)
            {
                return new ChuteAssignmentResponse
                {
                    ParcelId = parcelId,
                    ChuteNumber = WellKnownChuteIds.Exception,
                    IsSuccess = false,
                    ErrorMessage = "无法连接到RuleEngine SignalR Hub"
                };
            }
        }

        var retryCount = 0;
        Exception? lastException = null;

        while (retryCount <= _options.RetryCount)
        {
            try
            {
                _logger.LogDebug("向RuleEngine请求包裹 {ParcelId} 的格口号（第{Retry}次尝试）", parcelId, retryCount + 1);

                // 调用Hub方法获取格口号
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(_options.TimeoutMs);

                var request = new ChuteAssignmentRequest { ParcelId = parcelId };
                var response = await _connection!.InvokeAsync<ChuteAssignmentResponse>(
                    "RequestChuteAssignment",
                    request,
                    cts.Token);

                _logger.LogInformation(
                    "成功获取包裹 {ParcelId} 的格口号: {ChuteNumber}",
                    parcelId,
                    response.ChuteNumber);

                return response;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                lastException = ex;
                _logger.LogWarning(ex, "请求格口号失败（第{Retry}次尝试）", retryCount + 1);

                retryCount++;
                if (retryCount <= _options.RetryCount)
                {
                    await Task.Delay(_options.RetryDelayMs, cancellationToken);
                }
            }
        }

        _logger.LogError(lastException, "请求格口号失败，已达到最大重试次数");
        return new ChuteAssignmentResponse
        {
            ParcelId = parcelId,
            ChuteNumber = WellKnownChuteIds.Exception,
            IsSuccess = false,
            ErrorMessage = $"请求失败: {lastException?.Message}"
        };
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        DisconnectAsync().GetAwaiter().GetResult();
        _connection?.DisposeAsync().GetAwaiter().GetResult();
    }
}
