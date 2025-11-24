using ZakYip.WheelDiverterSorter.Communication.Models;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Communication.Abstractions;
using ZakYip.WheelDiverterSorter.Communication.Configuration;
using ZakYip.WheelDiverterSorter.Core.LineModel;
using ZakYip.WheelDiverterSorter.Core.LineModel.Chutes;
using ZakYip.WheelDiverterSorter.Core.Utilities;

namespace ZakYip.WheelDiverterSorter.Communication.Clients;

/// <summary>
/// 基于TCP Socket的RuleEngine通信客户端
/// </summary>
/// <remarks>
/// 推荐生产环境使用，提供低延迟、高吞吐量的通信
/// </remarks>
public class TcpRuleEngineClient : RuleEngineClientBase
{
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private TcpClient? _client;
    private NetworkStream? _stream;
    private bool _isConnected;

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

        // 验证重试次数
        if (options.RetryCount < 0)
        {
            throw new ArgumentException($"重试次数不能为负数: {options.RetryCount}", nameof(options));
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
    /// TCP客户端使用请求/响应模型的内部实现来模拟推送模型
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

        return await ExecuteWithRetryAsync(
            async ct => await SendRequestAsync(parcelId, ct),
            $"向RuleEngine请求包裹 {parcelId} 的格口号",
            cancellationToken);
    }

    private async Task<bool> SendRequestAsync(long parcelId, CancellationToken cancellationToken)
    {
        // 构造请求
        var request = new ChuteAssignmentRequest 
        { 
            ParcelId = parcelId,
            RequestTime = SystemClock.LocalNowOffset
        };
        var requestJson = JsonSerializer.Serialize(request);
        var requestBytes = Encoding.UTF8.GetBytes(requestJson + "\n");

        // 发送请求
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(Options.TimeoutMs);

        await _stream!.WriteAsync(requestBytes, cts.Token);
        await _stream.FlushAsync(cts.Token);

        // 读取响应
        var buffer = new byte[Options.Tcp.ReceiveBufferSize];
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

        Logger.LogInformation(
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
        OnChuteAssignmentReceived(notification);

        return response.IsSuccess;
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
