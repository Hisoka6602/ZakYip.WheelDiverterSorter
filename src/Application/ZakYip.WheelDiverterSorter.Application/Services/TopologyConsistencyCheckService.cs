using ZakYip.WheelDiverterSorter.Core.LineModel.Orchestration;

namespace ZakYip.WheelDiverterSorter.Application.Services;

/// <summary>
/// Application 层拓扑一致性检查服务实现
/// </summary>
/// <remarks>
/// 委托 Core 层的 IRouteTopologyConsistencyChecker 执行实际检查逻辑。
/// 此服务作为 Host 层与 Core 层之间的桥梁，
/// 确保 Host 层遵循分层架构规范，只依赖 Application 层。
/// </remarks>
public class TopologyConsistencyCheckService : ITopologyConsistencyCheckService
{
    private readonly IRouteTopologyConsistencyChecker _consistencyChecker;

    /// <summary>
    /// 初始化拓扑一致性检查服务
    /// </summary>
    /// <param name="consistencyChecker">Core 层的路由-拓扑一致性检查器</param>
    public TopologyConsistencyCheckService(IRouteTopologyConsistencyChecker consistencyChecker)
    {
        _consistencyChecker = consistencyChecker ?? throw new ArgumentNullException(nameof(consistencyChecker));
    }

    /// <inheritdoc />
    public ConsistencyCheckResult CheckConsistency()
    {
        return _consistencyChecker.CheckConsistency();
    }
}
