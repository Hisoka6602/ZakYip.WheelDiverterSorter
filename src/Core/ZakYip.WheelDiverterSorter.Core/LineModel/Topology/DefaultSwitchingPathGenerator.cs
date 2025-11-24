using ZakYip.WheelDiverterSorter.Core.LineModel.Chutes;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;
using ZakYip.WheelDiverterSorter.Observability.Utilities;

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
    private readonly ISystemClock _systemClock;

    /// <summary>
    /// 初始化路径生成器
    /// </summary>
    /// <param name="routeRepository">路由配置仓储</param>
    /// <param name="systemClock">系统时钟</param>
    public DefaultSwitchingPathGenerator(IRouteConfigurationRepository routeRepository, ISystemClock systemClock)
    {
        _routeRepository = routeRepository ?? throw new ArgumentNullException(nameof(routeRepository));
        _systemClock = systemClock ?? throw new ArgumentNullException(nameof(systemClock));
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
            GeneratedAt = _systemClock.LocalNowOffset,
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
