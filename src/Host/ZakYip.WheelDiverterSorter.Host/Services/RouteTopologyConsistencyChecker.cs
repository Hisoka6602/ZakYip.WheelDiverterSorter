using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;
using ZakYip.WheelDiverterSorter.Core.LineModel.Orchestration;
using ZakYip.WheelDiverterSorter.Observability.Utilities;

namespace ZakYip.WheelDiverterSorter.Host.Services;

/// <summary>
/// 路由-拓扑一致性检查器实现
/// </summary>
/// <remarks>
/// 在应用启动时检查路由配置与拓扑配置的一致性，
/// 确保所有路由引用的 ChuteId 都在拓扑中存在
/// </remarks>
public class RouteTopologyConsistencyChecker : IRouteTopologyConsistencyChecker
{
    private readonly IRouteConfigurationRepository _routeRepository;
    private readonly ILineTopologyRepository _topologyRepository;
    private readonly ISystemClock _clock;
    private readonly ILogger<RouteTopologyConsistencyChecker> _logger;

    public RouteTopologyConsistencyChecker(
        IRouteConfigurationRepository routeRepository,
        ILineTopologyRepository topologyRepository,
        ISystemClock clock,
        ILogger<RouteTopologyConsistencyChecker> logger)
    {
        _routeRepository = routeRepository ?? throw new ArgumentNullException(nameof(routeRepository));
        _topologyRepository = topologyRepository ?? throw new ArgumentNullException(nameof(topologyRepository));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 检查路由与拓扑的一致性
    /// </summary>
    public RouteTopologyConsistencyResult Check()
    {
        var checkTime = _clock.LocalNow;

        // 1. 获取所有路由配置中的 ChuteId (long)
        var routingChuteIds = GetRoutingChuteIds();

        // 2. 获取所有拓扑配置中的 ChuteId (string -> long)
        var topologyChuteIds = GetTopologyChuteIds();

        // 3. 找出路由中引用但拓扑中不存在的 ChuteId
        var invalidReferences = routingChuteIds.Except(topologyChuteIds).ToHashSet();

        // 4. 找出拓扑中存在但路由未使用的 ChuteId
        var unusedChutes = topologyChuteIds.Except(routingChuteIds).ToHashSet();

        var isConsistent = invalidReferences.Count == 0;

        _logger.LogInformation(
            "路由-拓扑一致性检查完成: " +
            "检查时间={CheckTime:yyyy-MM-dd HH:mm:ss}, " +
            "一致性={IsConsistent}, " +
            "路由格口总数={RoutingCount}, " +
            "拓扑格口总数={TopologyCount}, " +
            "非法引用数={InvalidCount}, " +
            "未使用格口数={UnusedCount}",
            checkTime,
            isConsistent,
            routingChuteIds.Count,
            topologyChuteIds.Count,
            invalidReferences.Count,
            unusedChutes.Count);

        if (invalidReferences.Count > 0)
        {
            _logger.LogWarning(
                "发现路由配置中引用了拓扑中不存在的格口ID: [{InvalidChuteIds}]。" +
                "这些包裹在运行时将被强制路由到异常格口。",
                string.Join(", ", invalidReferences.OrderBy(id => id)));
        }

        if (unusedChutes.Count > 0)
        {
            _logger.LogInformation(
                "发现拓扑中存在但路由未使用的格口ID: [{UnusedChuteIds}]。" +
                "这些格口可能是预留的或暂未配置路由规则。",
                string.Join(", ", unusedChutes.OrderBy(id => id)));
        }

        return new RouteTopologyConsistencyResult
        {
            IsConsistent = isConsistent,
            RoutingChuteIds = routingChuteIds,
            TopologyChuteIds = topologyChuteIds,
            InvalidRoutingReferences = invalidReferences,
            UnusedTopologyChutes = unusedChutes,
            CheckedAt = checkTime
        };
    }

    /// <summary>
    /// 获取所有路由配置中的 ChuteId
    /// </summary>
    private HashSet<long> GetRoutingChuteIds()
    {
        try
        {
            var routeConfigs = _routeRepository.GetAllEnabled();
            return routeConfigs.Select(c => c.ChuteId).ToHashSet();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取路由配置时发生错误");
            return new HashSet<long>();
        }
    }

    /// <summary>
    /// 获取所有拓扑配置中的 ChuteId（转换为 long）
    /// </summary>
    private HashSet<long> GetTopologyChuteIds()
    {
        try
        {
            var topology = _topologyRepository.Get();
            var chuteIds = new HashSet<long>();

            foreach (var chute in topology.Chutes.Where(c => c.IsEnabled))
            {
                // 尝试将 string ChuteId 转换为 long
                if (long.TryParse(chute.ChuteId, out var chuteIdLong))
                {
                    chuteIds.Add(chuteIdLong);
                }
                else
                {
                    _logger.LogWarning(
                        "拓扑中的格口ID '{ChuteId}' 无法转换为 long 类型，已跳过",
                        chute.ChuteId);
                }
            }

            return chuteIds;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取拓扑配置时发生错误");
            return new HashSet<long>();
        }
    }
}
