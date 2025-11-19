using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Sorting.Contracts;
using ZakYip.WheelDiverterSorter.Core.Sorting.Exceptions;
using ZakYip.WheelDiverterSorter.Core.Sorting.Interfaces;
using ZakYip.WheelDiverterSorter.Communication.Abstractions;
using ZakYip.WheelDiverterSorter.Communication.Configuration;

namespace ZakYip.WheelDiverterSorter.Communication.Gateways;

/// <summary>
/// SignalR 协议的上游分拣网关实现
/// </summary>
/// <remarks>
/// 适配 SignalRRuleEngineClient，提供协议层编解码和基础重试
/// 推荐生产环境使用
/// </remarks>
public class SignalRUpstreamSortingGateway : IUpstreamSortingGateway
{
    private readonly IRuleEngineClient _signalRClient;
    private readonly ILogger<SignalRUpstreamSortingGateway> _logger;
    private readonly RuleEngineConnectionOptions _options;

    /// <summary>
    /// 构造函数
    /// </summary>
    public SignalRUpstreamSortingGateway(
        IRuleEngineClient signalRClient,
        ILogger<SignalRUpstreamSortingGateway> logger,
        RuleEngineConnectionOptions options)
    {
        _signalRClient = signalRClient ?? throw new ArgumentNullException(nameof(signalRClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? throw new ArgumentNullException(nameof(options));
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
                "通过 SignalR 网关请求分拣决策: ParcelId={ParcelId}",
                request.ParcelId);

            // 确保连接已建立
            if (!_signalRClient.IsConnected)
            {
                var connected = await _signalRClient.ConnectAsync(cancellationToken);
                if (!connected)
                {
                    throw new UpstreamUnavailableException("无法连接到上游 SignalR 服务器");
                }
            }

            // 调用底层 SignalR 客户端（使用已废弃的方法，但这是当前唯一的同步接口）
            #pragma warning disable CS0618
            var response = await _signalRClient.RequestChuteAssignmentAsync(
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
                Source = "SignalRUpstreamGateway"
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning(
                "SignalR 网关请求超时: ParcelId={ParcelId}",
                request.ParcelId);
            throw new UpstreamUnavailableException("请求超时", new OperationCanceledException());
        }
        catch (Exception ex) when (ex is not UpstreamUnavailableException && ex is not InvalidResponseException)
        {
            _logger.LogError(
                ex,
                "SignalR 网关请求失败: ParcelId={ParcelId}",
                request.ParcelId);
            throw new UpstreamUnavailableException($"通信失败: {ex.Message}", ex);
        }
    }
}
