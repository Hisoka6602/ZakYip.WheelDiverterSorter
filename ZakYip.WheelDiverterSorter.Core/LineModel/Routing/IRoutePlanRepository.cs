namespace ZakYip.WheelDiverterSorter.Core.LineModel.Routing;

/// <summary>
/// 路由计划仓储接口
/// </summary>
public interface IRoutePlanRepository
{
    /// <summary>
    /// 根据包裹ID获取路由计划
    /// </summary>
    Task<RoutePlan?> GetByParcelIdAsync(long parcelId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 保存或更新路由计划
    /// </summary>
    Task SaveAsync(RoutePlan routePlan, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除路由计划
    /// </summary>
    Task DeleteAsync(long parcelId, CancellationToken cancellationToken = default);
}
