using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.LineModel.Topology;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Observability;

namespace ZakYip.WheelDiverterSorter.Execution;

/// <summary>
/// 统一的路径执行服务实现
/// </summary>
/// <remarks>
/// <para>此实现将分散的路径执行逻辑统一封装，包括：</para>
/// <list type="bullet">
/// <item>通过ISwitchingPathExecutor执行路径段</item>
/// <item>通过IPathFailureHandler处理失败</item>
/// <item>通过PrometheusMetrics采集指标</item>
/// </list>
/// <para>仿真环境和生产环境共用此管线，仅通过注入不同的ISwitchingPathExecutor区分行为。</para>
/// </remarks>
public sealed class PathExecutionService : IPathExecutionService
{
    private readonly ISwitchingPathExecutor _pathExecutor;
    private readonly IPathFailureHandler _pathFailureHandler;
    private readonly PrometheusMetrics? _metrics;
    private readonly ILogger<PathExecutionService> _logger;
    private readonly ISystemClock _clock;

    /// <summary>
    /// 初始化路径执行服务
    /// </summary>
    /// <param name="pathExecutor">路径执行器（真实驱动或仿真驱动）</param>
    /// <param name="pathFailureHandler">路径失败处理器</param>
    /// <param name="clock">系统时钟</param>
    /// <param name="logger">日志记录器</param>
    /// <param name="metrics">Prometheus指标服务（可选）</param>
    public PathExecutionService(
        ISwitchingPathExecutor pathExecutor,
        IPathFailureHandler pathFailureHandler,
        ISystemClock clock,
        ILogger<PathExecutionService> logger,
        PrometheusMetrics? metrics = null)
    {
        _pathExecutor = pathExecutor ?? throw new ArgumentNullException(nameof(pathExecutor));
        _pathFailureHandler = pathFailureHandler ?? throw new ArgumentNullException(nameof(pathFailureHandler));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _metrics = metrics;
    }

    /// <inheritdoc/>
    public async Task<PathExecutionResult> ExecutePathAsync(
        long parcelId,
        SwitchingPath path,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(path);

        var startTime = _clock.LocalNowOffset;

        _logger.LogDebug(
            "开始执行路径: ParcelId={ParcelId}, 目标格口={TargetChuteId}, 段数={SegmentCount}",
            parcelId, path.TargetChuteId, path.Segments.Count);

        try
        {
            // 通过 ISwitchingPathExecutor 执行路径
            var result = await _pathExecutor.ExecuteAsync(path, cancellationToken);

            var elapsedTime = _clock.LocalNowOffset - startTime;
            var elapsedSeconds = elapsedTime.TotalSeconds;

            // 记录路径执行指标
            _metrics?.RecordPathExecution(elapsedSeconds);

            if (result.IsSuccess)
            {
                _logger.LogInformation(
                    "路径执行成功: ParcelId={ParcelId}, 目标格口={TargetChuteId}, 耗时={ElapsedMs}ms",
                    parcelId, path.TargetChuteId, elapsedTime.TotalMilliseconds);
            }
            else
            {
                _logger.LogWarning(
                    "路径执行失败: ParcelId={ParcelId}, 原因={FailureReason}, 实际格口={ActualChuteId}, 耗时={ElapsedMs}ms",
                    parcelId, result.FailureReason, result.ActualChuteId, elapsedTime.TotalMilliseconds);

                // 统一通过 IPathFailureHandler 处理失败
                if (result.FailedSegment != null)
                {
                    _pathFailureHandler.HandleSegmentFailure(
                        parcelId,
                        path,
                        result.FailedSegment,
                        result.FailureReason ?? "未知原因");
                }
                else
                {
                    _pathFailureHandler.HandlePathFailure(
                        parcelId,
                        path,
                        result.FailureReason ?? "未知原因",
                        result.FailedSegment);
                }
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            var elapsedTime = _clock.LocalNowOffset - startTime;

            _logger.LogWarning(
                "路径执行被取消: ParcelId={ParcelId}, 耗时={ElapsedMs}ms",
                parcelId, elapsedTime.TotalMilliseconds);

            var failureReason = "操作被取消";
            
            // 通过 IPathFailureHandler 处理取消情况
            _pathFailureHandler.HandlePathFailure(parcelId, path, failureReason);

            return new PathExecutionResult
            {
                IsSuccess = false,
                ActualChuteId = path.FallbackChuteId,
                FailureReason = failureReason,
                FailureTime = _clock.LocalNowOffset
            };
        }
        catch (Exception ex)
        {
            var elapsedTime = _clock.LocalNowOffset - startTime;

            _logger.LogError(ex,
                "路径执行发生异常: ParcelId={ParcelId}, 耗时={ElapsedMs}ms",
                parcelId, elapsedTime.TotalMilliseconds);

            var failureReason = $"执行异常: {ex.Message}";

            // 通过 IPathFailureHandler 处理异常情况
            _pathFailureHandler.HandlePathFailure(parcelId, path, failureReason);

            return new PathExecutionResult
            {
                IsSuccess = false,
                ActualChuteId = path.FallbackChuteId,
                FailureReason = failureReason,
                FailureTime = _clock.LocalNowOffset
            };
        }
    }
}
