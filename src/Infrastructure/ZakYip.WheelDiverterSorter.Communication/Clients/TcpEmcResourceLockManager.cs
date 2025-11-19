using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using ZakYip.WheelDiverterSorter.Communication.Abstractions;
using ZakYip.WheelDiverterSorter.Communication.Configuration;
using ZakYip.WheelDiverterSorter.Communication.Models;

namespace ZakYip.WheelDiverterSorter.Communication.Clients;

/// <summary>
/// 基于TCP的EMC资源锁管理器实现
/// </summary>
public class TcpEmcResourceLockManager : IEmcResourceLockManager
{
    private readonly ILogger<TcpEmcResourceLockManager> _logger;
    private readonly EmcLockOptions _options;
    private TcpClient? _tcpClient;
    private NetworkStream? _stream;
    private CancellationTokenSource? _receiveLoopCts;
    private Task? _receiveLoopTask;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> _pendingRequests = new();
    private bool _disposed;

    /// <inheritdoc/>
    public string InstanceId { get; }

    /// <inheritdoc/>
    public bool IsConnected => _tcpClient?.Connected ?? false;

    /// <inheritdoc/>
    public event EventHandler<EmcLockEventArgs>? EmcLockEventReceived;

    /// <summary>
    /// 构造函数
    /// </summary>
    public TcpEmcResourceLockManager(
        ILogger<TcpEmcResourceLockManager> logger,
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

            _logger.LogInformation("正在连接到EMC锁服务: {Server}", _options.TcpServer);

            var parts = _options.TcpServer.Split(':');
            if (parts.Length != 2 || !int.TryParse(parts[1], out var port))
            {
                _logger.LogError("无效的TCP服务器地址: {Server}", _options.TcpServer);
                return false;
            }

            _tcpClient = new TcpClient();
            await _tcpClient.ConnectAsync(parts[0], port, cancellationToken);
            _stream = _tcpClient.GetStream();

            // 启动接收循环
            _receiveLoopCts = new CancellationTokenSource();
            _receiveLoopTask = Task.Run(() => ReceiveLoopAsync(_receiveLoopCts.Token), _receiveLoopCts.Token);

            _logger.LogInformation("已连接到EMC锁服务: {Server}, 实例ID: {InstanceId}", _options.TcpServer, InstanceId);
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
            if (!IsConnected)
            {
                return true;
            }

            _logger.LogInformation("正在断开与EMC锁服务的连接");

            // 停止接收循环
            _receiveLoopCts?.Cancel();
            if (_receiveLoopTask != null)
            {
                await _receiveLoopTask;
            }

            _stream?.Close();
            _tcpClient?.Close();

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
            if (!IsConnected || _stream == null)
            {
                _logger.LogError("未连接到EMC锁服务");
                return false;
            }

            var json = JsonSerializer.Serialize(lockEvent);
            var bytes = Encoding.UTF8.GetBytes(json + "\n");

            await _stream.WriteAsync(bytes, cancellationToken);
            await _stream.FlushAsync(cancellationToken);

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

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        var messageBuffer = new StringBuilder();

        try
        {
            while (!cancellationToken.IsCancellationRequested && IsConnected && _stream != null)
            {
                var bytesRead = await _stream.ReadAsync(buffer, cancellationToken);
                if (bytesRead == 0)
                {
                    _logger.LogWarning("EMC锁服务连接已关闭");
                    break;
                }

                var receivedData = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                messageBuffer.Append(receivedData);

                // 处理完整的消息（以换行符分隔）
                var messages = messageBuffer.ToString().Split('\n');
                for (int i = 0; i < messages.Length - 1; i++)
                {
                    await ProcessReceivedMessageAsync(messages[i], cancellationToken);
                }

                // 保留未完成的消息
                messageBuffer.Clear();
                if (messages.Length > 0 && !messages[^1].EndsWith('\n'))
                {
                    messageBuffer.Append(messages[^1]);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("EMC锁接收循环已取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "EMC锁接收循环异常");
        }
    }

    private async Task ProcessReceivedMessageAsync(string message, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            var lockEvent = JsonSerializer.Deserialize<EmcLockEvent>(message);
            if (lockEvent == null)
            {
                _logger.LogWarning("无法反序列化EMC锁事件: {Message}", message);
                return;
            }

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
                    // 发送确认消息
                    await SendAcknowledgeAsync(lockEvent.EventId, lockEvent.CardNo, cancellationToken);
                    
                    // 可以在这里添加额外的逻辑，比如停止使用EMC硬件
                    // 然后发送Ready消息
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
            _logger.LogError(ex, "处理EMC锁消息失败: {Message}", message);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _receiveLoopCts?.Cancel();
        _receiveLoopCts?.Dispose();
        _stream?.Dispose();
        _tcpClient?.Dispose();

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
