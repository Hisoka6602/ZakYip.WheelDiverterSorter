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
                    "请检查线体拓扑配置：确保线体段配置正确。",
                    options.FallbackTimeoutSeconds);
                return options.FallbackTimeoutSeconds;
            }

            // 3. 计算理论物理极限时间（T_firstWheel）
            var totalTimeSeconds = 0.0m;
            foreach (var segment in pathToFirstWheel)
            {
                if (segment.SpeedMmPerSec <= 0)
                {
                    _logger.LogError(
                        "线体段 {SegmentId} 的速度配置无效（{Speed} mm/s），使用降级超时时间 {FallbackTimeout}秒。" +
                        "请检查线体段速度配置，速度必须大于0。",
                        segment.SegmentId,
                        segment.SpeedMmPerSec,
                        options.FallbackTimeoutSeconds);
                    return options.FallbackTimeoutSeconds;
                }

                // 计算每段的时间：时间（秒） = 长度（mm） / 速度（mm/s）
                var segmentTimeSeconds = (decimal)(segment.LengthMm / segment.SpeedMmPerSec);
                totalTimeSeconds += segmentTimeSeconds;

                _logger.LogTrace(
                    "线体段 {SegmentId}: 长度={Length}mm, 速度={Speed}mm/s, 时间={Time:F3}秒",
                    segment.SegmentId,
                    segment.LengthMm,
                    segment.SpeedMmPerSec,
                    segmentTimeSeconds);
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
        
        // 获取第一个摆轮的前置IO（FrontIoId）
        if (firstWheel.FrontIoId == null || firstWheel.FrontIoId == 0)
        {
            _logger.LogError("第一个摆轮节点 {NodeId} 未配置摆轮前感应IO (FrontIoId)", firstWheel.NodeId);
            return null;
        }

        // 找到第一段线体的起点IO（应该是ParcelCreation类型）
        if (topology.LineSegments.Count == 0)
        {
            _logger.LogError("线体拓扑配置中没有线体段");
            return null;
        }

        var firstSegment = topology.LineSegments.FirstOrDefault();
        if (firstSegment == null)
        {
            _logger.LogError("无法找到第一段线体");
            return null;
        }

        var startIoId = firstSegment.StartIoId;
        var targetIoId = firstWheel.FrontIoId.Value;

        // 获取从起点IO到第一个摆轮前IO的路径
        var path = topology.GetPathBetweenIos(startIoId, targetIoId);
        if (path == null || path.Count == 0)
        {
            _logger.LogError(
                "无法找到从起点IO {StartIoId} 到摆轮前IO {EndIoId} 的路径",
                startIoId,
                targetIoId);
            return null;
        }

        return path;
    }
}
