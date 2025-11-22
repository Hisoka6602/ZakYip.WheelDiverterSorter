using System.Diagnostics;
using ZakYip.WheelDiverterSorter.Host.Models;
using ZakYip.WheelDiverterSorter.Host.Application.Services;
using ZakYip.WheelDiverterSorter.Observability;

namespace ZakYip.WheelDiverterSorter.Host.Services;

/// <summary>
/// 调试服务，用于测试直线摆轮分拣方案
/// </summary>
/// <remarks>
/// PR-2: 重构为使用 SortingOrchestrator 统一处理分拣逻辑，避免代码重复
/// </remarks>
public class DebugSortService
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

    /// <summary>
    /// 执行调试分拣操作
    /// </summary>
    /// <param name="parcelId">包裹标识</param>
    /// <param name="targetChuteId">目标格口标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>调试分拣响应</returns>
    public async Task<DebugSortResponse> ExecuteDebugSortAsync(
        string parcelId,
        int targetChuteId,
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

            // 转换为 DebugSortResponse 格式
            return new DebugSortResponse
            {
                ParcelId = parcelId,
                TargetChuteId = targetChuteId,
                IsSuccess = result.IsSuccess,
                ActualChuteId = result.ActualChuteId,
                Message = result.IsSuccess
                    ? $"分拣成功：包裹 {parcelId} 已成功分拣到格口 {result.ActualChuteId}"
                    : $"分拣失败：包裹 {parcelId} {(result.FailureReason != null ? $"- {result.FailureReason}" : "")}",
                FailureReason = result.FailureReason,
                PathSegmentCount = 0 // SortingResult 不包含 PathSegmentCount，这里设置为 0
            };
        }
        finally
        {
            _prometheusMetrics.DecrementActiveRequests();
        }
    }
}
