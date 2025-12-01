using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.LineModel.Chutes;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;

namespace ZakYip.WheelDiverterSorter.Core.LineModel.Topology;

/// <summary>
/// 默认的摆轮路径生成器实现，基于直线摆轮方案
/// <para>
/// 实现逻辑：包裹进入 → 获取目标Chute → 生成从入口到该Chute的摆轮列表
/// </para>
/// </summary>
/// <remarks>
/// <para><b>拓扑配置说明：</b></para>
/// <para>
/// 本生成器支持两种路径生成方式：
/// <list type="bullet">
/// <item><description>基于 <see cref="IRouteConfigurationRepository"/>：使用预定义的格口到摆轮映射（传统方式）</description></item>
/// <item><description>基于 <see cref="IChutePathTopologyRepository"/>：使用 N 摆轮拓扑配置动态生成路径（PR-TOPO02）</description></item>
/// </list>
/// </para>
/// <para><b>N 摆轮线性拓扑模型（PR-TOPO02）：</b></para>
/// <para>
/// 支持 N 个摆轮，每个摆轮左右各一个格口，末端一个异常口。
/// 路径生成策略：
/// <list type="bullet">
/// <item>若 targetChuteId == AbnormalChuteId → 生成全直通路径</item>
/// <item>否则在 Diverters 中找到匹配的节点：
///   <list type="bullet">
///   <item>对该节点之前的所有节点：TargetDirection = Straight</item>
///   <item>对该节点：根据匹配结果设为 Left / Right</item>
///   <item>对后续节点：全部 Straight</item>
///   </list>
/// </item>
/// </list>
/// </para>
/// </remarks>
public class DefaultSwitchingPathGenerator : ISwitchingPathGenerator
{
    /// <summary>
    /// 默认的段 TTL 值（毫秒）- 5秒
    /// </summary>
    /// <remarks>
    /// <para>此值用于拓扑模式下的路径段超时设置。</para>
    /// <para>选择 5000ms 的原因：</para>
    /// <list type="bullet">
    /// <item>典型皮带段长度 3-5m，运行速度 0.5-1.5m/s</item>
    /// <item>理论通过时间约 2-10 秒</item>
    /// <item>5 秒作为保守默认值，覆盖大多数场景</item>
    /// </list>
    /// <para>实际应用中，应根据具体线体参数通过 <see cref="DiverterConfigurationEntry"/> 配置。</para>
    /// </remarks>
    private const int DefaultSegmentTtlMs = 5000;

    private readonly IRouteConfigurationRepository? _routeRepository;
    private readonly IChutePathTopologyRepository? _topologyRepository;

    /// <summary>
    /// 初始化路径生成器（使用路由配置仓储）
    /// </summary>
    /// <param name="routeRepository">路由配置仓储</param>
    public DefaultSwitchingPathGenerator(IRouteConfigurationRepository routeRepository)
    {
        _routeRepository = routeRepository ?? throw new ArgumentNullException(nameof(routeRepository));
    }

    /// <summary>
    /// 初始化路径生成器（使用拓扑配置仓储，支持 N 摆轮模型）
    /// </summary>
    /// <param name="topologyRepository">拓扑配置仓储</param>
    public DefaultSwitchingPathGenerator(IChutePathTopologyRepository topologyRepository)
    {
        _topologyRepository = topologyRepository ?? throw new ArgumentNullException(nameof(topologyRepository));
    }

    /// <summary>
    /// 初始化路径生成器（同时支持两种配置源）
    /// </summary>
    /// <param name="routeRepository">路由配置仓储</param>
    /// <param name="topologyRepository">拓扑配置仓储</param>
    /// <remarks>
    /// 当同时提供两种仓储时，优先使用拓扑配置仓储（支持 N 摆轮模型）
    /// </remarks>
    public DefaultSwitchingPathGenerator(
        IRouteConfigurationRepository routeRepository,
        IChutePathTopologyRepository topologyRepository)
    {
        _routeRepository = routeRepository;
        _topologyRepository = topologyRepository;
    }

    /// <summary>
    /// 根据目标格口生成摆轮路径
    /// </summary>
    /// <param name="targetChuteId">目标格口标识（数字ID）</param>
    /// <returns>
    /// 生成的摆轮路径，如果目标格口无法映射到任意摆轮组合则返回null。
    /// 当返回null时，包裹将走异常口处理流程。
    /// </returns>
    public SwitchingPath? GeneratePath(long targetChuteId)
    {
        if (targetChuteId <= 0)
        {
            return null;
        }

        // 优先使用拓扑配置仓储（支持 N 摆轮模型）
        if (_topologyRepository != null)
        {
            return GeneratePathFromTopology(targetChuteId);
        }

        // 回退到传统的路由配置方式
        return GeneratePathFromRouteConfig(targetChuteId);
    }

