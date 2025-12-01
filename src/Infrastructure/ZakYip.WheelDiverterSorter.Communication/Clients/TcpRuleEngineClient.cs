using ZakYip.WheelDiverterSorter.Core.Events.Chute;
using ZakYip.WheelDiverterSorter.Communication.Models;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Upstream;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Communication.Configuration;
using ZakYip.WheelDiverterSorter.Core.Utilities;

namespace ZakYip.WheelDiverterSorter.Communication.Clients;

/// <summary>
/// 基于TCP Socket的上游路由通信客户端
/// </summary>
/// <remarks>
/// 推荐生产环境使用，提供低延迟、高吞吐量的通信
/// PR-U1: 直接实现 IUpstreamRoutingClient（通过基类）
/// PR-UPSTREAM02: 添加落格完成通知，更新消息处理以支持新的格口分配通知格式
/// </remarks>
public class TcpRuleEngineClient : RuleEngineClientBase
{
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private TcpClient? _client;
    private NetworkStream? _stream;
    private bool _isConnected;
    private Task? _receiveTask;
    private CancellationTokenSource? _receiveCts;

    /// <summary>
    /// 客户端是否已连接
    /// </summary>
    public override bool IsConnected => _isConnected && _client?.Connected == true;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="options">连接配置</param>
    /// <param name="systemClock">系统时钟</param>
    public TcpRuleEngineClient(
        ILogger<TcpRuleEngineClient> logger,
        RuleEngineConnectionOptions options,
        ISystemClock systemClock) : base(logger, options, systemClock)
    {
        ValidateTcpOptions(options);
    }

    private static void ValidateTcpOptions(RuleEngineConnectionOptions options)
    {
        // 验证 TCP 服务器地址格式
        if (string.IsNullOrWhiteSpace(options.TcpServer))
        {
            throw new ArgumentException("TCP服务器地址不能为空", nameof(options));
        }

        // 验证地址格式：必须是 "host:port" 格式
        var parts = options.TcpServer.Split(':');
        if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
        {
            throw new ArgumentException($"无效的TCP服务器地址格式，必须为 'host:port' 格式: {options.TcpServer}", nameof(options));
        }

        // 验证端口号
        if (!int.TryParse(parts[1], out var port) || port <= 0 || port > 65535)
        {
            throw new ArgumentException($"无效的端口号: {parts[1]}", nameof(options));
        }

        // 验证超时时间
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

        // 快速检查，避免不必要的锁等待
        if (IsConnected)
        {
            return true;
        }

        // 使用锁保护连接过程，防止并发连接
        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            // 双重检查，可能在等待锁时已被其他线程连接
            if (IsConnected)
            {
                return true;
            }

            var parts = Options.TcpServer!.Split(':');
            if (parts.Length != 2 || !int.TryParse(parts[1], out var port))
            {
                Logger.LogError("无效的TCP服务器地址格式: {TcpServer}", Options.TcpServer);
                return false;
            }

            var host = parts[0];
            Logger.LogInformation("正在连接到RuleEngine TCP服务器 {Host}:{Port}...", host, port);

            _client = new TcpClient
            {
                ReceiveBufferSize = Options.Tcp.ReceiveBufferSize,
                SendBufferSize = Options.Tcp.SendBufferSize,
                NoDelay = Options.Tcp.NoDelay
            };
            await _client.ConnectAsync(host, port, cancellationToken);
            _stream = _client.GetStream();
            _isConnected = true;

            // 启动后台消息接收任务
            StartReceiveLoop();

            Logger.LogInformation(
                "成功连接到RuleEngine TCP服务器 (缓冲区: {Buffer}KB, NoDelay: {NoDelay})",
                Options.Tcp.ReceiveBufferSize / 1024,
                Options.Tcp.NoDelay);
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "连接到RuleEngine TCP服务器失败");
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
            // 停止接收循环
            StopReceiveLoop();
            
