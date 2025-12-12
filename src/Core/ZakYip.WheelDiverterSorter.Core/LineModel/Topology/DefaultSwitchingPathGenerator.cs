using ZakYip.WheelDiverterSorter.Core.Abstractions.Execution;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.LineModel.Chutes;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using Microsoft.Extensions.Logging;

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

    /// <summary>
    /// 默认的超时阈值（毫秒）- 2秒
    /// </summary>
    /// <remarks>
    /// <para>当线段配置中未提供 TimeToleranceMs 时使用的回退值。</para>
    /// <para>2000ms 作为保守默认值，适用于大多数场景的时间容差。</para>
    /// </remarks>
    private const int DefaultTimeoutThresholdMs = 2000;

    private readonly IRouteConfigurationRepository? _routeRepository;
    private readonly IChutePathTopologyRepository? _topologyRepository;
    private readonly IConveyorSegmentRepository? _conveyorSegmentRepository;
    private readonly ISystemClock? _systemClock;
    private readonly ILogger? _logger;
    private bool _topologyConfigLogged = false;  // 用于记录是否已输出拓扑配置日志

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
    /// <param name="systemClock">系统时钟（必需，用于队列任务时间计算）</param>
    /// <param name="conveyorSegmentRepository">输送线段配置仓储（可选，用于动态TTL计算）</param>
    /// <param name="logger">日志记录器（可选）</param>
    public DefaultSwitchingPathGenerator(
        IChutePathTopologyRepository topologyRepository,
        ISystemClock systemClock,
        IConveyorSegmentRepository? conveyorSegmentRepository = null,
        ILogger<DefaultSwitchingPathGenerator>? logger = null)
    {
        _topologyRepository = topologyRepository ?? throw new ArgumentNullException(nameof(topologyRepository));
        _systemClock = systemClock ?? throw new ArgumentNullException(nameof(systemClock));
        _conveyorSegmentRepository = conveyorSegmentRepository;
        _logger = logger;
        
        // Validate that systemClock is not null for queue operations
        if (_systemClock == null)
        {
            throw new InvalidOperationException("SystemClock is required for queue task generation");
        }
    }

    /// <summary>
    /// 初始化路径生成器（同时支持两种配置源）
    /// </summary>
    /// <param name="routeRepository">路由配置仓储</param>
    /// <param name="topologyRepository">拓扑配置仓储</param>
    /// <param name="systemClock">系统时钟</param>
    /// <param name="conveyorSegmentRepository">输送线段配置仓储（可选，用于动态TTL计算）</param>
    /// <param name="logger">日志记录器（可选）</param>
    /// <remarks>
    /// 当同时提供两种仓储时，优先使用拓扑配置仓储（支持 N 摆轮模型）
    /// </remarks>
    public DefaultSwitchingPathGenerator(
        IRouteConfigurationRepository routeRepository,
        IChutePathTopologyRepository topologyRepository,
        ISystemClock systemClock,
        IConveyorSegmentRepository? conveyorSegmentRepository = null,
        ILogger<DefaultSwitchingPathGenerator>? logger = null)
    {
        _routeRepository = routeRepository;
        _topologyRepository = topologyRepository;
        _systemClock = systemClock ?? throw new ArgumentNullException(nameof(systemClock));
        _conveyorSegmentRepository = conveyorSegmentRepository;
        _logger = logger;
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
    /// <remarks>
    /// 修复问题：
    /// 1. TTL 计算从硬编码改为基于 ConveyorSegmentConfiguration 的动态计算
    /// 2. 路径只生成到目标摆轮，不生成后续摆轮的段（避免干扰后续包裹）
    /// </remarks>
    private SwitchingPath? GeneratePathFromTopology(long targetChuteId)
    {
        var topologyConfig = _topologyRepository!.Get();
        
        // 首次使用拓扑配置时输出配置信息（仅在首次调用时记录）
        if (!_topologyConfigLogged)
        {
            _topologyConfigLogged = true;
            System.Diagnostics.Debug.WriteLine(
                $"========== 使用拓扑配置生成路径 ==========\n" +
                $"  - 拓扑名称: {topologyConfig.TopologyName}\n" +
                $"  - 拓扑ID: {topologyConfig.TopologyId}\n" +
                $"  - 摆轮节点数: {topologyConfig.DiverterNodes.Count}\n" +
                $"  - 异常格口ID: {topologyConfig.ExceptionChuteId}\n" +
                $"  - 配置来源: 数据库 (Production)\n" +
                $"=========================================");
        }
        
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
            _logger?.LogWarning(
                "[路径生成失败] 目标格口 {TargetChuteId} 在拓扑配置中不存在",
                targetChuteId);
            return null;
        }

        // 确定目标格口的方向（左/右）
        var direction = topologyConfig.GetChuteDirection(targetChuteId);
        if (direction == null)
        {
            _logger?.LogWarning(
                "[路径生成失败] 无法确定格口 {TargetChuteId} 的分拣方向",
                targetChuteId);
            return null;
        }

        // 生成路径段：只生成到目标摆轮的段，不包含后续摆轮
        var segments = new List<SwitchingPathSegment>();
        var sortedNodes = topologyConfig.DiverterNodes
            .OrderBy(n => n.PositionIndex)
            .Where(n => n.PositionIndex <= targetNode.PositionIndex) // 只生成到目标摆轮
            .ToList();

        _logger?.LogDebug(
            "[路径生成] 目标格口 {TargetChuteId} 位于摆轮 {DiverterId} (PositionIndex={PositionIndex})，将生成 {SegmentCount} 个路径段",
            targetChuteId,
            targetNode.DiverterId,
            targetNode.PositionIndex,
            sortedNodes.Count);

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
                // 这个分支理论上不应该到达，因为我们已经过滤了 PositionIndex > targetNode.PositionIndex 的节点
                // 但为了代码健壮性，仍然处理这种情况
                targetDirection = DiverterDirection.Straight;
            }

            // 计算该段的 TTL
            var ttlMs = CalculateSegmentTtl(node.SegmentId);

            segments.Add(new SwitchingPathSegment
            {
                SequenceNumber = i + 1,
                DiverterId = node.DiverterId,
                TargetDirection = targetDirection,
                TtlMilliseconds = ttlMs
            });
        }

        _logger?.LogInformation(
            "[路径生成成功] 目标格口={TargetChuteId}, 路径段数={SegmentCount}, 总预计TTL={TotalTtlMs}ms",
            targetChuteId,
            segments.Count,
            segments.Sum(s => s.TtlMilliseconds));

        return new SwitchingPath
        {
            TargetChuteId = targetChuteId,
            Segments = segments.AsReadOnly(),
            GeneratedAt = _systemClock!.LocalNowOffset,
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
            var ttlMs = CalculateSegmentTtl(node.SegmentId);
            
            segments.Add(new SwitchingPathSegment
            {
                SequenceNumber = i + 1,
                DiverterId = node.DiverterId,
                TargetDirection = DiverterDirection.Straight,
                TtlMilliseconds = ttlMs
            });
        }

        _logger?.LogInformation(
            "[异常格口路径生成] 路径段数={SegmentCount}, 总预计TTL={TotalTtlMs}ms",
            segments.Count,
            segments.Sum(s => s.TtlMilliseconds));

        return new SwitchingPath
        {
            TargetChuteId = topologyConfig.ExceptionChuteId,
            Segments = segments.AsReadOnly(),
            GeneratedAt = _systemClock!.LocalNowOffset,
            FallbackChuteId = topologyConfig.ExceptionChuteId
        };
    }

    /// <summary>
    /// 计算路径段的 TTL（基于输送线段配置）
    /// </summary>
    /// <param name="segmentId">线段ID</param>
    /// <returns>TTL（毫秒）</returns>
    /// <remarks>
    /// 修复问题：TTL从硬编码5000ms改为根据线体配置动态计算
    /// 计算公式：TTL = (线段长度 / 线速) + 时间容差
    /// </remarks>
    private int CalculateSegmentTtl(long segmentId)
    {
        // 如果没有配置输送线段仓储，使用默认值
        if (_conveyorSegmentRepository == null)
        {
            _logger?.LogWarning(
                "[TTL计算] 未配置ConveyorSegmentRepository，使用默认TTL={DefaultTtlMs}ms（SegmentId={SegmentId}）",
                DefaultSegmentTtlMs,
                segmentId);
            return DefaultSegmentTtlMs;
        }

        // 从仓储获取线段配置
        var segmentConfig = _conveyorSegmentRepository.GetById(segmentId);
        if (segmentConfig == null)
        {
            _logger?.LogWarning(
                "[TTL计算] 线段配置不存在 (SegmentId={SegmentId})，使用默认TTL={DefaultTtlMs}ms",
                segmentId,
                DefaultSegmentTtlMs);
            return DefaultSegmentTtlMs;
        }

        // 计算超时阈值：理论传输时间 + 时间容差
        var calculatedTtlMs = (int)Math.Ceiling(segmentConfig.CalculateTimeoutThresholdMs());
        
        // 确保TTL不小于最小值（1秒）
        const int MinTtlMs = 1000;
        var finalTtl = Math.Max(calculatedTtlMs, MinTtlMs);

        _logger?.LogDebug(
            "[TTL计算] SegmentId={SegmentId}, 长度={LengthMm}mm, 速度={SpeedMmps}mm/s, " +
            "容差={ToleranceMs}ms, 理论时间={TransitTimeMs:F0}ms, 计算TTL={CalculatedTtl}ms, 最终TTL={FinalTtl}ms",
            segmentId,
            segmentConfig.LengthMm,
            segmentConfig.SpeedMmps,
            segmentConfig.TimeToleranceMs,
            segmentConfig.CalculateTransitTimeMs(),
            calculatedTtlMs,
            finalTtl);

        return finalTtl;
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

    /// <summary>
    /// 根据目标格口生成位置索引队列任务列表（Phase 4）
    /// </summary>
    /// <param name="parcelId">包裹ID</param>
    /// <param name="targetChuteId">目标格口标识（数字ID）</param>
    /// <param name="createdAt">包裹创建时间</param>
    /// <returns>队列任务列表，如果无法生成路径则返回空列表</returns>
    /// <remarks>
    /// <para>此方法为 Position-Index 队列系统生成任务列表。</para>
    /// <para><b>计算逻辑：</b></para>
    /// <list type="bullet">
    ///   <item>从拓扑配置读取路径节点</item>
    ///   <item>计算理论到达时间（createdAt + 累加前序线段传输时间）</item>
    ///   <item>从 ConveyorSegmentConfiguration 读取超时容差</item>
    ///   <item>确定摆轮动作（Left/Right/Straight）</item>
    ///   <item>设置回退动作为 Straight</item>
    /// </list>
    /// <para><b>示例：</b></para>
    /// <para>包裹P1目标格口3（位于D2左侧），生成任务：</para>
    /// <list type="bullet">
    ///   <item>Task1: positionIndex=1, action=Straight, expectedTime=创建时间+segment1传输时间</item>
    ///   <item>Task2: positionIndex=2, action=Left, expectedTime=创建时间+segment1+segment2传输时间</item>
    /// </list>
    /// </remarks>
    public List<PositionQueueItem> GenerateQueueTasks(
        long parcelId,
        long targetChuteId,
        DateTime createdAt)
    {
        var tasks = new List<PositionQueueItem>();

        // 验证必需的依赖项
        if (_systemClock == null)
        {
            throw new InvalidOperationException("SystemClock is required for queue task generation");
        }

        // 优先使用拓扑配置仓储
        if (_topologyRepository == null)
        {
            _logger?.LogWarning(
                "[队列任务生成失败] 未配置拓扑仓储，无法生成队列任务 (ParcelId={ParcelId}, TargetChuteId={TargetChuteId})",
                parcelId, targetChuteId);
            return tasks;
        }

        var topologyConfig = _topologyRepository.Get();

        // 如果目标是异常口，生成全直通任务
        if (targetChuteId == topologyConfig.ExceptionChuteId)
        {
            return GenerateExceptionQueueTasks(parcelId, topologyConfig, createdAt);
        }

        // 查找目标格口所在的摆轮节点
        var targetNode = topologyConfig.FindNodeByChuteId(targetChuteId);
        if (targetNode == null)
        {
            _logger?.LogWarning(
                "[队列任务生成失败] 目标格口 {TargetChuteId} 在拓扑配置中不存在 (ParcelId={ParcelId})",
                targetChuteId, parcelId);
            return tasks;
        }

        // 确定目标格口的方向（左/右）
        var direction = topologyConfig.GetChuteDirection(targetChuteId);
        if (direction == null)
        {
            _logger?.LogWarning(
                "[队列任务生成失败] 无法确定格口 {TargetChuteId} 的分拣方向 (ParcelId={ParcelId})",
                targetChuteId, parcelId);
            return tasks;
        }

        // 生成任务：只生成到目标摆轮的任务
        var sortedNodes = topologyConfig.DiverterNodes
            .OrderBy(n => n.PositionIndex)
            .Where(n => n.PositionIndex <= targetNode.PositionIndex)
            .ToList();

        _logger?.LogDebug(
            "[队列任务生成] ParcelId={ParcelId}, 目标格口={TargetChuteId}, 摆轮={DiverterId} (Position={PositionIndex}), 将生成 {TaskCount} 个任务",
            parcelId, targetChuteId, targetNode.DiverterId, targetNode.PositionIndex, sortedNodes.Count);

        // 累加计算理论到达时间
        var currentTime = createdAt;

        for (int i = 0; i < sortedNodes.Count; i++)
        {
            var node = sortedNodes[i];
            
            // 从输送线段配置读取传输时间和超时容差
            var segmentConfig = _conveyorSegmentRepository?.GetById(node.SegmentId);
            if (segmentConfig == null)
            {
                _logger?.LogWarning(
                    "[队列任务生成] 线段配置不存在 (SegmentId={SegmentId})，跳过该任务",
                    node.SegmentId);
                continue;
            }

            // 计算理论传输时间并累加到当前时间
            var transitTimeMs = segmentConfig.CalculateTransitTimeMs();
            currentTime = currentTime.AddMilliseconds(transitTimeMs);

            // 确定摆轮动作
            DiverterDirection targetDirection;
            if (node.PositionIndex < targetNode.PositionIndex)
            {
                // 在目标节点之前：直通
                targetDirection = DiverterDirection.Straight;
            }
            else if (node.PositionIndex == targetNode.PositionIndex)
            {
                // 目标节点：根据格口位置设置左/右
                targetDirection = direction.Value;
            }
            else
            {
                // 理论上不应到达（已过滤），但保持健壮性
                targetDirection = DiverterDirection.Straight;
            }

            // 创建队列任务
            var task = new PositionQueueItem
            {
                ParcelId = parcelId,
                DiverterId = node.DiverterId,
                PositionIndex = node.PositionIndex,
                DiverterAction = targetDirection,
                ExpectedArrivalTime = currentTime,
                TimeoutThresholdMs = segmentConfig.TimeToleranceMs,
                FallbackAction = DiverterDirection.Straight,
                CreatedAt = _systemClock.LocalNow
            };

            tasks.Add(task);

            _logger?.LogDebug(
                "[队列任务创建] ParcelId={ParcelId}, Position={PositionIndex}, Action={Action}, " +
                "ExpectedArrival={ExpectedTime:HH:mm:ss.fff}, Timeout={TimeoutMs}ms",
                parcelId, node.PositionIndex, targetDirection,
                currentTime, segmentConfig.TimeToleranceMs);
        }

        _logger?.LogInformation(
            "[队列任务生成成功] ParcelId={ParcelId}, 目标格口={TargetChuteId}, 任务数={TaskCount}",
            parcelId, targetChuteId, tasks.Count);

        return tasks;
    }

    /// <summary>
    /// 生成到异常口的全直通队列任务
    /// </summary>
    /// <param name="parcelId">包裹ID</param>
    /// <param name="topologyConfig">拓扑配置</param>
    /// <param name="createdAt">包裹创建时间</param>
    /// <returns>全直通任务列表</returns>
    private List<PositionQueueItem> GenerateExceptionQueueTasks(
        long parcelId,
        ChutePathTopologyConfig topologyConfig,
        DateTime createdAt)
    {
        // _systemClock is validated in the calling method
        if (_systemClock == null)
        {
            throw new InvalidOperationException("SystemClock is required for queue task generation");
        }

        var tasks = new List<PositionQueueItem>();
        var sortedNodes = topologyConfig.DiverterNodes
            .OrderBy(n => n.PositionIndex)
            .ToList();

        var currentTime = createdAt;

        for (int i = 0; i < sortedNodes.Count; i++)
        {
            var node = sortedNodes[i];
            var segmentConfig = _conveyorSegmentRepository?.GetById(node.SegmentId);
            
            if (segmentConfig != null)
            {
                var transitTimeMs = segmentConfig.CalculateTransitTimeMs();
                currentTime = currentTime.AddMilliseconds(transitTimeMs);
            }

            var task = new PositionQueueItem
            {
                ParcelId = parcelId,
                DiverterId = node.DiverterId,
                PositionIndex = node.PositionIndex,
                DiverterAction = DiverterDirection.Straight,
                ExpectedArrivalTime = currentTime,
                TimeoutThresholdMs = segmentConfig?.TimeToleranceMs ?? DefaultTimeoutThresholdMs,
                FallbackAction = DiverterDirection.Straight,
                CreatedAt = _systemClock.LocalNow
            };

            tasks.Add(task);
        }

        _logger?.LogInformation(
            "[异常格口队列任务生成] ParcelId={ParcelId}, 任务数={TaskCount} (全直通)",
            parcelId, tasks.Count);

        return tasks;
    }
}
