using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using ZakYip.WheelDiverterSorter.Communication.Abstractions;
using ZakYip.WheelDiverterSorter.Communication.Configuration;
using ZakYip.WheelDiverterSorter.Communication.Models;

namespace ZakYip.WheelDiverterSorter.Communication.Clients;

/// <summary>
/// 基于SignalR的EMC资源锁管理器实现
/// </summary>
public class SignalREmcResourceLockManager : IEmcResourceLockManager
{
    private readonly ILogger<SignalREmcResourceLockManager> _logger;
    private readonly EmcLockOptions _options;
    private HubConnection? _hubConnection;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> _pendingRequests = new();
    private bool _disposed;

    /// <inheritdoc/>
    public string InstanceId { get; }

    /// <inheritdoc/>
    public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;

    /// <inheritdoc/>
    public event EventHandler<EmcLockEventArgs>? EmcLockEventReceived;

    /// <summary>
    /// 构造函数
    /// </summary>
    public SignalREmcResourceLockManager(
        ILogger<SignalREmcResourceLockManager> logger,
        IOptions<EmcLockOptions> options)
    {
        _logger = logger;
        _options = options.Value;
        InstanceId = _options.InstanceId ?? Guid.NewGuid().ToString();
    }

    /// <inheritdoc/>
    public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (IsConnected)
            {
                _logger.LogWarning("已连接到EMC锁服务，无需重复连接");
                return true;
            }

            _logger.LogInformation("正在连接到EMC锁服务: {HubUrl}", _options.SignalRHubUrl);

            _hubConnection = new HubConnectionBuilder()
                .WithUrl(_options.SignalRHubUrl)
                .WithAutomaticReconnect()
                .Build();

            // 注册接收事件的处理器
            _hubConnection.On<EmcLockEvent>("ReceiveEmcLockEvent", (lockEvent) =>
            {
                HandleReceivedEvent(lockEvent);
            });

            await _hubConnection.StartAsync(cancellationToken);

            // 注册实例
            await _hubConnection.InvokeAsync("RegisterInstance", InstanceId, cancellationToken);