    /// <summary>
    /// 基于 N 摆轮拓扑配置生成路径（PR-TOPO02）
    /// </summary>
    /// <param name="targetChuteId">目标格口ID</param>
    /// <returns>生成的摆轮路径</returns>
    private SwitchingPath? GeneratePathFromTopology(long targetChuteId)
    {
        var topologyConfig = _topologyRepository!.Get();
        
        // 如果目标是异常口，生成全直通路径
        if (targetChuteId == topologyConfig.ExceptionChuteId)
        {
            return GenerateExceptionPath(topologyConfig);
        }

        // 查找目标格口所在的摆轮节点
        var targetNode = topologyConfig.FindNodeByChuteId(targetChuteId);
        if (targetNode == null)
        {
            // 无法找到目标格口，返回 null，由调用方处理（走异常口）
            return null;
        }

        // 确定目标格口的方向（左/右）
        var direction = topologyConfig.GetChuteDirection(targetChuteId);
        if (direction == null)
        {
            return null;
        }

        // 生成路径段
        var segments = new List<SwitchingPathSegment>();
        var sortedNodes = topologyConfig.DiverterNodes
            .OrderBy(n => n.PositionIndex)
            .ToList();

        for (int i = 0; i < sortedNodes.Count; i++)
        {
            var node = sortedNodes[i];
            DiverterDirection targetDirection;

            if (node.PositionIndex < targetNode.PositionIndex)
            {
                // 在目标节点之前的节点：直通
                targetDirection = DiverterDirection.Straight;
            }
            else if (node.PositionIndex == targetNode.PositionIndex)
            {
                // 目标节点：根据格口位置设置左/右
                targetDirection = direction.Value;
            }
            else
            {
                // 在目标节点之后的节点：直通（包裹已经被分拣走）
                targetDirection = DiverterDirection.Straight;
            }

            segments.Add(new SwitchingPathSegment
            {
                SequenceNumber = i + 1,
                DiverterId = node.DiverterId,
                TargetDirection = targetDirection,
                TtlMilliseconds = DefaultSegmentTtlMs
            });
        }

        return new SwitchingPath
        {
            TargetChuteId = targetChuteId,
            Segments = segments.AsReadOnly(),
            GeneratedAt = DateTimeOffset.Now,
            FallbackChuteId = topologyConfig.ExceptionChuteId
        };
    }

    /// <summary>
    /// 生成到异常口的全直通路径
    /// </summary>
    /// <param name="topologyConfig">拓扑配置</param>
    /// <returns>全直通路径</returns>
    private SwitchingPath GenerateExceptionPath(ChutePathTopologyConfig topologyConfig)
    {
        var sortedNodes = topologyConfig.DiverterNodes
            .OrderBy(n => n.PositionIndex)
            .ToList();

        var segments = new List<SwitchingPathSegment>(sortedNodes.Count);
        
        for (int i = 0; i < sortedNodes.Count; i++)
        {
            var node = sortedNodes[i];
            segments.Add(new SwitchingPathSegment
            {
                SequenceNumber = i + 1,
                DiverterId = node.DiverterId,
                TargetDirection = DiverterDirection.Straight,
                TtlMilliseconds = DefaultSegmentTtlMs
            });
        }

        return new SwitchingPath
        {
            TargetChuteId = topologyConfig.ExceptionChuteId,
            Segments = segments.AsReadOnly(),
            GeneratedAt = DateTimeOffset.Now,
            FallbackChuteId = topologyConfig.ExceptionChuteId
        };
    }

    /// <summary>
    /// 基于传统路由配置生成路径
    /// </summary>
    /// <param name="targetChuteId">目标格口ID</param>
    /// <returns>生成的摆轮路径</returns>
    /// <remarks>
    /// 当使用仅拓扑仓储的构造函数时，此方法将返回 null。
    /// 这是预期行为，因为路径生成已在 <see cref="GeneratePathFromTopology"/> 中处理。
    /// </remarks>
    private SwitchingPath? GeneratePathFromRouteConfig(long targetChuteId)
    {
        // 当使用仅拓扑仓储的构造函数时，_routeRepository 为 null
        if (_routeRepository == null)
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

        // PR-5: Optimize - avoid LINQ allocations by sorting in-place when needed
        var configs = routeConfig.DiverterConfigurations;
        var segments = new List<SwitchingPathSegment>(configs.Count);
        
        if (configs.Count > 1)
        {
            // For multiple configs, use List.Sort for better performance than LINQ OrderBy
            var sortedConfigs = new List<DiverterConfigurationEntry>(configs);
            sortedConfigs.Sort((a, b) => a.SequenceNumber.CompareTo(b.SequenceNumber));
            
            for (int i = 0; i < sortedConfigs.Count; i++)
            {
                var config = sortedConfigs[i];
                segments.Add(new SwitchingPathSegment
                {
                    SequenceNumber = i + 1,
                    DiverterId = config.DiverterId,
                    TargetDirection = config.TargetDirection,
                    TtlMilliseconds = CalculateSegmentTtl(config)
                });
            }
        }
        else
        {
            // Single config - no sorting needed
            var config = configs[0];
            segments.Add(new SwitchingPathSegment
            {
                SequenceNumber = 1,
                DiverterId = config.DiverterId,
                TargetDirection = config.TargetDirection,
                TtlMilliseconds = CalculateSegmentTtl(config)
            });
        }

        return new SwitchingPath
        {
            TargetChuteId = targetChuteId,
            Segments = segments.AsReadOnly(),
            GeneratedAt = DateTimeOffset.Now,
            FallbackChuteId = WellKnownChuteIds.DefaultException
        };
    }

