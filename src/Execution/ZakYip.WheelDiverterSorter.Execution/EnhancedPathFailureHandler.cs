using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.LineModel;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Execution.Events;
using ZakYip.WheelDiverterSorter.Observability;

using ZakYip.WheelDiverterSorter.Core.LineModel.Orchestration;
using ZakYip.WheelDiverterSorter.Core.LineModel.Topology;
using ZakYip.WheelDiverterSorter.Core.Utilities;
namespace ZakYip.WheelDiverterSorter.Execution;

/// <summary>
/// 增强的路径失败处理器，支持路径重规划
/// Enhanced path failure handler with rerouting support
/// </summary>
/// <remarks>
/// PR-07 增强：在路径失败时，尝试从后续节点重规划路径，
/// 如果重规划成功，则继续执行新路径；
/// 如果重规划失败，则退回到异常格口。
/// </remarks>
public class EnhancedPathFailureHandler : IPathFailureHandler
{
    private readonly ISwitchingPathGenerator _pathGenerator;
    private readonly IPathReroutingService? _reroutingService;
    private readonly PrometheusMetrics? _metrics;
    private readonly ILogger<EnhancedPathFailureHandler> _logger;
    private readonly ISystemClock _clock;

    /// <inheritdoc/>
    public event EventHandler<PathSegmentExecutionFailedEventArgs>? SegmentExecutionFailed;

    /// <inheritdoc/>
    public event EventHandler<PathExecutionFailedEventArgs>? PathExecutionFailed;

    /// <inheritdoc/>
    public event EventHandler<PathSwitchedEventArgs>? PathSwitched;

    /// <summary>
    /// 重规划成功事件
    /// Rerouting succeeded event
    /// </summary>
    public event EventHandler<ReroutingSucceededEventArgs>? ReroutingSucceeded;

    /// <summary>
    /// 重规划失败事件
    /// Rerouting failed event
    /// </summary>
    public event EventHandler<ReroutingFailedEventArgs>? ReroutingFailed;

