using ZakYip.WheelDiverterSorter.Core.LineModel.Orchestration;

namespace ZakYip.WheelDiverterSorter.Application.Services;

/// <summary>
/// Application 层拓扑一致性检查服务接口
/// </summary>
/// <remarks>
/// 提供对 Core 层 IRouteTopologyConsistencyChecker 的包装，
/// 使 Host 层可以通过 Application 层调用拓扑一致性检查功能，
/// 而无需直接依赖 Core 层的实现细节。
/// </remarks>
public interface ITopologyConsistencyCheckService
{
    /// <summary>
    /// 执行路由-拓扑一致性检查
    /// </summary>
    /// <returns>检查结果</returns>
    ConsistencyCheckResult CheckConsistency();
}
