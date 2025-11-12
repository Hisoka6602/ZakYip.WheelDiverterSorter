using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Communication.Abstractions;
using ZakYip.WheelDiverterSorter.Communication.Configuration;
using ZakYip.WheelDiverterSorter.Core;

namespace ZakYip.WheelDiverterSorter.Communication.Clients;

/// <summary>
/// 基于TCP Socket的RuleEngine通信客户端
/// </summary>
/// <remarks>
/// 推荐生产环境使用，提供低延迟、高吞吐量的通信
/// </remarks>
public class TcpRuleEngineClient : IRuleEngineClient
{
    /// <summary>
    /// TCP接收缓冲区大小（字节）
    /// </summary>
    private const int TcpReceiveBufferSize = 8192;

    private readonly ILogger<TcpRuleEngineClient> _logger;
    private readonly RuleEngineConnectionOptions _options;
    private TcpClient? _client;
    private NetworkStream? _stream;
    private bool _isConnected;

    /// <summary>
    /// 客户端是否已连接
    /// </summary>
    public bool IsConnected => _isConnected && _client?.Connected == true;

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

        if (string.IsNullOrWhiteSpace(_options.TcpServer))
        {
            throw new ArgumentException("TCP服务器地址不能为空", nameof(options));
        }
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
            var parts = _options.TcpServer!.Split(':');
            if (parts.Length != 2 || !int.TryParse(parts[1], out var port))
            {
                _logger.LogError("无效的TCP服务器地址格式: {TcpServer}", _options.TcpServer);
                return false;
            }

            var host = parts[0];
            _logger.LogInformation("正在连接到RuleEngine TCP服务器 {Host}:{Port}...", host, port);

            _client = new TcpClient();
            await _client.ConnectAsync(host, port, cancellationToken);
            _stream = _client.GetStream();
            _isConnected = true;

            _logger.LogInformation("成功连接到RuleEngine TCP服务器");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "连接到RuleEngine TCP服务器失败");
            _isConnected = false;
            return false;
        }
    }

    /// <summary>
    /// 断开与RuleEngine的连接
    /// </summary>
    public Task DisconnectAsync()
    {
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
    /// TCP客户端使用请求/响应模型，此方法将调用RequestChuteAssignmentAsync
    /// 并通过ChuteAssignmentReceived事件返回结果
    /// </remarks>
    public async Task<bool> NotifyParcelDetectedAsync(
        string parcelId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            #pragma warning disable CS0618 // 类型或成员已过时
            var response = await RequestChuteAssignmentAsync(parcelId, cancellationToken);
            #pragma warning restore CS0618
            
            // 触发事件
            var notification = new ChuteAssignmentNotificationEventArgs
            {
                ParcelId = response.ParcelId,
                ChuteNumber = response.ChuteNumber,
                NotificationTime = response.ResponseTime
            };
            ChuteAssignmentReceived?.Invoke(this, notification);
            
            return response.IsSuccess;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "通知包裹检测失败: {ParcelId}", parcelId);
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
                    ErrorMessage = "无法连接到RuleEngine服务器"
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
                var buffer = new byte[TcpReceiveBufferSize];
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
                    "成功获取包裹 {ParcelId} 的格口号: {ChuteNumber}",
                    parcelId,
                    response.ChuteNumber);

                return response;
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
        _stream?.Dispose();
        _client?.Dispose();
    }
}
