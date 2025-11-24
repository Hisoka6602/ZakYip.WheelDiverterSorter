using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.LineModel;
using ZakYip.WheelDiverterSorter.Core.LineModel.Topology;
using ZakYip.WheelDiverterSorter.Core.LineModel.Tracing;
using ZakYip.WheelDiverterSorter.Core.Sorting.Models;
using ZakYip.WheelDiverterSorter.Core.Sorting.Overload;
using ZakYip.WheelDiverterSorter.Core.Sorting.Pipeline;
using ZakYip.WheelDiverterSorter.Core.Utilities;

namespace ZakYip.WheelDiverterSorter.Execution.Pipeline.Middlewares;

/// <summary>
/// 超载评估委托
/// </summary>
public delegate Task<(CongestionLevel Level, OverloadDecision? Decision)> OverloadEvaluationDelegate(
    long parcelId, 
    long? targetChuteId, 
    SwitchingPath? plannedPath,
    string stage);

/// <summary>
/// 超载评估中间件，负责检查包裹是否需要超载处理
/// </summary>
public sealed class OverloadEvaluationMiddleware : ISortingPipelineMiddleware
{
    private readonly OverloadEvaluationDelegate? _evaluationDelegate;
    private readonly IParcelTraceSink? _traceSink;
    private readonly ILogger<OverloadEvaluationMiddleware>? _logger;
    private readonly ISystemClock _clock;

    /// <summary>
    /// 构造函数
    /// </summary>
    public OverloadEvaluationMiddleware(
        ISystemClock clock,
        OverloadEvaluationDelegate? evaluationDelegate = null,
        IParcelTraceSink? traceSink = null,
        ILogger<OverloadEvaluationMiddleware>? logger = null)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _evaluationDelegate = evaluationDelegate;
        _traceSink = traceSink;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async ValueTask InvokeAsync(SortingPipelineContext context, Func<SortingPipelineContext, ValueTask> next)
    {
        context.CurrentStage = "OverloadEvaluation";
        _logger?.LogDebug("包裹 {ParcelId} 开始超载评估", context.ParcelId);

        // 如果已经标记为强制异常或没有评估委托，跳过超载评估
        if (context.ShouldForceException || _evaluationDelegate == null)
        {
            _logger?.LogDebug("包裹 {ParcelId} 跳过超载评估", context.ParcelId);
            await next(context);
            return;
        }

        try
        {
            // 入口超载检查
            var (congestionLevel, overloadDecision) = await _evaluationDelegate(
                context.ParcelId, 
                context.TargetChuteId, 
                context.PlannedPath,
                "Entry");

            if (overloadDecision?.ShouldForceException == true)
            {
                _logger?.LogWarning("包裹 {ParcelId} 触发超载策略，原因：{Reason}，拥堵等级：{CongestionLevel}",
                    context.ParcelId, overloadDecision.Value.Reason, congestionLevel);

                // 记录超载决策事件
                await WriteTraceAsync(new ParcelTraceEventArgs
                {
                    ItemId = context.ParcelId,
                    BarCode = context.Barcode,
                    TargetChuteId = context.TargetChuteId,
                    OccurredAt = _clock.LocalNowOffset,
                    Stage = "OverloadEvaluated",
                    Source = "OverloadPolicy",
                    Details = $"Reason={overloadDecision.Value.Reason}, CongestionLevel={congestionLevel}, ForceException=true"
                });

                context.ShouldForceException = true;
                context.ExceptionReason = overloadDecision.Value.Reason ?? "Overload";
                context.ExceptionType = ExceptionType.Overload;
            }
            else if (overloadDecision?.ShouldMarkAsOverflow == true)
            {
                _logger?.LogInformation("包裹 {ParcelId} 标记为潜在超载风险。原因：{Reason}",
                    context.ParcelId, overloadDecision.Value.Reason);
            }

            _logger?.LogDebug("包裹 {ParcelId} 完成超载评估", context.ParcelId);

            // 执行后续中间件
            await next(context);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "包裹 {ParcelId} 超载评估失败", context.ParcelId);
            // 不标记为异常，继续执行
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