    /// <summary>
    /// 计算段的TTL（生存时间）
    /// </summary>
    /// <param name="segmentConfig">段配置</param>
    /// <returns>计算得到的TTL（毫秒）</returns>
    /// <remarks>
    /// <para><strong>计算公式：</strong>TTL = (段长度 / 段速度) * 1000 + 容差时间</para>
    /// <para><strong>最小值保证：</strong>如果计算结果小于最小TTL（1000ms），使用最小TTL</para>
    /// <para><strong>容差验证：</strong>容差时间应该合理设置，确保不会与下一个包裹到达时间重叠。
    /// 理想的容差时间应该小于包裹间隔时间的一半，以避免两个包裹的超时检测窗口重叠。</para>
    /// <para><strong>建议配置：</strong>如果包裹间隔时间为X毫秒，容差时间应 &lt; X/2，
    /// 例如：包裹间隔1000ms，则容差应 &lt; 500ms；包裹间隔500ms，则容差应 &lt; 250ms</para>
    /// </remarks>
    private int CalculateSegmentTtl(DiverterConfigurationEntry segmentConfig)
    {
        // 计算理论通过时间（毫秒）
        var theoreticalTimeMs = (segmentConfig.SegmentLengthMm / segmentConfig.SegmentSpeedMmPerSecond) * 1000;
        
        // 加上容差时间
        var calculatedTtl = (int)Math.Ceiling(theoreticalTimeMs) + segmentConfig.SegmentToleranceTimeMs;
        
        // 确保TTL不小于最小值（1秒）
        const int MinTtlMs = 1000;
        return Math.Max(calculatedTtl, MinTtlMs);
    }

    /// <summary>
    /// 验证段配置的容差时间是否合理
    /// </summary>
    /// <param name="segmentConfig">段配置</param>
    /// <param name="parcelIntervalMs">包裹间隔时间（毫秒）- 两个连续包裹之间的时间间隔</param>
    /// <returns>验证结果：true表示容差时间合理，false表示可能导致超时窗口重叠</returns>
    /// <remarks>
    /// <para>为了避免相邻包裹的超时检测窗口重叠，容差时间应该满足以下条件：</para>
    /// <para><strong>验证规则：</strong>容差时间 &lt; 包裹间隔时间 / 2</para>
    /// <para><strong>原理：</strong>如果包裹A在时刻T到达，包裹B在时刻T+间隔到达，
    /// 那么包裹A的超时窗口为[T-容差, T+容差]，包裹B的窗口为[T+间隔-容差, T+间隔+容差]。
    /// 为避免重叠，需要T+容差 &lt; T+间隔-容差，即2*容差 &lt; 间隔，即容差 &lt; 间隔/2</para>
    /// </remarks>
    public static bool ValidateToleranceTime(DiverterConfigurationEntry segmentConfig, int parcelIntervalMs)
    {
        if (parcelIntervalMs <= 0)
        {
            throw new ArgumentException("包裹间隔时间必须大于0", nameof(parcelIntervalMs));
        }

        // 容差时间应该小于包裹间隔时间的一半
        // 这样可以确保相邻包裹的超时检测窗口不会重叠
        return segmentConfig.SegmentToleranceTimeMs < (parcelIntervalMs / 2.0);
    }

    /// <summary>
    /// 评估路径是否能在时间预算内完成
    /// </summary>
    /// <param name="path">待评估的路径</param>
    /// <param name="currentLineSpeedMmPerSecond">当前线速（毫米/秒）</param>
    /// <param name="availableTimeBudgetMs">可用时间预算（毫秒）</param>
    /// <returns>如果路径能在时间预算内完成返回true，否则返回false</returns>
    /// <remarks>
    /// 此方法用于在路径规划阶段进行二次超载检查，判断包裹是否能在剩余TTL内完成路径。
    /// </remarks>
    public static bool CanCompleteRouteInTime(
        SwitchingPath path,
        decimal currentLineSpeedMmPerSecond,
        double availableTimeBudgetMs)
    {
        if (path == null || path.Segments == null || path.Segments.Count == 0)
        {
            return false;
        }

        if (currentLineSpeedMmPerSecond <= 0)
        {
            return false;
        }

        // PR-5: Optimize - use simple loop instead of LINQ Sum to avoid allocation
        double totalRequiredTimeMs = 0;
        for (int i = 0; i < path.Segments.Count; i++)
        {
            totalRequiredTimeMs += path.Segments[i].TtlMilliseconds;
        }

        // 判断可用时间是否足够
        return totalRequiredTimeMs <= availableTimeBudgetMs;
    }
}
