using ZakYip.WheelDiverterSorter.Core.Configuration;

namespace ZakYip.WheelDiverterSorter.Core;

/// <summary>
/// 默认的摆轮路径生成器实现，基于直线摆轮方案
/// <para>
/// 实现逻辑：包裹进入 → 获取目标Chute → 生成从入口到该Chute的摆轮列表
/// </para>
/// </summary>
/// <remarks>
/// <para><b>拓扑配置说明：</b></para>
/// <para>
/// 本生成器依赖摆轮拓扑配置来确定路径。拓扑配置描述了直线输送线上的摆轮节点顺序，
/// 以及每个节点能分到哪些Chute。相关配置类参见：
/// <list type="bullet">
/// <item><description><see cref="SorterTopology"/>: 描述完整的拓扑结构（入口 → A → B → C → 末端）</description></item>
/// <item><description><see cref="DiverterNode"/>: 表示单个摆轮节点及其支持的动作（直行/左/右）</description></item>
/// <item><description><see cref="DefaultSorterTopologyProvider"/>: 提供默认的硬编码拓扑示例</description></item>
/// </list>
/// </para>
/// <para>
/// 当前实现使用LiteDB数据库存储的配置映射，支持动态修改配置而无需重新编译部署。
/// </para>
/// </remarks>
public class DefaultSwitchingPathGenerator : ISwitchingPathGenerator
{
    /// <summary>
    /// 默认的段TTL值（毫秒）
    /// </summary>
    /// <remarks>
    /// 当前使用固定值，后续可通过配置或策略进行动态设置
    /// </remarks>
    private const int DefaultSegmentTtlMs = 5000;

    private readonly IRouteConfigurationRepository _routeRepository;

    /// <summary>
    /// 初始化路径生成器
    /// </summary>
    /// <param name="routeRepository">路由配置仓储</param>
    public DefaultSwitchingPathGenerator(IRouteConfigurationRepository routeRepository)
    {
        _routeRepository = routeRepository ?? throw new ArgumentNullException(nameof(routeRepository));
    }

    /// <summary>
    /// 根据目标格口生成摆轮路径
    /// </summary>
    /// <param name="targetChuteId">目标格口标识</param>
    /// <returns>
    /// 生成的摆轮路径，如果目标格口无法映射到任意摆轮组合则返回null。
    /// 当返回null时，包裹将走异常口处理流程。
    /// </returns>
    public SwitchingPath? GeneratePath(string targetChuteId)
    {
        if (string.IsNullOrWhiteSpace(targetChuteId))
        {
            return null;
        }

        // 从数据库查询格口对应的摆轮配置
        var routeConfig = _routeRepository.GetByChuteId(targetChuteId);
        if (routeConfig == null || routeConfig.DiverterConfigurations.Count == 0)
        {
            // 无法映射到摆轮组合，返回null，包裹将走异常口
            return null;
        }

        // 生成有序的摆轮段列表，按配置中的顺序号排序
        var segments = routeConfig.DiverterConfigurations
            .OrderBy(config => config.SequenceNumber)
            .Select((config, index) => new SwitchingPathSegment
            {
                SequenceNumber = index + 1,
                DiverterId = config.DiverterId,
                TargetAngle = config.TargetAngle,
                TtlMilliseconds = DefaultSegmentTtlMs
            })
            .ToList();

        return new SwitchingPath
        {
            TargetChuteId = targetChuteId,
            Segments = segments.AsReadOnly(),
            GeneratedAt = DateTimeOffset.UtcNow,
            FallbackChuteId = WellKnownChuteIds.Exception
        };
    }
}
