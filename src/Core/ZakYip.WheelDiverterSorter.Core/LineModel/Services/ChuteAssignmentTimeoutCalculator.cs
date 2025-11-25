using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;

namespace ZakYip.WheelDiverterSorter.Core.LineModel.Services;

/// <summary>
/// 格口分配超时计算器实现
/// </summary>
/// <remarks>
/// 基于线体拓扑配置动态计算等待上游格口分配的超时时间
/// </remarks>
public class ChuteAssignmentTimeoutCalculator : IChuteAssignmentTimeoutCalculator
{
    private readonly ILineTopologyRepository _topologyRepository;
    private readonly ISystemConfigurationRepository _systemConfigRepository;
    private readonly ILogger<ChuteAssignmentTimeoutCalculator> _logger;

    public ChuteAssignmentTimeoutCalculator(
        ILineTopologyRepository topologyRepository,
        ISystemConfigurationRepository systemConfigRepository,
        ILogger<ChuteAssignmentTimeoutCalculator> logger)
    {
        _topologyRepository = topologyRepository ?? throw new ArgumentNullException(nameof(topologyRepository));
        _systemConfigRepository = systemConfigRepository ?? throw new ArgumentNullException(nameof(systemConfigRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 计算包裹等待上游格口分配的超时时间（秒）
    /// </summary>
    public decimal CalculateTimeoutSeconds(ChuteAssignmentTimeoutContext context)
    {
        try
        {
            // 0. 获取配置选项
            var systemConfig = _systemConfigRepository.Get();
            var options = systemConfig?.ChuteAssignmentTimeout ?? new ChuteAssignmentTimeoutOptions();

            // 1. 获取线体拓扑配置
            var topology = _topologyRepository.Get();
            if (topology == null)
            {
                _logger.LogError(
                    "无法获取线体拓扑配置，使用降级超时时间 {FallbackTimeout}秒",
                    options.FallbackTimeoutSeconds);
                return options.FallbackTimeoutSeconds;
            }

            // 2. 获取从入口到第一个摆轮前决策传感器的路径
            var pathToFirstWheel = GetPathToFirstDecisionPoint(topology);
            if (pathToFirstWheel == null || pathToFirstWheel.Count == 0)
            {
                _logger.LogError(
                    "无法解析从入口到第一个摆轮决策点的路径，使用降级超时时间 {FallbackTimeout}秒。" +
                    "请检查线体拓扑配置：确保入口节点 (ENTRY) 和第一个摆轮节点已正确配置。",
                    options.FallbackTimeoutSeconds);
                return options.FallbackTimeoutSeconds;
            }

            // 3. 计算理论物理极限时间（T_firstWheel）
            var totalTimeSeconds = 0.0m;
            foreach (var segment in pathToFirstWheel)
            {
#pragma warning disable CS0618 // Type or member is obsolete - backward compatibility
                if (segment.NominalSpeedMmPerSec <= 0)
                {
                    _logger.LogError(
                        "线体段 {SegmentId} 的速度配置无效（{Speed} mm/s），使用降级超时时间 {FallbackTimeout}秒。" +
                        "请检查线体段速度配置，速度必须大于0。",
                        segment.SegmentId,
                        segment.NominalSpeedMmPerSec,
                        options.FallbackTimeoutSeconds);
                    return options.FallbackTimeoutSeconds;
                }

                // 计算每段的时间：时间（秒） = 长度（mm） / 速度（mm/s）
                var segmentTimeSeconds = (decimal)(segment.LengthMm / segment.NominalSpeedMmPerSec);
                totalTimeSeconds += segmentTimeSeconds;

                _logger.LogTrace(
                    "线体段 {SegmentId}: 长度={Length}mm, 速度={Speed}mm/s, 时间={Time:F3}秒",
                    segment.SegmentId,
                    segment.LengthMm,
                    segment.NominalSpeedMmPerSec,
                    segmentTimeSeconds);
#pragma warning restore CS0618 // Type or member is obsolete
            }

            // 4. 应用安全系数（clamp到0.1-1.0范围）
            var safetyFactor = Math.Clamp(context.SafetyFactor, 0.1m, 1.0m);
            var effectiveTimeoutSeconds = totalTimeSeconds * safetyFactor;

            // 5. 确保有效超时时间大于0且不超过物理极限
            if (effectiveTimeoutSeconds <= 0)
            {
                _logger.LogWarning(
                    "计算出的超时时间为 {Timeout}秒（≤0），使用降级超时时间 {FallbackTimeout}秒",
                    effectiveTimeoutSeconds,
                    options.FallbackTimeoutSeconds);
                return options.FallbackTimeoutSeconds;
            }

            _logger.LogInformation(
                "格口分配超时计算完成: 理论物理极限时间={PhysicalLimit:F3}秒, " +
                "安全系数={SafetyFactor:F2}, 有效超时时间={EffectiveTimeout:F3}秒, " +
                "路径段数={SegmentCount}",
                totalTimeSeconds,
                safetyFactor,
                effectiveTimeoutSeconds,
                pathToFirstWheel.Count);

            return effectiveTimeoutSeconds;
        }
        catch (Exception ex)
        {
            // Fallback to default in case of exception
            var fallbackTimeout = 5m;
            _logger.LogError(ex,
                "计算格口分配超时时间时发生异常，使用降级超时时间 {FallbackTimeout}秒",
                fallbackTimeout);
            return fallbackTimeout;
        }
    }

    /// <summary>
    /// 获取从入口到第一个摆轮前决策传感器的路径
    /// </summary>
    private IReadOnlyList<LineSegmentConfig>? GetPathToFirstDecisionPoint(LineTopologyConfig topology)
    {
        // 获取按位置索引排序的摆轮节点
        var sortedNodes = topology.WheelNodes.OrderBy(n => n.PositionIndex).ToList();
        if (sortedNodes.Count == 0)
        {
            _logger.LogError("线体拓扑配置中没有摆轮节点");
            return null;
        }

        // 第一个摆轮节点即为决策点
        var firstWheel = sortedNodes[0];
        var path = new List<LineSegmentConfig>();

#pragma warning disable CS0618 // Type or member is obsolete - backward compatibility
        // 从入口到第一个摆轮的路径
        var currentNodeId = LineTopologyConfig.EntryNodeId;
        
        // 遍历直到第一个摆轮
        foreach (var node in sortedNodes)
        {
            if (node.PositionIndex > firstWheel.PositionIndex)
            {
                break;
            }

            var segment = topology.FindSegment(currentNodeId, node.NodeId);
#pragma warning restore CS0618 // Type or member is obsolete
            if (segment == null)
            {
                _logger.LogError(
                    "无法找到从 {From} 到 {To} 的线体段，路径不完整",
                    currentNodeId,
                    node.NodeId);
                return null;
            }

            path.Add(segment);
            currentNodeId = node.NodeId;

            // 只添加到第一个摆轮为止
            if (node.NodeId == firstWheel.NodeId)
            {
                break;
            }
        }

        if (path.Count == 0)
        {
            _logger.LogError("未找到从入口到第一个摆轮的有效路径");
            return null;
        }

        return path;
    }
}
