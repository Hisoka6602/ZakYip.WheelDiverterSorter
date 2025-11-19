using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Sorting.Contracts;
using ZakYip.WheelDiverterSorter.Core.Sorting.Exceptions;
using ZakYip.WheelDiverterSorter.Core.Sorting.Interfaces;
using ZakYip.WheelDiverterSorter.Communication.Abstractions;
using ZakYip.WheelDiverterSorter.Communication.Configuration;
using ZakYip.WheelDiverterSorter.Communication.Models;

namespace ZakYip.WheelDiverterSorter.Communication.Gateways;

/// <summary>
/// HTTP 协议的上游分拣网关实现
/// </summary>
/// <remarks>
/// 适配 HttpRuleEngineClient，提供协议层编解码和基础重试
/// ⚠️ 仅用于测试，生产环境禁用
/// </remarks>
public class HttpUpstreamSortingGateway : IUpstreamSortingGateway
{
    private readonly IRuleEngineClient _httpClient;
    private readonly ILogger<HttpUpstreamSortingGateway> _logger;
    private readonly RuleEngineConnectionOptions _options;

    /// <summary>
    /// 构造函数
    /// </summary>
    public HttpUpstreamSortingGateway(
        IRuleEngineClient httpClient,
        ILogger<HttpUpstreamSortingGateway> logger,
        RuleEngineConnectionOptions options)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? throw new ArgumentNullException(nameof(options));

        _logger.LogWarning("⚠️ 使用 HTTP 网关，仅用于测试，生产环境禁用");
    }

    /// <summary>
    /// 请求分拣决策
    /// </summary>
    public async Task<SortingResponse> RequestSortingAsync(
        SortingRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        try
        {
            _logger.LogDebug(
                "通过 HTTP 网关请求分拣决策: ParcelId={ParcelId}",
                request.ParcelId);

            // 使用 TaskCompletionSource 等待事件响应
            var tcs = new TaskCompletionSource<SortingResponse>();
            
            // 订阅事件处理响应
            EventHandler<ChuteAssignmentNotificationEventArgs>? handler = null;
            handler = (sender, eventArgs) =>
            {
                if (eventArgs.ParcelId == request.ParcelId)
                {
                    _httpClient.ChuteAssignmentReceived -= handler;
                    
                    var response = new SortingResponse
                    {
                        ParcelId = eventArgs.ParcelId,
                        TargetChuteId = eventArgs.ChuteId,
                        IsSuccess = true,
                        IsException = false,
                        ReasonCode = "SUCCESS",
                        ResponseTime = eventArgs.NotificationTime,
                        Source = "HttpUpstreamGateway"
                    };
                    
                    tcs.TrySetResult(response);
                }
            };
            
            _httpClient.ChuteAssignmentReceived += handler;

            try
            {
                // 发送通知
                var notified = await _httpClient.NotifyParcelDetectedAsync(
                    request.ParcelId,
                    cancellationToken);

                if (!notified)
                {
                    _httpClient.ChuteAssignmentReceived -= handler;
                    throw new UpstreamUnavailableException("发送包裹通知失败");
                }

                // 等待响应（带超时）
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(_options.TimeoutMs);
                
                var response = await tcs.Task.WaitAsync(timeoutCts.Token);

                if (!response.IsSuccess)
                {
                    _logger.LogWarning(
                        "上游返回失败响应: ParcelId={ParcelId}, Error={Error}",
                        request.ParcelId,
                        response.ErrorMessage);
                }

                return response;
            }
            catch
            {
                _httpClient.ChuteAssignmentReceived -= handler;
                throw;
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning(
                "HTTP 网关请求超时: ParcelId={ParcelId}",
                request.ParcelId);
            throw new UpstreamUnavailableException("请求超时", new OperationCanceledException());
        }
        catch (Exception ex) when (ex is not UpstreamUnavailableException && ex is not InvalidResponseException)
        {
            _logger.LogError(
                ex,
                "HTTP 网关请求失败: ParcelId={ParcelId}",
                request.ParcelId);
            throw new UpstreamUnavailableException($"通信失败: {ex.Message}", ex);
        }
    }
}
