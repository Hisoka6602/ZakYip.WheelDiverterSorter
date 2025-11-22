using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;
using ZakYip.WheelDiverterSorter.Core.LineModel.Topology;

namespace ZakYip.WheelDiverterSorter.Core.LineModel.Orchestration;

/// <summary>
/// 路由-拓扑一致性检查器实现
/// </summary>
/// <remarks>
/// 验证路由配置中的所有 ChuteId 是否都能通过路径生成器生成有效路径。
/// 这是编排层服务，同时引用 Routing (IRouteConfigurationRepository) 和 Topology (ISwitchingPathGenerator)。
/// </remarks>
public class RouteTopologyConsistencyChecker : IRouteTopologyConsistencyChecker
{
    private readonly IRouteConfigurationRepository _routeRepository;
    private readonly ISwitchingPathGenerator _pathGenerator;
    private readonly ILogger<RouteTopologyConsistencyChecker> _logger;

    public RouteTopologyConsistencyChecker(
        IRouteConfigurationRepository routeRepository,
        ISwitchingPathGenerator pathGenerator,
        ILogger<RouteTopologyConsistencyChecker> logger)
    {
        _routeRepository = routeRepository ?? throw new ArgumentNullException(nameof(routeRepository));
        _pathGenerator = pathGenerator ?? throw new ArgumentNullException(nameof(pathGenerator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 执行一致性检查
    /// </summary>
    public ConsistencyCheckResult CheckConsistency()
    {
        _logger.LogInformation("开始执行路由-拓扑一致性检查...");

        // 获取所有启用的路由配置
        var allRouteConfigs = _routeRepository.GetAllEnabled().ToList();
        var totalRouteChuteIds = allRouteConfigs.Count;

        _logger.LogInformation("共发现 {Count} 个启用的路由配置", totalRouteChuteIds);

        // 检查每个 ChuteId 是否能生成有效路径
        var invalidChuteIds = new List<long>();

        foreach (var routeConfig in allRouteConfigs)
        {
            var chuteId = routeConfig.ChuteId;
            
            try
            {
                var path = _pathGenerator.GeneratePath(chuteId);
                
                if (path == null)
                {
                    _logger.LogWarning(
                        "路由配置 ChuteId={ChuteId} ({ChuteName}) 无法生成有效路径（拓扑中不存在对应配置）",
                        chuteId,
                        routeConfig.ChuteName ?? "未命名");
                    invalidChuteIds.Add(chuteId);
                }
                else
                {
                    _logger.LogDebug(
                        "路由配置 ChuteId={ChuteId} ({ChuteName}) 验证通过，路径段数={SegmentCount}",
                        chuteId,
                        routeConfig.ChuteName ?? "未命名",
                        path.Segments.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "检查路由配置 ChuteId={ChuteId} ({ChuteName}) 时发生异常",
                    chuteId,
                    routeConfig.ChuteName ?? "未命名");
                invalidChuteIds.Add(chuteId);
            }
        }

        var validRouteChuteIds = totalRouteChuteIds - invalidChuteIds.Count;
        var checkedAt = DateTime.Now; // 使用本地时间

        var result = new ConsistencyCheckResult
        {
            TotalRouteChuteIds = totalRouteChuteIds,
            ValidRouteChuteIds = validRouteChuteIds,
            InvalidRouteChuteIds = invalidChuteIds.AsReadOnly(),
            CheckedAt = checkedAt
        };

        // 记录检查结果摘要
        if (result.IsConsistent)
        {
            _logger.LogInformation(
                "✅ 路由-拓扑一致性检查通过：所有 {Total} 个路由配置都能生成有效路径",
                totalRouteChuteIds);
        }
        else
        {
            _logger.LogWarning(
                "⚠️  路由-拓扑一致性检查发现问题：" +
                "总路由配置={Total}，有效={Valid}，无效={Invalid}。" +
                "无效的 ChuteId 列表: [{InvalidList}]",
                totalRouteChuteIds,
                validRouteChuteIds,
                invalidChuteIds.Count,
                string.Join(", ", invalidChuteIds));
        }

        return result;
    }
}
