using ZakYip.WheelDiverterSorter.Communication.Models;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Communication.Configuration;
using ZakYip.WheelDiverterSorter.Core.LineModel;
using ZakYip.WheelDiverterSorter.Core.LineModel.Chutes;
using ZakYip.WheelDiverterSorter.Core.Utilities;

namespace ZakYip.WheelDiverterSorter.Communication.Clients;

/// <summary>
/// 基于HTTP REST API的上游路由通信客户端
/// </summary>
/// <remarks>
/// ⚠️ 仅用于测试和调试，生产环境禁止使用
/// 原因：同步阻塞、连接开销大、性能不足
/// PR-U1: 直接实现 IUpstreamRoutingClient（通过基类）
/// </remarks>
public class HttpRuleEngineClient : RuleEngineClientBase
{
    private readonly HttpClient _httpClient;
    private bool _isConnected;

    /// <summary>
    /// 客户端是否已连接
    /// </summary>
    /// <remarks>
    /// HTTP是无状态的，但为了与 UpstreamConnectionManager 配合，需要验证端点可达性
    /// 通过 ConnectAsync 验证服务器是否可访问
    /// </remarks>
    public override bool IsConnected => _isConnected;

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
    /// <remarks>
    /// HTTP是无状态的，但此方法会验证服务器端点是否可访问
    /// 用于与 UpstreamConnectionManager 配合，确保连接状态准确
    /// </remarks>
    public override async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        try
        {
            Logger.LogInformation("正在验证HTTP RuleEngine端点可达性: {Endpoint}", Options.HttpApi);
            
            // 尝试发送一个健康检查请求来验证端点可达性
            // 使用较短的超时时间避免阻塞太久
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromMilliseconds(Math.Min(Options.TimeoutMs, 3000)));
            
            // 尝试访问根路径或健康检查端点
            var healthCheckUrl = Options.HttpApi!.TrimEnd('/');
            var response = await _httpClient.GetAsync(healthCheckUrl, cts.Token);
            
            // 即使返回404或其他状态码，只要能收到响应就说明端点可达
            _isConnected = true;
            Logger.LogInformation("HTTP RuleEngine端点验证成功: {Endpoint}", Options.HttpApi);
            return true;
        }
        catch (Exception ex)
        {
            _isConnected = false;
            Logger.LogWarning(ex, "HTTP RuleEngine端点验证失败: {Endpoint}", Options.HttpApi);
            return false;
        }
    }

    /// <summary>
    /// 断开与RuleEngine的连接
    /// </summary>
    public override Task DisconnectAsync()
    {
        ThrowIfDisposed();
        _isConnected = false;
        Logger.LogInformation("已标记HTTP RuleEngine连接为断开状态");
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

        // PR-U1: 使用新的事件触发方法（转换为 Core 的事件参数类型）
        OnChuteAssignmentReceived(
            result.ParcelId,
            result.ChuteId,
            result.ResponseTime,
            null);

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
