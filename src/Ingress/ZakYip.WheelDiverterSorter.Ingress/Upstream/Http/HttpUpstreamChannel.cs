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
            _logger?.LogDebug("发送命令到 HTTP 通道 {Name}: {RequestType}", Name, typeof(TRequest).Name);

            cts.CancelAfter(timeout);

            // 根据请求类型确定端点
            var endpoint = GetEndpointForRequest<TRequest>();
            var response = await _httpClient.PostAsJsonAsync(endpoint, request, cts.Token);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken: cts.Token);
            if (result == null)
            {
                throw new InvalidOperationException("响应为空");
            }

            sw.Stop();
            _logger?.LogDebug("HTTP 通道 {Name} 命令完成，耗时: {ElapsedMs}ms", Name, sw.ElapsedMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger?.LogError(ex, "HTTP 通道 {Name} 发送命令失败，耗时: {ElapsedMs}ms", Name, sw.ElapsedMilliseconds);
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
            _logger?.LogDebug("发送单向命令到 HTTP 通道 {Name}: {RequestType}", Name, typeof(TRequest).Name);

            var endpoint = GetEndpointForRequest<TRequest>();
            var response = await _httpClient.PostAsJsonAsync(endpoint, request, cancellationToken);
            response.EnsureSuccessStatusCode();

            _logger?.LogDebug("HTTP 通道 {Name} 单向命令发送成功", Name);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "HTTP 通道 {Name} 发送单向命令失败", Name);
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
