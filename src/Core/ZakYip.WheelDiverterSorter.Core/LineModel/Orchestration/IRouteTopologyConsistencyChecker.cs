namespace ZakYip.WheelDiverterSorter.Core.LineModel.Orchestration;

/// <summary>
/// 路由-拓扑一致性检查器接口
/// </summary>
/// <remarks>
/// 负责检查路由配置中引用的所有 ChuteId 是否都能在拓扑中生成有效路径。
/// 这是一个编排层服务，同时依赖 Routing 和 Topology 层。
/// </remarks>
public interface IRouteTopologyConsistencyChecker
{
    /// <summary>
    /// 执行一致性检查
    /// </summary>
    /// <returns>检查结果</returns>
    ConsistencyCheckResult CheckConsistency();
}

/// <summary>
/// 一致性检查结果
/// </summary>
public record ConsistencyCheckResult
{
    /// <summary>
    /// 路由配置中的总 ChuteId 数量
    /// </summary>
    public required int TotalRouteChuteIds { get; init; }

    /// <summary>
    /// 能够生成有效路径的 ChuteId 数量
    /// </summary>
    public required int ValidRouteChuteIds { get; init; }

    /// <summary>
    /// 无法生成有效路径的 ChuteId 列表（不在拓扑中）
    /// </summary>
    public required IReadOnlyList<long> InvalidRouteChuteIds { get; init; }

    /// <summary>
    /// 是否全部一致（所有路由 ChuteId 都能生成路径）
    /// </summary>
    public bool IsConsistent => InvalidRouteChuteIds.Count == 0;

    /// <summary>
    /// 检查时间（本地时间）
    /// </summary>
    public required DateTime CheckedAt { get; init; }
}
