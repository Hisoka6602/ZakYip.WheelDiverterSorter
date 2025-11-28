using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.LineModel;
using ZakYip.WheelDiverterSorter.Execution.Events;


using ZakYip.WheelDiverterSorter.Core.LineModel.Topology;
using ZakYip.WheelDiverterSorter.Core.Utilities;

namespace ZakYip.WheelDiverterSorter.Execution.PathExecution;

/// <summary>
/// 路径执行失败处理器实现
/// </summary>
public class PathFailureHandler : IPathFailureHandler
{
    private readonly ISwitchingPathGenerator _pathGenerator;
    private readonly ILogger<PathFailureHandler> _logger;
    private readonly ISystemClock _clock;

    /// <inheritdoc/>
    public event EventHandler<PathSegmentExecutionFailedEventArgs>? SegmentExecutionFailed;

    /// <inheritdoc/>
    public event EventHandler<PathExecutionFailedEventArgs>? PathExecutionFailed;

    /// <inheritdoc/>
    public event EventHandler<PathSwitchedEventArgs>? PathSwitched;

    /// <summary>
    /// 初始化路径失败处理器
    /// </summary>
    /// <param name="pathGenerator">路径生成器，用于生成备用路径</param>
    /// <param name="logger">日志记录器</param>
    /// <param name="clock">系统时钟</param>
    public PathFailureHandler(
        ISwitchingPathGenerator pathGenerator,
        ILogger<PathFailureHandler> logger,
        ISystemClock clock)
    {
        _pathGenerator = pathGenerator ?? throw new ArgumentNullException(nameof(pathGenerator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <inheritdoc/>
    public void HandleSegmentFailure(
        long parcelId,
        SwitchingPath originalPath,
        SwitchingPathSegment failedSegment,
        string failureReason)
    {
        var failureTime = _clock.LocalNowOffset;

        _logger.LogWarning(
            "路径段执行失败: ParcelId={ParcelId}, 段序号={SequenceNumber}, " +
            "摆轮={DiverterId}, 原因={FailureReason}",
            parcelId,
            failedSegment.SequenceNumber,
            failedSegment.DiverterId,
            failureReason);

        // 触发段失败事件
        SegmentExecutionFailed?.Invoke(this, new PathSegmentExecutionFailedEventArgs
        {
            ParcelId = parcelId,
            FailedSegment = failedSegment,
            OriginalTargetChuteId = originalPath.TargetChuteId,
            FailureReason = failureReason,
            FailureTime = failureTime
        });

        // 同时触发路径失败事件
        HandlePathFailure(parcelId, originalPath, failureReason, failedSegment);
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
}
