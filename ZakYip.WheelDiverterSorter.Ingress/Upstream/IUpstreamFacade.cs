using ZakYip.Sorting.Core.Contracts;

namespace ZakYip.WheelDiverterSorter.Ingress.Upstream;

/// <summary>
/// 上游门面接口，提供统一的上游系统访问入口
/// </summary>
public interface IUpstreamFacade
{
    /// <summary>
    /// 请求上游系统分配格口
    /// </summary>
    /// <param name="request">分配请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果，包含分配响应</returns>
    Task<OperationResult<AssignChuteResponse>> AssignChuteAsync(
        AssignChuteRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 通知上游系统创建包裹
    /// </summary>
    /// <param name="request">创建包裹请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果，包含创建响应</returns>
    Task<OperationResult<CreateParcelResponse>> CreateParcelAsync(
        CreateParcelRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取当前使用的通道名称
    /// </summary>
    string CurrentChannelName { get; }

    /// <summary>
    /// 获取所有可用通道
    /// </summary>
    IReadOnlyList<string> AvailableChannels { get; }
}
