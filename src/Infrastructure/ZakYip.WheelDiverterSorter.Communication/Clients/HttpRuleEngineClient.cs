using ZakYip.WheelDiverterSorter.Communication.Models;
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
/// 基于HTTP REST API的RuleEngine通信客户端
/// </summary>
/// <remarks>
/// ⚠️ 仅用于测试和调试，生产环境禁止使用
/// 原因：同步阻塞、连接开销大、性能不足
/// </remarks>
public class HttpRuleEngineClient : RuleEngineClientBase
{
    private readonly HttpClient _httpClient;

    /// <summary>
    /// 客户端是否已连接
    /// </summary>
    public override bool IsConnected => true; // HTTP是无状态的

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="options">连接配置</param>
    /// <param name="systemClock">系统时钟</param>
    public HttpRuleEngineClient(
        ILogger<HttpRuleEngineClient> logger,
        RuleEngineConnectionOptions options,
        ISystemClock systemClock) : base(logger, options, systemClock)
    {
        if (string.IsNullOrWhiteSpace(options.HttpApi))
        {
            throw new ArgumentException("HTTP API URL不能为空", nameof(options));
        }

        var handler = new SocketsHttpHandler
        {
            MaxConnectionsPerServer = options.Http.MaxConnectionsPerServer,
            PooledConnectionIdleTimeout = TimeSpan.FromSeconds(options.Http.PooledConnectionIdleTimeout),
            PooledConnectionLifetime = options.Http.PooledConnectionLifetime > 0 
                ? TimeSpan.FromSeconds(options.Http.PooledConnectionLifetime) 
                : Timeout.InfiniteTimeSpan
        };

        _httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromMilliseconds(options.TimeoutMs)
        };

        if (options.Http.UseHttp2)
        {
            _httpClient.DefaultRequestVersion = new Version(2, 0);
        }

        Logger.LogWarning(
            "⚠️ 使用HTTP客户端（连接池: {MaxConn}, HTTP/{Version}），此模式仅用于测试，生产环境禁用",
            options.Http.MaxConnectionsPerServer,
            options.Http.UseHttp2 ? "2" : "1.1");
    }

    /// <summary>
    /// 连接到RuleEngine
    /// </summary>
    public override Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        // HTTP是无状态的，不需要连接
        return Task.FromResult(true);
    }

    /// <summary>
    /// 断开与RuleEngine的连接
    /// </summary>
    public override Task DisconnectAsync()
    {
        // HTTP是无状态的，不需要断开
        return Task.CompletedTask;
    }

    /// <summary>
    /// 通知RuleEngine包裹已到达
    /// </summary>
    /// <remarks>
    /// HTTP客户端使用请求/响应模型的内部实现来模拟推送模型
    /// </remarks>
    public override async Task<bool> NotifyParcelDetectedAsync(
        long parcelId,
        CancellationToken cancellationToken = default)
    {
        ValidateParcelId(parcelId);

        return await ExecuteWithRetryAsync(
            async ct => await SendHttpRequestAsync(parcelId, ct),
            $"向RuleEngine请求包裹 {parcelId} 的格口号",
            cancellationToken);
    }

    private async Task<bool> SendHttpRequestAsync(long parcelId, CancellationToken cancellationToken)
    {
        // 构造请求
        var request = new ChuteAssignmentRequest 
        { 
            ParcelId = parcelId,
            RequestTime = SystemClock.LocalNowOffset
        };
        var requestJson = JsonSerializer.Serialize(request);
        var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

        // 发送HTTP POST请求
        var response = await _httpClient.PostAsync(Options.HttpApi, content, cancellationToken);
        response.EnsureSuccessStatusCode();

        // 读取响应
        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = JsonSerializer.Deserialize<ChuteAssignmentResponse>(responseJson);

        if (result == null)
        {
            throw new InvalidOperationException("响应反序列化失败");
        }

        Logger.LogInformation(
            "成功获取包裹 {ParcelId} 的格口号: {ChuteId}",
            parcelId,
            result.ChuteId);

        // 触发事件
        var notification = new ChuteAssignmentNotificationEventArgs
        {
            ParcelId = result.ParcelId,
            ChuteId = result.ChuteId,
            NotificationTime = result.ResponseTime
        };
        OnChuteAssignmentReceived(notification);

        return result.IsSuccess;
    }

    /// <summary>
    /// 释放托管和非托管资源
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _httpClient.Dispose();
        }

        base.Dispose(disposing);
    }
}
