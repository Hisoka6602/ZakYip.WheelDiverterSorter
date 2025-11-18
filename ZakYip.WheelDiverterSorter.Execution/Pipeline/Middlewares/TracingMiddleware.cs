using Microsoft.Extensions.Logging;
using ZakYip.Sorting.Core.Events;
using ZakYip.Sorting.Core.Pipeline;
using ZakYip.WheelDiverterSorter.Core.Tracing;

namespace ZakYip.WheelDiverterSorter.Execution.Pipeline.Middlewares;

/// <summary>
/// 追踪中间件，负责在流水线的关键节点记录追踪日志
/// </summary>
public sealed class TracingMiddleware : ISortingPipelineMiddleware
{
    private readonly IParcelTraceSink? _traceSink;
    private readonly ILogger<TracingMiddleware>? _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    public TracingMiddleware(IParcelTraceSink? traceSink = null, ILogger<TracingMiddleware>? logger = null)
    {
        _traceSink = traceSink;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async ValueTask InvokeAsync(SortingPipelineContext context, Func<SortingPipelineContext, ValueTask> next)
    {
        // 记录进入中间件
        _logger?.LogDebug("包裹 {ParcelId} 进入追踪中间件", context.ParcelId);

        // 记录当前阶段
        await WriteTraceAsync(new ParcelTraceEventArgs
        {
            ItemId = context.ParcelId,
            BarCode = context.Barcode,
            OccurredAt = DateTimeOffset.UtcNow,
            Stage = context.CurrentStage,
            Source = "Pipeline",
            Details = $"进入阶段: {context.CurrentStage}"
        });

        // 执行后续中间件
        await next(context);

        // 记录完成阶段
        await WriteTraceAsync(new ParcelTraceEventArgs
        {
            ItemId = context.ParcelId,
            BarCode = context.Barcode,
            OccurredAt = DateTimeOffset.UtcNow,
            Stage = context.CurrentStage,
            Source = "Pipeline",
            Details = $"完成阶段: {context.CurrentStage}, 成功: {context.IsSuccess}"
        });

        _logger?.LogDebug("包裹 {ParcelId} 完成追踪中间件", context.ParcelId);
    }

    private async ValueTask WriteTraceAsync(ParcelTraceEventArgs eventArgs)
    {
        if (_traceSink != null)
        {
            await _traceSink.WriteAsync(eventArgs);
        }
    }
}
