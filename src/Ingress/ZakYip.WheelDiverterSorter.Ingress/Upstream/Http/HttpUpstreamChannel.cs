using System.Diagnostics;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Ingress.Upstream.Configuration;

namespace ZakYip.WheelDiverterSorter.Ingress.Upstream.Http;

/// <summary>
/// HTTP 上游通道实现
/// </summary>
public class HttpUpstreamChannel : IUpstreamChannel, IUpstreamCommandSender
{
    private readonly HttpClient _httpClient;
    private readonly UpstreamChannelConfig _config;
    private readonly ILogger<HttpUpstreamChannel>? _logger;
    private bool _disposed;

    public HttpUpstreamChannel(
        HttpClient httpClient,
        UpstreamChannelConfig config,
        ILogger<HttpUpstreamChannel>? logger = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger;

        if (!string.IsNullOrEmpty(_config.Endpoint))
        {
            _httpClient.BaseAddress = new Uri(_config.Endpoint);
        }
        _httpClient.Timeout = TimeSpan.FromMilliseconds(_config.TimeoutMs);
    }

    public string Name => _config.Name;

    public string ChannelType => "HTTP";

    public bool IsConnected { get; private set; }

    public Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("HTTP 通道 {Name} 连接就绪", Name);
        IsConnected = true;
        return Task.CompletedTask;
    }

    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("HTTP 通道 {Name} 断开连接", Name);
        IsConnected = false;
        return Task.CompletedTask;
    }

    public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(_config.Endpoint))
            {
                return false;
            }

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(5));

            var response = await _httpClient.GetAsync("/health", cts.Token);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "HTTP 通道 {Name} 健康检查失败", Name);
            return false;
        }
    }

    public async Task<TResponse> SendCommandAsync<TRequest, TResponse>(
        TRequest request,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
        where TRequest : class
        where TResponse : class
    {
        var sw = Stopwatch.StartNew();
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeout);

            // 根据请求类型确定端点
            var endpoint = GetEndpointForRequest<TRequest>();
            
            // 序列化请求内容用于日志记录
            var requestJson = System.Text.Json.JsonSerializer.Serialize(request);
            
            // 记录发送的完整请求内容
            _logger?.LogInformation(
                "[上游通信-发送] HTTP通道 {ChannelName} 发送命令 | Endpoint={Endpoint} | RequestType={RequestType} | 请求内容={RequestContent}",
                Name,
                endpoint,
                typeof(TRequest).Name,
                requestJson);

            var response = await _httpClient.PostAsJsonAsync(endpoint, request, cts.Token);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken: cts.Token);
            if (result == null)
            {
                throw new InvalidOperationException("响应为空");
            }

            sw.Stop();
            
            // 序列化响应内容用于日志记录
            var responseJson = System.Text.Json.JsonSerializer.Serialize(result);
            
            // 记录接收到的完整响应内容
            _logger?.LogInformation(
                "[上游通信-接收] HTTP通道 {ChannelName} 收到响应 | Endpoint={Endpoint} | ResponseType={ResponseType} | 耗时={ElapsedMs}ms | 响应内容={ResponseContent}",
                Name,
                endpoint,
                typeof(TResponse).Name,
                sw.ElapsedMilliseconds,
                responseJson);

            return result;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger?.LogError(
                ex,
                "[上游通信-发送] HTTP通道 {ChannelName} 发送命令失败 | RequestType={RequestType} | 耗时={ElapsedMs}ms",
                Name,
                typeof(TRequest).Name,
                sw.ElapsedMilliseconds);
            throw;
        }
    }

    public async Task SendOneWayAsync<TRequest>(
        TRequest request,
        CancellationToken cancellationToken = default)
        where TRequest : class
    {
        try
        {
            var endpoint = GetEndpointForRequest<TRequest>();
            
            // 序列化请求内容用于日志记录
            var requestJson = System.Text.Json.JsonSerializer.Serialize(request);
            
            // 记录发送的完整请求内容
            _logger?.LogInformation(
                "[上游通信-发送] HTTP通道 {ChannelName} 发送单向命令 | Endpoint={Endpoint} | RequestType={RequestType} | 请求内容={RequestContent}",
                Name,
                endpoint,
                typeof(TRequest).Name,
                requestJson);

            var response = await _httpClient.PostAsJsonAsync(endpoint, request, cancellationToken);
            response.EnsureSuccessStatusCode();

            _logger?.LogInformation(
                "[上游通信-发送完成] HTTP通道 {ChannelName} 单向命令发送成功 | Endpoint={Endpoint} | StatusCode={StatusCode}",
                Name,
                endpoint,
                response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger?.LogError(
                ex,
                "[上游通信-发送] HTTP通道 {ChannelName} 发送单向命令失败 | RequestType={RequestType}",
                Name,
                typeof(TRequest).Name);
            throw;
        }
    }

    private string GetEndpointForRequest<TRequest>()
    {
        var typeName = typeof(TRequest).Name;
        return typeName switch
        {
            "AssignChuteRequest" => "/api/chute/assign",
            "CreateParcelRequest" => "/api/parcel/create",
            _ => $"/api/{typeName.ToLowerInvariant()}"
        };
    }

    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        IsConnected = false;

        GC.SuppressFinalize(this);
    }
}
