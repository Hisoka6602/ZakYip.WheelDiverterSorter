using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZakYip.WheelDiverterSorter.Core.Sorting.Events;
using ZakYip.WheelDiverterSorter.Core.Sorting.Pipeline;
using ZakYip.WheelDiverterSorter.Core.LineModel.Enums;
using ZakYip.WheelDiverterSorter.Core.LineModel.Tracing;

namespace ZakYip.WheelDiverterSorter.Execution.Pipeline.Middlewares;

/// <summary>
/// 追踪中间件，负责在流水线的关键节点记录追踪日志
/// </summary>
public sealed class TracingMiddleware : ISortingPipelineMiddleware
{
    private readonly IParcelTraceSink? _traceSink;
    private readonly ILogger<TracingMiddleware>? _logger;
    private readonly DiagnosticsLevel _diagnosticsLevel;
    private readonly double _normalParcelSamplingRate;
    private readonly Random _random = new();

    /// <summary>
    /// 构造函数
    /// </summary>
    public TracingMiddleware(
        IParcelTraceSink? traceSink = null, 
        ILogger<TracingMiddleware>? logger = null,
        IOptions<DiagnosticsOptions>? options = null)
    {
        _traceSink = traceSink;
        _logger = logger;
        var diagnosticsOptions = options?.Value;
        _diagnosticsLevel = diagnosticsOptions?.Level ?? DiagnosticsLevel.Basic;
        _normalParcelSamplingRate = diagnosticsOptions?.NormalParcelSamplingRate ?? 0.1;
    }

    /// <inheritdoc/>
    public async ValueTask InvokeAsync(SortingPipelineContext context, Func<SortingPipelineContext, ValueTask> next)
    {
        // 根据 DiagnosticsLevel 决定是否记录
        if (_diagnosticsLevel == DiagnosticsLevel.None)
        {
            // 关闭诊断，直接执行后续中间件
            await next(context);
            return;
        }

        // 判断是否为异常件或 Overload 件
        var isAbnormal = context.ShouldForceException || 
                         context.ExceptionType != null || 
                         context.ExceptionReason != null;

        // 根据级别和件类型决定是否记录
        bool shouldTrace = _diagnosticsLevel == DiagnosticsLevel.Verbose || 
                          isAbnormal || 
                          (_diagnosticsLevel == DiagnosticsLevel.Basic && ShouldSample());

        if (!shouldTrace)
        {
            // 跳过追踪，执行后续中间件
            await next(context);
            return;
        }

        // 记录进入中间件
        _logger?.LogDebug("包裹 {ParcelId} 进入追踪中间件", context.ParcelId);

        // 记录当前阶段
        await WriteTraceAsync(new ParcelTraceEventArgs
        {
            ItemId = context.ParcelId,
            BarCode = context.Barcode,
            TargetChuteId = context.TargetChuteId,
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
            TargetChuteId = context.TargetChuteId,
            ActualChuteId = context.ActualChuteId,
            OccurredAt = DateTimeOffset.UtcNow,
            Stage = context.CurrentStage,
            Source = "Pipeline",
            Details = $"完成阶段: {context.CurrentStage}, 成功: {context.IsSuccess}"
        });

        _logger?.LogDebug("包裹 {ParcelId} 完成追踪中间件", context.ParcelId);
    }

    private bool ShouldSample()
    {
        // 根据抽样比例决定是否追踪
        return _random.NextDouble() < _normalParcelSamplingRate;
    }

    private async ValueTask WriteTraceAsync(ParcelTraceEventArgs eventArgs)
    {
        if (_traceSink != null)
        {
            await _traceSink.WriteAsync(eventArgs);
        }
    }
}
