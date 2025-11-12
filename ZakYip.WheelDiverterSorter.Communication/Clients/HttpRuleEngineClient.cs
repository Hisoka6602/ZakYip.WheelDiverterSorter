using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Communication.Abstractions;
using ZakYip.WheelDiverterSorter.Communication.Configuration;

namespace ZakYip.WheelDiverterSorter.Communication.Clients;

/// <summary>
/// 基于HTTP REST API的RuleEngine通信客户端
/// </summary>
/// <remarks>
/// ⚠️ 仅用于测试和调试，生产环境禁止使用
/// 原因：同步阻塞、连接开销大、性能不足
/// </remarks>
public class HttpRuleEngineClient : IRuleEngineClient
{
    private readonly ILogger<HttpRuleEngineClient> _logger;
    private readonly RuleEngineConnectionOptions _options;
    private readonly HttpClient _httpClient;

    /// <summary>
    /// 客户端是否已连接
    /// </summary>
    public bool IsConnected => true; // HTTP是无状态的

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="options">连接配置</param>
    public HttpRuleEngineClient(
        ILogger<HttpRuleEngineClient> logger,
        RuleEngineConnectionOptions options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? throw new ArgumentNullException(nameof(options));

        if (string.IsNullOrWhiteSpace(_options.HttpApi))
        {
            throw new ArgumentException("HTTP API URL不能为空", nameof(options));
        }

        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMilliseconds(_options.TimeoutMs)
        };

        _logger.LogWarning("⚠️ 使用HTTP客户端，此模式仅用于测试，生产环境禁用");
    }

    /// <summary>
    /// 连接到RuleEngine
    /// </summary>
    public Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        // HTTP是无状态的，不需要连接
        return Task.FromResult(true);
    }

    /// <summary>
    /// 断开与RuleEngine的连接
    /// </summary>
    public Task DisconnectAsync()
    {
        // HTTP是无状态的，不需要断开
        return Task.CompletedTask;
    }

    /// <summary>
    /// 请求包裹的格口号
    /// </summary>
    public async Task<ChuteAssignmentResponse> RequestChuteAssignmentAsync(
        string parcelId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(parcelId))
        {
            throw new ArgumentException("包裹ID不能为空", nameof(parcelId));
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
                var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

                // 发送HTTP POST请求
                var response = await _httpClient.PostAsync(_options.HttpApi, content, cancellationToken);
                response.EnsureSuccessStatusCode();

                // 读取响应
                var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
                var result = JsonSerializer.Deserialize<ChuteAssignmentResponse>(responseJson);

                if (result == null)
                {
                    throw new InvalidOperationException("响应反序列化失败");
                }

                _logger.LogInformation(
                    "成功获取包裹 {ParcelId} 的格口号: {ChuteNumber}",
                    parcelId,
                    result.ChuteNumber);

                return result;
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
            ChuteNumber = "CHUTE_EXCEPTION",
            IsSuccess = false,
            ErrorMessage = $"请求失败: {lastException?.Message}"
        };
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
