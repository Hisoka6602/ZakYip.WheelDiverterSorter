using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Sorting.Pipeline;
using ZakYip.WheelDiverterSorter.Core.LineModel.Tracing;

namespace ZakYip.WheelDiverterSorter.Execution.Pipeline.Middlewares;

/// <summary>
/// 上游分配中间件的委托，负责获取目标格口
/// </summary>
public delegate Task<(int? ChuteId, double LatencyMs, string Status, string Source)> UpstreamAssignmentDelegate(long parcelId);

/// <summary>
/// 上游分配中间件，负责从上游系统获取目标格口
/// </summary>
public sealed class UpstreamAssignmentMiddleware : ISortingPipelineMiddleware
{
    private readonly UpstreamAssignmentDelegate _assignmentDelegate;
    private readonly IParcelTraceSink? _traceSink;
    private readonly ILogger<UpstreamAssignmentMiddleware>? _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    public UpstreamAssignmentMiddleware(
        UpstreamAssignmentDelegate assignmentDelegate,
        IParcelTraceSink? traceSink = null,
        ILogger<UpstreamAssignmentMiddleware>? logger = null)
    {
        _assignmentDelegate = assignmentDelegate ?? throw new ArgumentNullException(nameof(assignmentDelegate));
        _traceSink = traceSink;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async ValueTask InvokeAsync(SortingPipelineContext context, Func<SortingPipelineContext, ValueTask> next)
    {
        context.CurrentStage = "UpstreamAssignment";
        _logger?.LogDebug("包裹 {ParcelId} 开始上游分配", context.ParcelId);

        try
        {
            // 调用委托获取目标格口
            var (chuteId, latencyMs, status, source) = await _assignmentDelegate(context.ParcelId);

            if (!chuteId.HasValue || chuteId.Value <= 0)
            {
                _logger?.LogWarning("包裹 {ParcelId} 未能确定有效的目标格口", context.ParcelId);
                context.ShouldForceException = true;
                context.ExceptionReason = "NoValidTargetChute";
                context.ExceptionType = "UpstreamTimeout";
            }
            else
            {
                context.TargetChuteId = chuteId.Value;
            }

            context.UpstreamLatencyMs = latencyMs;

            // 记录上游分配事件
            await WriteTraceAsync(new ParcelTraceEventArgs
            {
                ItemId = context.ParcelId,
                BarCode = context.Barcode,
                OccurredAt = DateTimeOffset.UtcNow,
                Stage = "UpstreamAssigned",
                Source = source,
                Details = $"ChuteId={chuteId}, LatencyMs={latencyMs:F0}, Status={status}"
            });

            _logger?.LogDebug("包裹 {ParcelId} 完成上游分配，目标格口: {ChuteId}", context.ParcelId, chuteId);

            // 执行后续中间件
            await next(context);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "包裹 {ParcelId} 上游分配失败", context.ParcelId);
            context.ShouldForceException = true;
            context.ExceptionReason = $"UpstreamAssignmentError: {ex.Message}";
            context.ExceptionType = "UpstreamTimeout";

            // 继续执行后续中间件
            await next(context);
        }
    }

    private async ValueTask WriteTraceAsync(ParcelTraceEventArgs eventArgs)
    {
        if (_traceSink != null)
        {
            await _traceSink.WriteAsync(eventArgs);
        }
    }
}
