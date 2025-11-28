using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.LineModel;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;
using ZakYip.WheelDiverterSorter.Core.Enums;

using ZakYip.WheelDiverterSorter.Core.LineModel.Orchestration;
using ZakYip.WheelDiverterSorter.Core.LineModel.Topology;
using ZakYip.WheelDiverterSorter.Core.Utilities;

namespace ZakYip.WheelDiverterSorter.Execution.Routing;

/// <summary>
/// 路径重规划服务实现
/// Path rerouting service implementation
/// </summary>
/// <remarks>
/// 当路径段失败时，尝试从后续节点重新生成路径到目标格口。
/// 简单策略：查找失败节点后续的节点，判断是否能到达目标格口。
/// </remarks>
public class PathReroutingService : IPathReroutingService
{
    private readonly IRouteConfigurationRepository _routeRepository;
    private readonly ISwitchingPathGenerator _pathGenerator;
    private readonly ILogger<PathReroutingService> _logger;
    private readonly ISystemClock _clock;

    /// <summary>
    /// 初始化路径重规划服务
    /// </summary>
    /// <param name="routeRepository">路由配置仓储</param>
    /// <param name="pathGenerator">路径生成器</param>
    /// <param name="logger">日志记录器</param>
    /// <param name="clock">系统时钟</param>
    public PathReroutingService(
        IRouteConfigurationRepository routeRepository,
        ISwitchingPathGenerator pathGenerator,
        ILogger<PathReroutingService> logger,
        ISystemClock clock)
    {
        _routeRepository = routeRepository ?? throw new ArgumentNullException(nameof(routeRepository));
        _pathGenerator = pathGenerator ?? throw new ArgumentNullException(nameof(pathGenerator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <inheritdoc/>
    public Task<ReroutingResult> TryRerouteAsync(
        long parcelId,
        SwitchingPath currentPath,
        long failedNodeId,
        PathFailureReason failureReason,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "尝试为包裹 {ParcelId} 重规划路径，失败节点: {FailedNodeId}，失败原因: {FailureReason}，目标格口: {TargetChuteId}",
                parcelId, failedNodeId, failureReason, currentPath.TargetChuteId);

            // 某些失败类型不适合重规划，直接返回失败
            if (IsRerouteInappropriate(failureReason))
            {
                var reason = $"失败类型 {failureReason} 不适合重规划";
                _logger.LogWarning(
                    "包裹 {ParcelId} 不适合重规划: {Reason}",
                    parcelId, reason);

                return Task.FromResult(ReroutingResult.Failure(
                    parcelId,
                    currentPath.TargetChuteId,
                    failedNodeId,
                    reason));
            }

            // 查找失败节点在原路径中的位置
            var failedSegmentIndex = FindFailedSegmentIndex(currentPath, failedNodeId);
            if (failedSegmentIndex < 0)
            {
                var reason = $"未在路径中找到失败节点 {failedNodeId}";
                _logger.LogWarning(
                    "包裹 {ParcelId} 重规划失败: {Reason}",
                    parcelId, reason);

                return Task.FromResult(ReroutingResult.Failure(
                    parcelId,
                    currentPath.TargetChuteId,
                    failedNodeId,
                    reason));
            }

            // 获取目标格口的路由配置
            var routeConfig = _routeRepository.GetByChuteId(currentPath.TargetChuteId);
            if (routeConfig == null || routeConfig.DiverterConfigurations.Count == 0)
            {
                var reason = $"未找到格口 {currentPath.TargetChuteId} 的路由配置";
                _logger.LogWarning(
                    "包裹 {ParcelId} 重规划失败: {Reason}",
                    parcelId, reason);

                return Task.FromResult(ReroutingResult.Failure(
                    parcelId,
                    currentPath.TargetChuteId,
                    failedNodeId,
                    reason));
            }

            // 查找后续节点中是否有能到达目标格口的节点
            var remainingSegments = currentPath.Segments
                .Skip(failedSegmentIndex + 1)
                .ToList();

            if (remainingSegments.Count == 0)
            {
                var reason = "失败节点后没有剩余节点可用于重规划";
                _logger.LogWarning(
                    "包裹 {ParcelId} 重规划失败: {Reason}",
                    parcelId, reason);

                return Task.FromResult(ReroutingResult.Failure(
                    parcelId,
                    currentPath.TargetChuteId,
                    failedNodeId,
                    reason));
            }

            // 检查剩余路径段中是否包含目标格口所需的关键节点
            var rerouteSegments = TryGenerateRerouteSegments(
                routeConfig.DiverterConfigurations,
                remainingSegments,
                currentPath.TargetChuteId);

            if (rerouteSegments == null || rerouteSegments.Count == 0)
            {
                var reason = "剩余节点无法构成到达目标格口的完整路径";
                _logger.LogWarning(
                    "包裹 {ParcelId} 重规划失败: {Reason}",
                    parcelId, reason);

                return Task.FromResult(ReroutingResult.Failure(
                    parcelId,
                    currentPath.TargetChuteId,
                    failedNodeId,
                    reason));
            }

            // 生成新路径
            var newPath = new SwitchingPath
            {
                TargetChuteId = currentPath.TargetChuteId,
                Segments = rerouteSegments,
                GeneratedAt = _clock.LocalNowOffset,
                FallbackChuteId = currentPath.FallbackChuteId
            };

            _logger.LogInformation(
                "包裹 {ParcelId} 重规划成功，新路径包含 {SegmentCount} 个节点",
                parcelId, newPath.Segments.Count);

            return Task.FromResult(ReroutingResult.Success(
                parcelId,
                currentPath.TargetChuteId,
                newPath));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "包裹 {ParcelId} 重规划过程中发生异常",
                parcelId);

            return Task.FromResult(ReroutingResult.Failure(
                parcelId,
                currentPath.TargetChuteId,
                failedNodeId,
                $"重规划异常: {ex.Message}"));
        }
    }

