using ZakYip.WheelDiverterSorter.Core.Sorting.Contracts;

namespace ZakYip.WheelDiverterSorter.Core.Sorting.Interfaces;

/// <summary>
/// 上游分拣网关接口
/// </summary>
/// <remarks>
/// 统一抽象对上游 RuleEngine 的调用，隔离协议细节
/// </remarks>
public interface IUpstreamSortingGateway
{
    /// <summary>
    /// 请求分拣决策
    /// </summary>
    /// <param name="request">分拣请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>分拣响应</returns>
    /// <exception cref="UpstreamUnavailableException">上游服务不可用</exception>
    /// <exception cref="InvalidResponseException">上游响应无效</exception>
    Task<SortingResponse> RequestSortingAsync(
        SortingRequest request,
        CancellationToken cancellationToken = default);
}
