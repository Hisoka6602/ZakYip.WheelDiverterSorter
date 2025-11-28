using System.Diagnostics;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Sorting.Orchestration;
using ZakYip.WheelDiverterSorter.Observability;

namespace ZakYip.WheelDiverterSorter.Application.Services;

/// <summary>
/// 调试分拣服务实现
/// </summary>
/// <remarks>
/// 用于测试直线摆轮分拣方案，提供调试分拣功能
/// </remarks>
public class DebugSortService : IDebugSortService
{
    private readonly ISortingOrchestrator _orchestrator;
    private readonly ILogger<DebugSortService> _logger;
    private readonly PrometheusMetrics _prometheusMetrics;

    public DebugSortService(
        ISortingOrchestrator orchestrator,
        ILogger<DebugSortService> logger,
        PrometheusMetrics prometheusMetrics)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _prometheusMetrics = prometheusMetrics ?? throw new ArgumentNullException(nameof(prometheusMetrics));
    }

    /// <inheritdoc />
    public async Task<DebugSortResult> ExecuteDebugSortAsync(
        string parcelId,
        long targetChuteId,
        CancellationToken cancellationToken = default)
    {
        var overallStopwatch = Stopwatch.StartNew();
        _prometheusMetrics.IncrementActiveRequests();

        try
        {
            _logger.LogInformation("开始调试分拣: 包裹ID={ParcelId}, 目标格口={TargetChuteId}",
                parcelId, targetChuteId);

            // 调用 SortingOrchestrator 统一处理调试分拣
            var result = await _orchestrator.ExecuteDebugSortAsync(parcelId, targetChuteId, cancellationToken);

            overallStopwatch.Stop();

            // 记录指标
            if (result.IsSuccess)
            {
                _prometheusMetrics.RecordSortingSuccess(overallStopwatch.Elapsed.TotalSeconds);
            }
            else
            {
                _prometheusMetrics.RecordSortingFailure(overallStopwatch.Elapsed.TotalSeconds);
            }

            // 转换为 DebugSortResult 格式
            return new DebugSortResult
            {
                ParcelId = parcelId,
                TargetChuteId = targetChuteId,
                IsSuccess = result.IsSuccess,
                ActualChuteId = result.ActualChuteId,
                Message = result.IsSuccess
                    ? $"分拣成功：包裹 {parcelId} 已成功分拣到格口 {result.ActualChuteId}"
                    : $"分拣失败：包裹 {parcelId} {(result.FailureReason != null ? $"- {result.FailureReason}" : "")}",
                FailureReason = result.FailureReason,
                PathSegmentCount = 0 // SortingResult 不包含 PathSegmentCount
            };
        }
        finally
        {
            _prometheusMetrics.DecrementActiveRequests();
        }
    }
}
