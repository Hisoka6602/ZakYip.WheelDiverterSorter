using ZakYip.Sorting.Core.Contracts;

namespace ZakYip.Sorting.Core.Interfaces;

/// <summary>
/// 分拣决策服务接口
/// </summary>
/// <remarks>
/// 对上游 RuleEngine 的统一抽象，定义请求/响应合同
/// </remarks>
public interface ISortingDecisionService
{
    /// <summary>
    /// 请求格口分配
    /// </summary>
    /// <param name="request">分拣请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>分拣响应</returns>
    Task<SortingResponse> RequestChuteAssignmentAsync(
        SortingRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 检查服务是否可用
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>服务是否可用</returns>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
}
