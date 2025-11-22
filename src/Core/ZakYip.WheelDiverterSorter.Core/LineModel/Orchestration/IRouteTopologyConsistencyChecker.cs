namespace ZakYip.WheelDiverterSorter.Core.LineModel.Orchestration;

/// <summary>
/// 路由-拓扑一致性检查器接口
/// </summary>
/// <remarks>
/// 负责检查路由配置中引用的 ChuteId 是否都存在于线体拓扑中
/// </remarks>
public interface IRouteTopologyConsistencyChecker
{
    /// <summary>
    /// 检查路由与拓扑的一致性
    /// </summary>
    /// <returns>一致性检查结果</returns>
    RouteTopologyConsistencyResult Check();
}

/// <summary>
/// 路由-拓扑一致性检查结果
/// </summary>
public record RouteTopologyConsistencyResult
{
    /// <summary>
    /// 检查是否通过（无不一致项）
    /// </summary>
    public required bool IsConsistent { get; init; }

    /// <summary>
    /// 路由配置中引用的所有 ChuteId
    /// </summary>
    public required IReadOnlySet<long> RoutingChuteIds { get; init; }

    /// <summary>
    /// 拓扑配置中定义的所有 ChuteId（转换为 long）
    /// </summary>
    public required IReadOnlySet<long> TopologyChuteIds { get; init; }

    /// <summary>
    /// 路由中引用但拓扑中不存在的 ChuteId（非法引用）
    /// </summary>
    public required IReadOnlySet<long> InvalidRoutingReferences { get; init; }

    /// <summary>
    /// 拓扑中存在但路由未使用的 ChuteId（未使用格口）
    /// </summary>
    public required IReadOnlySet<long> UnusedTopologyChutes { get; init; }

    /// <summary>
    /// 检查时间（本地时间）
    /// </summary>
    public required DateTime CheckedAt { get; init; }
}
