using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Sorting.Contracts;
using ZakYip.WheelDiverterSorter.Core.Sorting.Pipeline;
using ZakYip.WheelDiverterSorter.Core.Tracing;
using ZakYip.WheelDiverterSorter.Execution.Pipeline.Middlewares;
using ZakYip.WheelDiverterSorter.Ingress.Upstream;

namespace ZakYip.WheelDiverterSorter.Host.Pipeline;

/// <summary>
/// 上游分配中间件（新版本），使用 IUpstreamFacade 进行上游通信
/// </summary>
public sealed class UpstreamFacadeAssignmentMiddleware : ISortingPipelineMiddleware
{
    private readonly IUpstreamFacade _upstreamFacade;
    private readonly IParcelTraceSink? _traceSink;
    private readonly ILogger<UpstreamFacadeAssignmentMiddleware>? _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    public UpstreamFacadeAssignmentMiddleware(
        IUpstreamFacade upstreamFacade,
        IParcelTraceSink? traceSink = null,
        ILogger<UpstreamFacadeAssignmentMiddleware>? logger = null)
    {
        _upstreamFacade = upstreamFacade ?? throw new ArgumentNullException(nameof(upstreamFacade));
        _traceSink = traceSink;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async ValueTask InvokeAsync(SortingPipelineContext context, Func<SortingPipelineContext, ValueTask> next)
    {
        context.CurrentStage = "UpstreamAssignment";
        _logger?.LogDebug("包裹 {ParcelId} 开始上游分配（使用 UpstreamFacade）", context.ParcelId);

        try
        {
            // 构造请求
            var request = new AssignChuteRequest
            {
                ParcelId = context.ParcelId,
                Barcode = context.Barcode,
                RequestTime = DateTimeOffset.UtcNow
            };

            // 调用上游门面获取目标格口
            var result = await _upstreamFacade.AssignChuteAsync(request);

            if (result.IsSuccess && result.Data != null && result.Data.ChuteId > 0)
            {
                context.TargetChuteId = result.Data.ChuteId;
                context.UpstreamLatencyMs = result.LatencyMs;

                // 记录上游分配事件
                await WriteTraceAsync(new ParcelTraceEventArgs
                {
                    ItemId = context.ParcelId,
                    BarCode = context.Barcode,
                    OccurredAt = DateTimeOffset.UtcNow,
                    Stage = "UpstreamAssigned",
                    Source = result.Source ?? "Unknown",
                    Details = $"ChuteId={result.Data.ChuteId}, LatencyMs={result.LatencyMs:F0}, " +
                             $"IsFallback={result.IsFallback}"
                });

                _logger?.LogDebug(
                    "包裹 {ParcelId} 完成上游分配，目标格口: {ChuteId}，来源: {Source}，降级: {IsFallback}",
                    context.ParcelId,
                    result.Data.ChuteId,
                    result.Source,
                    result.IsFallback);
            }
            else
            {
                // 上游分配失败
                _logger?.LogWarning(
                    "包裹 {ParcelId} 上游分配失败: {ErrorMessage}",
                    context.ParcelId,
                    result.ErrorMessage);

                context.ShouldForceException = true;
                context.ExceptionReason = result.ErrorMessage ?? "UpstreamAssignmentFailed";
                context.ExceptionType = "UpstreamTimeout";

                await WriteTraceAsync(new ParcelTraceEventArgs
                {
                    ItemId = context.ParcelId,
                    BarCode = context.Barcode,
                    OccurredAt = DateTimeOffset.UtcNow,
                    Stage = "UpstreamAssignmentFailed",
                    Source = result.Source ?? "Unknown",
                    Details = $"ErrorCode={result.ErrorCode}, ErrorMessage={result.ErrorMessage}"
                });
            }

            // 执行后续中间件
            await next(context);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "包裹 {ParcelId} 上游分配异常", context.ParcelId);
            context.ShouldForceException = true;
            context.ExceptionReason = $"UpstreamAssignmentException: {ex.Message}";
            context.ExceptionType = "UpstreamTimeout";

            await WriteTraceAsync(new ParcelTraceEventArgs
            {
                ItemId = context.ParcelId,
                BarCode = context.Barcode,
                OccurredAt = DateTimeOffset.UtcNow,
                Stage = "UpstreamAssignmentException",
                Source = "Local",
                Details = $"Exception={ex.GetType().Name}, Message={ex.Message}"
            });

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
