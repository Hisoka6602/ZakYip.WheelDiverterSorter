using Microsoft.Extensions.Logging;
using ZakYip.Sorting.Core.Contracts;
using ZakYip.Sorting.Core.Exceptions;
using ZakYip.Sorting.Core.Interfaces;
using ZakYip.Sorting.Core.Models;

namespace ZakYip.Sorting.Core.Policies;

/// <summary>
/// 默认分拣异常策略实现
/// </summary>
/// <remarks>
/// 基于配置的异常路由策略，统一处理上游失败、超时等异常情况
/// </remarks>
public class DefaultSortingExceptionPolicy : ISortingExceptionPolicy
{
    private readonly ExceptionRoutingPolicy _policy;
    private readonly ILogger<DefaultSortingExceptionPolicy> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    public DefaultSortingExceptionPolicy(
        ExceptionRoutingPolicy policy,
        ILogger<DefaultSortingExceptionPolicy> logger)
    {
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 判断是否应该使用异常格口
    /// </summary>
    public bool ShouldUseExceptionChute(string failureReason)
    {
        if (string.IsNullOrWhiteSpace(failureReason))
        {
            return true;
        }

        // 根据失败原因判断
        return failureReason switch
        {
            "UPSTREAM_TIMEOUT" => true,
            "UPSTREAM_UNAVAILABLE" => true,
            "INVALID_RESPONSE" => true,
            "TOPOLOGY_UNREACHABLE" => _policy.UseExceptionOnTopologyUnreachable,
            "TTL_FAILURE" => _policy.UseExceptionOnTtlFailure,
            _ => true // 默认使用异常格口
        };
    }

    /// <summary>
    /// 判断是否应该重试
    /// </summary>
    public bool ShouldRetry(string failureReason, int attemptCount)
    {
        if (attemptCount >= _policy.RetryCount)
        {
            return false;
        }

        if (!_policy.RetryOnTimeout)
        {
            return false;
        }

        // 只对特定失败原因重试
        return failureReason switch
        {
            "UPSTREAM_TIMEOUT" => true,
            "UPSTREAM_UNAVAILABLE" => true,
            _ => false
        };
    }

    /// <summary>
    /// 获取异常路由策略
    /// </summary>
    public ExceptionRoutingPolicy GetPolicy()
    {
        return _policy;
    }

    /// <summary>
    /// 处理上游异常，决定如何响应
    /// </summary>
    public SortingResponse HandleUpstreamException(
        SortingRequest request,
        Exception exception,
        int attemptCount)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (exception == null)
        {
            throw new ArgumentNullException(nameof(exception));
        }

        // 确定失败原因代码
        string reasonCode;
        string errorMessage;

        switch (exception)
        {
            case UpstreamUnavailableException:
                reasonCode = "UPSTREAM_UNAVAILABLE";
                errorMessage = $"上游服务不可用: {exception.Message}";
                break;

            case InvalidResponseException:
                reasonCode = "INVALID_RESPONSE";
                errorMessage = $"上游响应无效: {exception.Message}";
                break;

            case OperationCanceledException:
            case TimeoutException:
                reasonCode = "UPSTREAM_TIMEOUT";
                errorMessage = $"上游请求超时 (尝试 {attemptCount} 次)";
                break;

            default:
                reasonCode = "UNKNOWN_ERROR";
                errorMessage = $"未知错误: {exception.Message}";
                break;
        }

        _logger.LogWarning(
            "处理上游异常: ParcelId={ParcelId}, ReasonCode={ReasonCode}, Attempt={Attempt}",
            request.ParcelId,
            reasonCode,
            attemptCount);

        // 判断是否应该重试
        if (ShouldRetry(reasonCode, attemptCount))
        {
            _logger.LogInformation(
                "异常策略决定重试: ParcelId={ParcelId}, ReasonCode={ReasonCode}, Attempt={Attempt}",
                request.ParcelId,
                reasonCode,
                attemptCount);

            // 返回一个表示需要重试的响应（IsSuccess = false, 但不是异常格口）
            return new SortingResponse
            {
                ParcelId = request.ParcelId,
                TargetChuteId = 0, // 0 表示需要重试，不是有效格口
                IsSuccess = false,
                IsException = false,
                ReasonCode = $"{reasonCode}_RETRY",
                ErrorMessage = $"{errorMessage} - 将重试",
                Source = "ExceptionPolicy"
            };
        }

        // 不重试，使用异常格口
        if (ShouldUseExceptionChute(reasonCode))
        {
            _logger.LogWarning(
                "异常策略决定使用异常格口: ParcelId={ParcelId}, ReasonCode={ReasonCode}, ExceptionChuteId={ChuteId}",
                request.ParcelId,
                reasonCode,
                _policy.ExceptionChuteId);

            return new SortingResponse
            {
                ParcelId = request.ParcelId,
                TargetChuteId = _policy.ExceptionChuteId,
                IsSuccess = false,
                IsException = true,
                ReasonCode = reasonCode,
                ErrorMessage = errorMessage,
                Source = "ExceptionPolicy"
            };
        }

        // 既不重试也不使用异常格口（罕见情况，记为失败）
        _logger.LogError(
            "异常策略无法处理: ParcelId={ParcelId}, ReasonCode={ReasonCode}",
            request.ParcelId,
            reasonCode);

        return new SortingResponse
        {
            ParcelId = request.ParcelId,
            TargetChuteId = 0,
            IsSuccess = false,
            IsException = false,
            ReasonCode = $"{reasonCode}_UNHANDLED",
            ErrorMessage = $"{errorMessage} - 无法处理",
            Source = "ExceptionPolicy"
        };
    }
}