            _logger.LogInformation("已连接到EMC锁服务: {HubUrl}, 实例ID: {InstanceId}", 
                _options.SignalRHubUrl, InstanceId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "连接到EMC锁服务失败");
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> DisconnectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!IsConnected || _hubConnection == null)
            {
                return true;
            }

            _logger.LogInformation("正在断开与EMC锁服务的连接");

            // 取消注册实例
            await _hubConnection.InvokeAsync("UnregisterInstance", InstanceId, cancellationToken);
            await _hubConnection.StopAsync(cancellationToken);

            _logger.LogInformation("已断开与EMC锁服务的连接");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "断开EMC锁服务连接失败");
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> RequestLockAsync(ushort cardNo, int timeoutMs = 5000, CancellationToken cancellationToken = default)
    {
        var lockEvent = new EmcLockEvent
        {
            InstanceId = InstanceId,
            NotificationType = EmcLockNotificationType.RequestLock,
            CardNo = cardNo,
            TimeoutMs = timeoutMs
        };

        return await SendEventAndWaitForResponseAsync(lockEvent, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> ReleaseLockAsync(ushort cardNo, CancellationToken cancellationToken = default)
    {
        var lockEvent = new EmcLockEvent
        {
            InstanceId = InstanceId,
            NotificationType = EmcLockNotificationType.ReleaseLock,
            CardNo = cardNo
        };

        return await SendEventAsync(lockEvent, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> NotifyColdResetAsync(ushort cardNo, int timeoutMs = 5000, CancellationToken cancellationToken = default)
    {
        var lockEvent = new EmcLockEvent
        {
            InstanceId = InstanceId,
            NotificationType = EmcLockNotificationType.ColdReset,
            CardNo = cardNo,
            TimeoutMs = timeoutMs,
            Message = "冷重置即将执行，请其他实例准备重启"
        };

        return await SendEventAndWaitForResponseAsync(lockEvent, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> NotifyHotResetAsync(ushort cardNo, int timeoutMs = 5000, CancellationToken cancellationToken = default)
    {
        var lockEvent = new EmcLockEvent
        {
            InstanceId = InstanceId,
            NotificationType = EmcLockNotificationType.HotReset,
            CardNo = cardNo,
            TimeoutMs = timeoutMs,
            Message = "热重置即将执行，请其他实例暂停使用EMC"
        };

        return await SendEventAndWaitForResponseAsync(lockEvent, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> SendAcknowledgeAsync(string eventId, ushort cardNo, CancellationToken cancellationToken = default)
    {
        var lockEvent = new EmcLockEvent
        {
            EventId = eventId,
            InstanceId = InstanceId,
            NotificationType = EmcLockNotificationType.Acknowledge,
            CardNo = cardNo
        };

        return await SendEventAsync(lockEvent, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> SendReadyAsync(string eventId, ushort cardNo, CancellationToken cancellationToken = default)
    {
        var lockEvent = new EmcLockEvent
        {
            EventId = eventId,
            InstanceId = InstanceId,
            NotificationType = EmcLockNotificationType.Ready,
            CardNo = cardNo,
            Message = "实例已停止使用EMC，可以执行重置"
        };

        return await SendEventAsync(lockEvent, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> NotifyResetCompleteAsync(ushort cardNo, CancellationToken cancellationToken = default)
    {
        var lockEvent = new EmcLockEvent
        {
            InstanceId = InstanceId,
            NotificationType = EmcLockNotificationType.ResetComplete,
            CardNo = cardNo,
            Message = "重置操作已完成，其他实例可以恢复使用EMC"
        };

        return await SendEventAsync(lockEvent, cancellationToken);
    }

    private async Task<bool> SendEventAsync(EmcLockEvent lockEvent, CancellationToken cancellationToken)
    {
        try
        {
            if (!IsConnected || _hubConnection == null)
            {
                _logger.LogError("未连接到EMC锁服务");
                return false;
            }

            await _hubConnection.InvokeAsync("SendEmcLockEvent", lockEvent, cancellationToken);

            _logger.LogDebug("已发送EMC锁事件: {NotificationType}, EventId: {EventId}", 
                lockEvent.NotificationType, lockEvent.EventId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发送EMC锁事件失败");
            return false;
        }
    }

    private async Task<bool> SendEventAndWaitForResponseAsync(EmcLockEvent lockEvent, CancellationToken cancellationToken)
    {
        try
        {
            var tcs = new TaskCompletionSource<bool>();
            _pendingRequests.TryAdd(lockEvent.EventId, tcs);

            if (!await SendEventAsync(lockEvent, cancellationToken))
            {
                _pendingRequests.TryRemove(lockEvent.EventId, out _);
                return false;
            }

            // 等待响应或超时
            using var timeoutCts = new CancellationTokenSource(lockEvent.TimeoutMs);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            try
            {
                return await tcs.Task.WaitAsync(linkedCts.Token);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("等待EMC锁响应超时，EventId: {EventId}", lockEvent.EventId);
                return false;
            }
            finally
            {
                _pendingRequests.TryRemove(lockEvent.EventId, out _);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发送EMC锁事件并等待响应失败");
            return false;
        }
    }

    private void HandleReceivedEvent(EmcLockEvent lockEvent)
    {
        try
        {
            _logger.LogDebug("收到EMC锁事件: {NotificationType}, EventId: {EventId}, 来自: {InstanceId}", 
                lockEvent.NotificationType, lockEvent.EventId, lockEvent.InstanceId);

            // 处理来自其他实例的锁请求和通知
            if (lockEvent.InstanceId != InstanceId)
            {
                // 触发事件，通知上层应用
                EmcLockEventReceived?.Invoke(this, new EmcLockEventArgs(lockEvent));

                // 自动响应某些类型的请求
                if (lockEvent.NotificationType == EmcLockNotificationType.RequestLock ||
                    lockEvent.NotificationType == EmcLockNotificationType.ColdReset ||
                    lockEvent.NotificationType == EmcLockNotificationType.HotReset)
                {
                    // 发送确认消息（异步执行，不阻塞）
                    _ = Task.Run(async () => 
                    {
                        await SendAcknowledgeAsync(lockEvent.EventId, lockEvent.CardNo);
                    });
                }
            }
            else
            {
                // 处理针对本实例请求的响应
                if (lockEvent.NotificationType == EmcLockNotificationType.Ready ||
                    lockEvent.NotificationType == EmcLockNotificationType.Acknowledge)
                {
                    // 找到对应的待处理请求并完成
                    if (_pendingRequests.TryGetValue(lockEvent.EventId, out var tcs))
                    {
                        tcs.TrySetResult(true);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理EMC锁事件失败");
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _hubConnection?.DisposeAsync().GetAwaiter().GetResult();

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