            _stream?.Close();
            _client?.Close();
            _isConnected = false;
            Logger.LogInformation("已断开与RuleEngine TCP服务器的连接");
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
    /// <remarks>
    /// TCP客户端发送通知后不等待响应，响应通过服务器推送接收（如果启用了服务器模式）
    /// 根据系统规则：发送失败只记录日志，不进行重试
    /// </remarks>
    public override async Task<bool> NotifyParcelDetectedAsync(
        long parcelId,
        CancellationToken cancellationToken = default)
    {
        ValidateParcelId(parcelId);

        // 尝试连接（如果未连接）
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
            // 根据系统规则：发送失败只记录日志，不重试
            Logger.LogError(
                ex,
                "向RuleEngine发送包裹 {ParcelId} 通知失败: {Message}",
                parcelId,
                ex.Message);
            return false;
        }
    }

    /// <summary>
    /// 发送包裹检测通知（不等待响应）
    /// </summary>
    private async Task SendNotificationAsync(long parcelId, CancellationToken cancellationToken)
    {
        // 构造通知
        var notification = new ParcelDetectionNotification 
        { 
            ParcelId = parcelId,
            DetectionTime = SystemClock.LocalNowOffset
        };
        var notificationJson = JsonSerializer.Serialize(notification);
        var notificationBytes = Encoding.UTF8.GetBytes(notificationJson + "\n");

        // 记录发送的完整消息内容
        Logger.LogInformation(
            "[上游通信-发送] TCP通道发送包裹检测通知 | ParcelId={ParcelId} | 消息内容={MessageContent}",
            parcelId,
            notificationJson);

        // 发送通知（不等待响应）
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(Options.TimeoutMs);

        await _stream!.WriteAsync(notificationBytes, cts.Token);
        await _stream.FlushAsync(cts.Token);

        Logger.LogInformation(
            "[上游通信-发送完成] TCP通道成功发送包裹检测通知 | ParcelId={ParcelId} | 字节数={ByteCount}",
            parcelId,
            notificationBytes.Length);
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
            return false;
        }

        try
        {
            await SendSortingCompletedNotificationAsync(notification, cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            // 发送失败只记录日志，不重试
            Logger.LogError(
                ex,
                "[上游通信-发送] TCP通道发送落格完成通知失败 | ParcelId={ParcelId} | ChuteId={ChuteId}",
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
        var notificationBytes = Encoding.UTF8.GetBytes(notificationJson + "\n");

        Logger.LogInformation(
            "[上游通信-发送] TCP通道发送落格完成通知 | ParcelId={ParcelId} | ChuteId={ChuteId} | IsSuccess={IsSuccess} | 消息内容={MessageContent}",
            notification.ParcelId,
            notification.ActualChuteId,
            notification.IsSuccess,
            notificationJson);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(Options.TimeoutMs);

        await _stream!.WriteAsync(notificationBytes, cts.Token);
        await _stream.FlushAsync(cts.Token);

        Logger.LogInformation(
            "[上游通信-发送完成] TCP通道成功发送落格完成通知 | ParcelId={ParcelId} | 字节数={ByteCount}",
            notification.ParcelId,
            notificationBytes.Length);
    }

    /// <summary>
    /// 启动后台消息接收循环
    /// </summary>
    private void StartReceiveLoop()
    {
        // 如果已经有接收任务在运行，先停止
        StopReceiveLoop();

        _receiveCts = new CancellationTokenSource();
        var cancellationToken = _receiveCts.Token;

        _receiveTask = Task.Run(async () =>
        {
            try
            {
                await ReceiveLoopAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                Logger.LogDebug("TCP消息接收循环已取消");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "TCP消息接收循环发生异常");
            }
        }, cancellationToken);
    }

    /// <summary>
    /// 停止后台消息接收循环
    /// </summary>
    private void StopReceiveLoop()
    {
        try
        {
            _receiveCts?.Cancel();
            // 等待任务完成，但不阻塞太久
            _receiveTask?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException ex) when (ex.InnerExceptions.All(e => e is OperationCanceledException))
        {
            // 预期的取消异常，忽略
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "停止接收循环时发生异常");
        }
        finally
        {
            _receiveCts?.Dispose();
            _receiveCts = null;
            _receiveTask = null;
        }
    }

    /// <summary>
    /// 消息接收循环（后台任务）
    /// </summary>
    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[Options.Tcp.ReceiveBufferSize];
        var messageBuffer = new StringBuilder();

        Logger.LogDebug("TCP消息接收循环已启动");

        while (!cancellationToken.IsCancellationRequested && IsConnected)
        {
            try
            {
                // 添加空检查以避免竞态条件
                var stream = _stream;
                if (stream == null || !IsConnected)
                {
                    Logger.LogDebug("Stream is null or connection is lost, exiting receive loop");
                    break;
                }

                // 读取数据
                var bytesRead = await stream.ReadAsync(buffer, cancellationToken);

                if (bytesRead == 0)
                {
                    // 连接已关闭
                    Logger.LogWarning("TCP连接已被服务器关闭");
                    _isConnected = false;
                    break;
                }

                // 解析消息
                var receivedText = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                messageBuffer.Append(receivedText);

                // 尝试处理缓冲区中的所有完整消息
                ProcessMessagesInBuffer(messageBuffer);
            }
            catch (OperationCanceledException)
            {
                throw; // 传播取消异常
            }
            catch (IOException ex)
            {
                Logger.LogWarning(ex, "TCP读取数据时发生IO异常");
                _isConnected = false;
                break;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "TCP接收消息时发生异常");
            }
        }

        Logger.LogDebug("TCP消息接收循环已结束");
    }

    /// <summary>
    /// 处理缓冲区中的消息
    /// </summary>
    /// <remarks>
    /// 支持两种消息格式：
    /// 1. 以换行符分隔的多条消息
    /// 2. 多条连续的JSON消息（无换行符）
    /// </remarks>
    private void ProcessMessagesInBuffer(StringBuilder messageBuffer)
    {
        var bufferContent = messageBuffer.ToString();
        
        // 如果包含换行符，按换行符分割处理
        if (bufferContent.Contains('\n'))
        {
            var messages = bufferContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            
            // 如果最后一个字符不是换行符，保留最后一条不完整的消息
            if (!bufferContent.EndsWith('\n') && messages.Length > 0)
            {
                messageBuffer.Clear();
                messageBuffer.Append(messages[^1]);
                messages = messages[..^1];
            }
            else
            {
                messageBuffer.Clear();
            }

            // 处理完整消息
            foreach (var message in messages)
            {
                if (!string.IsNullOrWhiteSpace(message))
                {
                    ProcessReceivedMessage(message.Trim());
                }
            }
        }
        else
        {
            // 尝试从缓冲区中提取所有完整的JSON对象
            var extractedMessages = ExtractJsonMessages(bufferContent);
            
            if (extractedMessages.Count > 0)
            {
                // 处理所有提取的消息
                foreach (var message in extractedMessages)
                {
                    ProcessReceivedMessage(message);
                }
                
                // 清除已处理的部分，保留未完成的部分
                var processedLength = extractedMessages.Sum(m => m.Length);
                if (processedLength < bufferContent.Length)
                {
                    messageBuffer.Remove(0, processedLength);
                }
                else
                {
                    messageBuffer.Clear();
                }
            }
            // 如果没有提取到完整的JSON，保持缓冲区不变，等待更多数据
        }
    }

    /// <summary>
    /// 从字符串中提取所有完整的JSON对象
    /// </summary>
    /// <remarks>
    /// 处理可能连续的多个JSON对象，如: {"a":1}{"b":2}{"c":3}
    /// </remarks>
    private static List<string> ExtractJsonMessages(string text)
    {
        var messages = new List<string>();
        if (string.IsNullOrWhiteSpace(text))
        {
            return messages;
        }

        var startIndex = 0;
        while (startIndex < text.Length)
        {
            // 跳过空白字符
            while (startIndex < text.Length && char.IsWhiteSpace(text[startIndex]))
            {
                startIndex++;
            }

            if (startIndex >= text.Length)
            {
                break;
            }

            // 查找JSON对象的开始
            if (text[startIndex] != '{')
            {
                break; // 不是有效的JSON开始
            }

            // 查找匹配的结束括号
            var braceCount = 0;
            var endIndex = startIndex;
            var inString = false;
            var escapeNext = false;

            for (int i = startIndex; i < text.Length; i++)
            {
                var c = text[i];

                if (escapeNext)
                {
                    escapeNext = false;
                    continue;
                }

                if (c == '\\')
                {
                    escapeNext = true;
                    continue;
                }

                if (c == '"')
                {
                    inString = !inString;
                    continue;
                }

                if (!inString)
                {
                    if (c == '{')
                    {
                        braceCount++;
                    }
                    else if (c == '}')
                    {
                        braceCount--;
                        if (braceCount == 0)
                        {
                            endIndex = i;
                            break;
                        }
                    }
                }
            }

            // 如果找到完整的JSON对象
            if (braceCount == 0 && endIndex > startIndex)
            {
                var jsonMessage = text.Substring(startIndex, endIndex - startIndex + 1);
                messages.Add(jsonMessage);
                startIndex = endIndex + 1;
            }
            else
            {
                // 没有找到完整的JSON，退出循环
                break;
            }
        }

        return messages;
    }

    /// <summary>
    /// 处理接收到的消息
    /// </summary>
    /// <remarks>
    /// PR-UPSTREAM02: 更新以使用 ChuteAssignmentNotificationEventArgs 和新的 AssignedAt 字段
    /// </remarks>
    private void ProcessReceivedMessage(string messageJson)
    {
        try
        {
            // 记录接收到的完整消息内容
            Logger.LogInformation(
                "[上游通信-接收] TCP通道收到消息 | 消息内容={MessageContent}",
                messageJson);

            // 尝试解析为格口分配通知（使用内部 DTO 进行 JSON 解析）
            var notification = JsonSerializer.Deserialize<ChuteAssignmentNotificationEventArgs>(messageJson);

            if (notification != null)
            {
                Logger.LogInformation(
                    "[上游通信-接收] TCP通道收到格口分配通知 | ParcelId={ParcelId} | ChuteId={ChuteId} | 消息内容={MessageContent}",
                    notification.ParcelId,
                    notification.ChuteId,
                    messageJson);

                // PR-UPSTREAM02: 使用共享方法转换 DWS 数据
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
                    "[上游通信-接收] TCP通道无法解析消息为格口分配通知 | 消息内容={MessageContent}",
                    messageJson);
            }
        }
        catch (JsonException ex)
        {
            Logger.LogError(
                ex,
                "[上游通信-接收] TCP通道解析消息时发生JSON异常 | 消息内容={MessageContent}",
                messageJson);
        }
        catch (Exception ex)
        {
            Logger.LogError(
                ex,
                "[上游通信-接收] TCP通道处理消息时发生异常 | 消息内容={MessageContent}",
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
                StopReceiveLoop();
                _stream?.Close();
                _client?.Close();
                _isConnected = false;
            }
            catch
            {
                // 忽略dispose过程中的异常
            }

            _stream?.Dispose();
            _client?.Dispose();
            _connectionLock?.Dispose();
        }

        base.Dispose(disposing);
    }
}
