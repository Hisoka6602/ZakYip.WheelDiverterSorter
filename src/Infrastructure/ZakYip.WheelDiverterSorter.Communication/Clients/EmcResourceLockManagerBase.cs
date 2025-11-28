using System.Collections.Concurrent;
using ZakYip.WheelDiverterSorter.Communication.Abstractions;
using ZakYip.WheelDiverterSorter.Communication.Models;
using ZakYip.WheelDiverterSorter.Core.Enums.Communication;

namespace ZakYip.WheelDiverterSorter.Communication.Clients;

/// <summary>
/// EMC资源锁管理器基类
/// 提供公共的高层业务逻辑，由子类实现具体的传输协议
/// Base class for EMC resource lock managers
/// Provides common high-level business logic, with transport-specific implementation in derived classes
/// </summary>
public abstract class EmcResourceLockManagerBase : IEmcResourceLockManager
{
    /// <summary>
    /// 待响应的请求字典 - 用于等待异步响应
    /// Pending requests dictionary - used for waiting for async responses
    /// </summary>
    protected readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> _pendingRequests = new();

    /// <inheritdoc/>
    public abstract string InstanceId { get; }

    /// <inheritdoc/>
    public abstract bool IsConnected { get; }

    /// <inheritdoc/>
    public event EventHandler<EmcLockEventArgs>? EmcLockEventReceived;

    /// <summary>
    /// 触发EMC锁事件
    /// Raise EMC lock event
    /// </summary>
    protected virtual void OnEmcLockEventReceived(EmcLockEventArgs e)
    {
        EmcLockEventReceived?.Invoke(this, e);
    }

    /// <inheritdoc/>
    public abstract Task<bool> ConnectAsync(CancellationToken cancellationToken = default);

    /// <inheritdoc/>
    public abstract Task<bool> DisconnectAsync(CancellationToken cancellationToken = default);

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

    /// <summary>
    /// 发送事件（单向，不等待响应）
    /// Send event (one-way, does not wait for response)
    /// </summary>
    /// <param name="lockEvent">EMC锁事件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否发送成功</returns>
    protected abstract Task<bool> SendEventAsync(EmcLockEvent lockEvent, CancellationToken cancellationToken);

    /// <summary>
    /// 完成待响应的请求
    /// Complete a pending request
    /// </summary>
    /// <param name="eventId">事件ID</param>
    /// <param name="success">是否成功</param>
    protected void CompletePendingRequest(string eventId, bool success)
    {
        if (_pendingRequests.TryRemove(eventId, out var tcs))
        {
            tcs.TrySetResult(success);
        }
    }

    /// <summary>
    /// 发送事件并等待响应
    /// Send event and wait for response
    /// </summary>
    /// <param name="lockEvent">EMC锁事件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否收到确认响应</returns>
    protected async Task<bool> SendEventAndWaitForResponseAsync(EmcLockEvent lockEvent, CancellationToken cancellationToken)
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
                // Timeout occurred
                return false;
            }
            finally
            {
                _pendingRequests.TryRemove(lockEvent.EventId, out _);
            }
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <inheritdoc/>
    public abstract void Dispose();
}