    /// <summary>
    /// 判断失败类型是否不适合重规划
    /// </summary>
    private bool IsRerouteInappropriate(PathFailureReason failureReason)
    {
        return failureReason switch
        {
            PathFailureReason.PhysicalConstraint => true,  // 物理约束无法通过重规划解决
            PathFailureReason.ParcelDropout => true,       // 包裹已丢失，无法重规划
            _ => false
        };
    }

    /// <summary>
    /// 在路径中查找失败节点的索引
    /// </summary>
    private int FindFailedSegmentIndex(SwitchingPath path, long failedNodeId)
    {
        for (int i = 0; i < path.Segments.Count; i++)
        {
            if (path.Segments[i].DiverterId == failedNodeId)
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>
    /// 尝试从剩余路径段生成重规划路径
    /// </summary>
    private IReadOnlyList<SwitchingPathSegment>? TryGenerateRerouteSegments(
        List<DiverterConfigurationEntry> diverterConfigurations,
        IReadOnlyList<SwitchingPathSegment> remainingSegments,
        long targetChuteId)
    {
        // 获取目标格口所需的所有摆轮配置
        var requiredDiverters = diverterConfigurations
            .OrderBy(c => c.SequenceNumber)
            .ToList();

        // 查找剩余段中的哪些摆轮是到达目标格口所必需的
        var rerouteSegments = new List<SwitchingPathSegment>();
        
        foreach (var requiredConfig in requiredDiverters)
        {
            var matchingSegment = remainingSegments.FirstOrDefault(
                s => s.DiverterId == requiredConfig.DiverterId);

            if (matchingSegment != null)
            {
                // 使用原配置的方向和TTL
                rerouteSegments.Add(new SwitchingPathSegment
                {
                    SequenceNumber = rerouteSegments.Count + 1,
                    DiverterId = requiredConfig.DiverterId,
                    TargetDirection = requiredConfig.TargetDirection,
                    TtlMilliseconds = matchingSegment.TtlMilliseconds
                });
            }
        }

        // 如果重规划的路径包含了所有必需的摆轮，则认为可以重规划
        // 注意：这是一个简化的策略，实际中可能需要更复杂的拓扑验证
        if (rerouteSegments.Count == requiredDiverters.Count)
        {
            return rerouteSegments.AsReadOnly();
        }

        return null;
    }
}
