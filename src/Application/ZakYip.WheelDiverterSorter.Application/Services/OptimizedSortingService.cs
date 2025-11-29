using System.Diagnostics;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Execution;
using ZakYip.WheelDiverterSorter.Core.Sorting.Orchestration;
using ZakYip.WheelDiverterSorter.Observability;

namespace ZakYip.WheelDiverterSorter.Application.Services;

/// <summary>
/// 性能优化的分拣服务（委托到 ISortingOrchestrator）
/// </summary>
/// <remarks>
/// PR-SORT2：Application 层的包装服务，只做指标记录和结果转换。
/// 所有分拣业务逻辑统一在 SortingOrchestrator 中实现。
/// </remarks>
public class OptimizedSortingService
{
    private readonly ISortingOrchestrator _orchestrator;
    private readonly SorterMetrics _metrics;
    private readonly AlarmService? _alarmService;
    private readonly ILogger<OptimizedSortingService> _logger;

    public OptimizedSortingService(
        ISortingOrchestrator orchestrator,
        SorterMetrics metrics,
        ILogger<OptimizedSortingService> logger,
        AlarmService? alarmService = null)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _alarmService = alarmService;
    }

    /// <summary>
    /// 执行分拣操作（带性能监控）
    /// </summary>
    public async Task<PathExecutionResult> SortParcelAsync(
        string parcelId,
        int targetChuteId,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        _metrics.RecordSortingRequest();

        try
        {
            var result = await _orchestrator.ExecuteDebugSortAsync(parcelId, targetChuteId, cancellationToken);
            stopwatch.Stop();

            if (result.IsSuccess)
            {
                _metrics.RecordSortingSuccess(stopwatch.Elapsed.TotalMilliseconds);
                _alarmService?.RecordSortingSuccess();
            }
            else
            {
                _metrics.RecordSortingFailure(stopwatch.Elapsed.TotalMilliseconds);
                _alarmService?.RecordSortingFailure();
            }

            return new PathExecutionResult
            {
                IsSuccess = result.IsSuccess,
                ActualChuteId = result.ActualChuteId,
                FailureReason = result.FailureReason
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _metrics.RecordSortingFailure(stopwatch.Elapsed.TotalMilliseconds);
            _alarmService?.RecordSortingFailure();
            _logger.LogError(ex, "分拣异常: ParcelId={ParcelId}, ChuteId={ChuteId}", parcelId, targetChuteId);

            return new PathExecutionResult
            {
                IsSuccess = false,
                ActualChuteId = 0,
                FailureReason = $"执行异常: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// 批量分拣
    /// </summary>
    public async Task<List<PathExecutionResult>> SortBatchAsync(
        IEnumerable<(string ParcelId, int ChuteId)> parcels,
        CancellationToken cancellationToken = default)
    {
        var tasks = parcels.Select(p => SortParcelAsync(p.ParcelId, p.ChuteId, cancellationToken)).ToList();
        var results = await Task.WhenAll(tasks);
        return results.ToList();
    }
}
