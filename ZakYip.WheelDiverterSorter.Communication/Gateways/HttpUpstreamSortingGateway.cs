using Microsoft.Extensions.Logging;
using ZakYip.Sorting.Core.Contracts;
using ZakYip.Sorting.Core.Exceptions;
using ZakYip.Sorting.Core.Interfaces;
using ZakYip.WheelDiverterSorter.Communication.Abstractions;
using ZakYip.WheelDiverterSorter.Communication.Configuration;

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

            // 调用底层 HTTP 客户端（使用已废弃的方法，但这是当前唯一的同步接口）
            #pragma warning disable CS0618
            var response = await _httpClient.RequestChuteAssignmentAsync(
                request.ParcelId,
                cancellationToken);
            #pragma warning restore CS0618

            // 转换响应
            if (response == null)
            {
                throw new InvalidResponseException("上游返回空响应");
            }

            if (!response.IsSuccess)
            {
                _logger.LogWarning(
                    "上游返回失败响应: ParcelId={ParcelId}, Error={Error}",
                    request.ParcelId,
                    response.ErrorMessage);
            }

            return new SortingResponse
            {
                ParcelId = response.ParcelId,
                TargetChuteId = response.ChuteId,
                IsSuccess = response.IsSuccess,
                IsException = !response.IsSuccess,
                ReasonCode = response.IsSuccess ? "SUCCESS" : "UPSTREAM_ERROR",
                ErrorMessage = response.ErrorMessage,
                ResponseTime = response.ResponseTime,
                Source = "HttpUpstreamGateway"
            };
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
