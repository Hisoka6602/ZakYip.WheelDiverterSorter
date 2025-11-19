using ZakYip.WheelDiverterSorter.Communication.Models;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Communication.Abstractions;
using ZakYip.WheelDiverterSorter.Communication.Configuration;
using ZakYip.WheelDiverterSorter.Core.LineModel;
using ZakYip.WheelDiverterSorter.Core.LineModel.Chutes;

namespace ZakYip.WheelDiverterSorter.Communication.Clients;

/// <summary>
/// 基于TCP Socket的RuleEngine通信客户端
/// </summary>
/// <remarks>
/// 推荐生产环境使用，提供低延迟、高吞吐量的通信
/// </remarks>
public class TcpRuleEngineClient : IRuleEngineClient
{
    private readonly ILogger<TcpRuleEngineClient> _logger;
    private readonly RuleEngineConnectionOptions _options;
    private readonly SemaphoreSlim _connectionLock = new SemaphoreSlim(1, 1);
    private TcpClient? _client;
    private NetworkStream? _stream;
    private bool _isConnected;
    private bool _disposed;

    /// <summary>
    /// 客户端是否已连接
    /// </summary>
    public bool IsConnected => !_disposed && _isConnected && _client?.Connected == true;

    /// <summary>
    /// 格口分配通知事件
    /// </summary>
    /// <remarks>
    /// TCP客户端当前使用请求/响应模型，此事件不会触发
    /// </remarks>
    public event EventHandler<ChuteAssignmentNotificationEventArgs>? ChuteAssignmentReceived;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="options">连接配置</param>
    public TcpRuleEngineClient(
        ILogger<TcpRuleEngineClient> logger,
        RuleEngineConnectionOptions options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? throw new ArgumentNullException(nameof(options));

        // 验证 TCP 服务器地址格式
        if (string.IsNullOrWhiteSpace(_options.TcpServer))
        {
            throw new ArgumentException("TCP服务器地址不能为空", nameof(options));
        }

        // 验证地址格式：必须是 "host:port" 格式
        var parts = _options.TcpServer.Split(':');
        if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
        {
            throw new ArgumentException($"无效的TCP服务器地址格式，必须为 'host:port' 格式: {_options.TcpServer}", nameof(options));
        }

        // 验证端口号
        if (!int.TryParse(parts[1], out var port) || port <= 0 || port > 65535)
        {
            throw new ArgumentException($"无效的端口号: {parts[1]}", nameof(options));
        }

        // 验证超时时间
        if (_options.TimeoutMs <= 0)
        {
            throw new ArgumentException($"超时时间必须大于0: {_options.TimeoutMs}ms", nameof(options));
        }

        // 验证重试次数
        if (_options.RetryCount < 0)
        {
            throw new ArgumentException($"重试次数不能为负数: {_options.RetryCount}", nameof(options));
        }
    }

    /// <summary>
    /// 连接到RuleEngine
    /// </summary>
    public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(TcpRuleEngineClient));
        }

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

            var parts = _options.TcpServer!.Split(':');
            if (parts.Length != 2 || !int.TryParse(parts[1], out var port))
            {
                _logger.LogError("无效的TCP服务器地址格式: {TcpServer}", _options.TcpServer);
                return false;
            }

            var host = parts[0];
            _logger.LogInformation("正在连接到RuleEngine TCP服务器 {Host}:{Port}...", host, port);

            _client = new TcpClient
            {
                ReceiveBufferSize = _options.Tcp.ReceiveBufferSize,
                SendBufferSize = _options.Tcp.SendBufferSize,
                NoDelay = _options.Tcp.NoDelay
            };
            await _client.ConnectAsync(host, port, cancellationToken);
            _stream = _client.GetStream();
            _isConnected = true;

            // 配置KeepAlive（如果启用）
            if (_options.Tcp.KeepAliveInterval > 0)
            {
                _client.Client.SetSocketOption(
                    SocketOptionLevel.Socket,
                    SocketOptionName.KeepAlive,
                    true);
            }

            _logger.LogInformation(
                "成功连接到RuleEngine TCP服务器 (缓冲区: {Buffer}KB, NoDelay: {NoDelay})",
                _options.Tcp.ReceiveBufferSize / 1024,
                _options.Tcp.NoDelay);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "连接到RuleEngine TCP服务器失败");
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
    public Task DisconnectAsync()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(TcpRuleEngineClient));
        }

        try
        {
            _stream?.Close();
            _client?.Close();
            _isConnected = false;
            _logger.LogInformation("已断开与RuleEngine TCP服务器的连接");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "断开连接时发生异常");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 通知RuleEngine包裹已到达
    /// </summary>
    /// <remarks>
    /// TCP客户端使用请求/响应模型的内部实现来模拟推送模型
    /// </remarks>
    public async Task<bool> NotifyParcelDetectedAsync(
        long parcelId,
        CancellationToken cancellationToken = default)
    {
        if (parcelId <= 0)
        {
            throw new ArgumentException("包裹ID必须为正数", nameof(parcelId));
        }

        // 尝试连接（如果未连接）
        if (!IsConnected)
        {
            var connected = await ConnectAsync(cancellationToken);
            if (!connected)
            {
                return false;
            }
        }

        var retryCount = 0;
        Exception? lastException = null;

        while (retryCount <= _options.RetryCount)
        {
            try
            {
                _logger.LogDebug("向RuleEngine请求包裹 {ParcelId} 的格口号（第{Retry}次尝试）", parcelId, retryCount + 1);

                // 构造请求
                var request = new ChuteAssignmentRequest { ParcelId = parcelId };
                var requestJson = JsonSerializer.Serialize(request);
                var requestBytes = Encoding.UTF8.GetBytes(requestJson + "\n");

                // 发送请求
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(_options.TimeoutMs);

                await _stream!.WriteAsync(requestBytes, cts.Token);
                await _stream.FlushAsync(cts.Token);

                // 读取响应
                var buffer = new byte[_options.Tcp.ReceiveBufferSize];
                var bytesRead = await _stream.ReadAsync(buffer, cts.Token);

                if (bytesRead == 0)
                {
                    throw new IOException("服务器关闭了连接");
                }

                var responseJson = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();
                var response = JsonSerializer.Deserialize<ChuteAssignmentResponse>(responseJson);

                if (response == null)
                {
                    throw new InvalidOperationException("响应反序列化失败");
                }

                _logger.LogInformation(
                    "成功获取包裹 {ParcelId} 的格口号: {ChuteId}",
                    parcelId,
                    response.ChuteId);

                // 触发事件
                var notification = new ChuteAssignmentNotificationEventArgs
                {
                    ParcelId = response.ParcelId,
                    ChuteId = response.ChuteId,
                    NotificationTime = response.ResponseTime
                };
                ChuteAssignmentReceived?.Invoke(this, notification);

                return response.IsSuccess;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                lastException = ex;
                _logger.LogWarning(ex, "请求格口号失败（第{Retry}次尝试）", retryCount + 1);

                // 断开连接，准备重试
                await DisconnectAsync();

                retryCount++;
                if (retryCount <= _options.RetryCount)
                {
                    await Task.Delay(_options.RetryDelayMs, cancellationToken);
                    await ConnectAsync(cancellationToken);
                }
            }
        }

        _logger.LogError(lastException, "请求格口号失败，已达到最大重试次数");
        return false;
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
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
}