    /// <summary>
    /// 初始化增强的路径失败处理器
    /// </summary>
    /// <param name="pathGenerator">路径生成器，用于生成备用路径</param>
    /// <param name="logger">日志记录器</param>
    /// <param name="clock">系统时钟</param>
    /// <param name="reroutingService">路径重规划服务（可选）</param>
    /// <param name="metrics">Prometheus指标服务（可选）</param>
    public EnhancedPathFailureHandler(
        ISwitchingPathGenerator pathGenerator,
        ILogger<EnhancedPathFailureHandler> logger,
        ISystemClock clock,
        IPathReroutingService? reroutingService = null,
        PrometheusMetrics? metrics = null)
    {
        _pathGenerator = pathGenerator ?? throw new ArgumentNullException(nameof(pathGenerator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _reroutingService = reroutingService;
        _metrics = metrics;
    }

    /// <inheritdoc/>
    public void HandleSegmentFailure(
        long parcelId,
        SwitchingPath originalPath,
        SwitchingPathSegment failedSegment,
        string failureReason)
    {
        var failureTime = _clock.LocalNowOffset;

        // 解析失败原因为枚举类型
        var failureReasonEnum = ParseFailureReason(failureReason);

        _logger.LogWarning(
            "路径段执行失败: ParcelId={ParcelId}, 段序号={SequenceNumber}, " +
            "摆轮={DiverterId}, 原因={FailureReason}",
            parcelId,
            failedSegment.SequenceNumber,
            failedSegment.DiverterId,
            failureReasonEnum);

        // 记录路径失败指标
        _metrics?.RecordPathFailure(failureReasonEnum.ToString());

        // 触发段失败事件
        SegmentExecutionFailed?.Invoke(this, new PathSegmentExecutionFailedEventArgs
        {
            ParcelId = parcelId,
            FailedSegment = failedSegment,
            OriginalTargetChuteId = originalPath.TargetChuteId,
            FailureReason = failureReason,
            FailureTime = failureTime
        });

        // 尝试重规划（如果服务可用）
        var reroutingAttempted = TryRerouteAsync(
            parcelId,
            originalPath,
            failedSegment.DiverterId,
            failureReasonEnum).GetAwaiter().GetResult();

        // 如果重规划失败或未尝试，则触发路径失败事件
        if (!reroutingAttempted)
        {
            HandlePathFailure(parcelId, originalPath, failureReason, failedSegment);
        }
    }

    /// <inheritdoc/>
    public void HandlePathFailure(
        long parcelId,
        SwitchingPath originalPath,
        string failureReason,
        SwitchingPathSegment? failedSegment = null)
    {
        var failureTime = _clock.LocalNowOffset;

        _logger.LogError(
            "路径执行失败: ParcelId={ParcelId}, 原始目标格口={TargetChuteId}, " +
            "失败原因={FailureReason}, 将切换到异常格口={FallbackChuteId}",
            parcelId,
            originalPath.TargetChuteId,
            failureReason,
            originalPath.FallbackChuteId);

        // 触发路径失败事件
        PathExecutionFailed?.Invoke(this, new PathExecutionFailedEventArgs
        {
            ParcelId = parcelId,
            OriginalPath = originalPath,
            FailedSegment = failedSegment,
            FailureReason = failureReason,
            FailureTime = failureTime,
            ActualChuteId = originalPath.FallbackChuteId
        });

        // 计算并记录备用路径切换
        var backupPath = CalculateBackupPath(originalPath);
        if (backupPath != null)
        {
            _logger.LogInformation(
                "已计算备用路径: ParcelId={ParcelId}, 目标格口={BackupChuteId}, " +
                "路径段数={SegmentCount}",
                parcelId,
                backupPath.TargetChuteId,
                backupPath.Segments.Count);

            // 触发路径切换事件
            PathSwitched?.Invoke(this, new PathSwitchedEventArgs
            {
                ParcelId = parcelId,
                OriginalPath = originalPath,
                BackupPath = backupPath,
                SwitchReason = failureReason,
                SwitchTime = failureTime
            });
        }
        else
        {
            _logger.LogError(
                "无法生成备用路径: ParcelId={ParcelId}, 异常格口={FallbackChuteId}",
                parcelId,
                originalPath.FallbackChuteId);
        }
    }

    /// <inheritdoc/>
    public SwitchingPath? CalculateBackupPath(SwitchingPath originalPath)
    {
        // 备用路径就是到异常格口（FallbackChuteId）的路径
        var fallbackChuteId = originalPath.FallbackChuteId;

        _logger.LogDebug(
            "计算备用路径: 目标异常格口={FallbackChuteId}",
            fallbackChuteId);

        // 使用路径生成器生成到异常格口的路径
        var backupPath = _pathGenerator.GeneratePath(fallbackChuteId);

        if (backupPath == null)
        {
            _logger.LogWarning(
                "无法生成到异常格口 {FallbackChuteId} 的路径",
                fallbackChuteId);
        }

        return backupPath;
    }

    /// <summary>
    /// 尝试重规划路径
    /// </summary>
    private async Task<bool> TryRerouteAsync(
        long parcelId,
        SwitchingPath originalPath,
        long failedNodeId,
        PathFailureReason failureReason)
    {
        if (_reroutingService == null)
        {
            _logger.LogDebug(
                "包裹 {ParcelId} 未配置重规划服务，跳过重规划",
                parcelId);
            return false;
        }

        try
        {
            _logger.LogInformation(
                "包裹 {ParcelId} 尝试重规划路径，失败节点: {FailedNodeId}",
                parcelId, failedNodeId);

            // 记录重规划尝试
            _metrics?.RecordPathReroute();

            var result = await _reroutingService.TryRerouteAsync(
                parcelId,
                originalPath,
                failedNodeId,
                failureReason);

            if (result.IsSuccess && result.NewPath != null)
            {
                _logger.LogInformation(
                    "包裹 {ParcelId} 重规划成功，新路径包含 {SegmentCount} 个节点",
                    parcelId, result.NewPath.Segments.Count);

                // 记录重规划成功
                _metrics?.RecordRerouteSuccess();

                // 触发重规划成功事件
                ReroutingSucceeded?.Invoke(this, new ReroutingSucceededEventArgs
                {
                    ParcelId = parcelId,
                    OriginalPath = originalPath,
                    NewPath = result.NewPath,
                    FailedNodeId = failedNodeId,
                    ReroutedAt = result.ReroutedAt
                });

                return true;
            }
            else
            {
                _logger.LogWarning(
                    "包裹 {ParcelId} 重规划失败: {Reason}",
                    parcelId, result.FailureReason);

                // 触发重规划失败事件
                ReroutingFailed?.Invoke(this, new ReroutingFailedEventArgs
                {
                    ParcelId = parcelId,
                    OriginalPath = originalPath,
                    FailedNodeId = failedNodeId,
                    FailureReason = result.FailureReason ?? "未知原因",
                    ReroutedAt = result.ReroutedAt
                });

                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "包裹 {ParcelId} 重规划过程中发生异常",
                parcelId);
            return false;
        }
    }

    /// <summary>
    /// 解析失败原因字符串为枚举类型
    /// </summary>
    private PathFailureReason ParseFailureReason(string failureReason)
    {
        if (string.IsNullOrWhiteSpace(failureReason))
        {
            return PathFailureReason.Unknown;
        }

        // 尝试直接解析枚举名称
        if (Enum.TryParse<PathFailureReason>(failureReason, true, out var result))
        {
            return result;
        }

        // 根据关键字推断
        var reason = failureReason.ToLowerInvariant();
        if (reason.Contains("sensor") && reason.Contains("timeout"))
            return PathFailureReason.SensorTimeout;
        if (reason.Contains("ttl") || reason.Contains("超时") || reason.Contains("timeout"))
            return PathFailureReason.TtlExpired;
        if (reason.Contains("direction") || reason.Contains("方向"))
            return PathFailureReason.UnexpectedDirection;
        if (reason.Contains("blocked") || reason.Contains("阻塞"))
            return PathFailureReason.UpstreamBlocked;
        if (reason.Contains("diverter") || reason.Contains("摆轮"))
            return PathFailureReason.DiverterFault;
        if (reason.Contains("sensor") || reason.Contains("传感器"))
            return PathFailureReason.SensorFault;
        if (reason.Contains("dropout") || reason.Contains("掉落") || reason.Contains("丢失"))
            return PathFailureReason.ParcelDropout;
        if (reason.Contains("physical") || reason.Contains("物理") || reason.Contains("constraint"))
            return PathFailureReason.PhysicalConstraint;

        return PathFailureReason.Unknown;
    }
}

/// <summary>
/// 重规划成功事件参数
/// </summary>
public class ReroutingSucceededEventArgs : EventArgs
{
    public required long ParcelId { get; init; }
    public required SwitchingPath OriginalPath { get; init; }
    public required SwitchingPath NewPath { get; init; }
    public required long FailedNodeId { get; init; }
    public required DateTimeOffset ReroutedAt { get; init; }
}

/// <summary>
/// 重规划失败事件参数
/// </summary>
public class ReroutingFailedEventArgs : EventArgs
{
    public required long ParcelId { get; init; }
    public required SwitchingPath OriginalPath { get; init; }
    public required long FailedNodeId { get; init; }
    public required string FailureReason { get; init; }
    public required DateTimeOffset ReroutedAt { get; init; }
}
