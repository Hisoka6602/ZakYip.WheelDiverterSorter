using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Core.Sorting.Pipeline;
using ZakYip.WheelDiverterSorter.Core.LineModel;
using ZakYip.WheelDiverterSorter.Core.LineModel.Tracing;
using ZakYip.WheelDiverterSorter.Core.Sorting.Models;
using ZakYip.WheelDiverterSorter.Core.Enums;

namespace ZakYip.WheelDiverterSorter.Execution.Pipeline.Middlewares;

/// <summary>
/// 包裹完成回调委托
/// </summary>
public delegate void ParcelCompletionDelegate(long parcelId, DateTime completedAt, bool isSuccess);

/// <summary>
/// 路径执行中间件，负责执行摆轮路径并处理结果
/// </summary>
/// <remarks>
/// <para>支持两种执行模式：</para>
/// <list type="bullet">
/// <item>使用 IPathExecutionService（推荐）：统一的执行管线，内置失败处理和指标采集</item>
/// <item>使用 ISwitchingPathExecutor（兼容）：直接调用执行器，需要手动处理失败</item>
/// </list>
/// </remarks>
public sealed class PathExecutionMiddleware : ISortingPipelineMiddleware
{
    private readonly IPathExecutionService? _pathExecutionService;
    private readonly ISwitchingPathExecutor? _pathExecutor;
    private readonly IPathFailureHandler? _pathFailureHandler;
    private readonly ParcelCompletionDelegate? _completionDelegate;
    private readonly IParcelTraceSink? _traceSink;
    private readonly ILogger<PathExecutionMiddleware>? _logger;
    private readonly ISystemClock _clock;

    /// <summary>
    /// 构造函数（推荐使用 IPathExecutionService）
    /// </summary>
    public PathExecutionMiddleware(
        IPathExecutionService pathExecutionService,
        ISystemClock clock,
        ParcelCompletionDelegate? completionDelegate = null,
        IParcelTraceSink? traceSink = null,
        ILogger<PathExecutionMiddleware>? logger = null)
    {
        _pathExecutionService = pathExecutionService ?? throw new ArgumentNullException(nameof(pathExecutionService));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _completionDelegate = completionDelegate;
        _traceSink = traceSink;
        _logger = logger;
    }

    /// <summary>
    /// 构造函数（兼容旧接口）
    /// </summary>
    public PathExecutionMiddleware(
        ISwitchingPathExecutor pathExecutor,
        ISystemClock clock,
        IPathFailureHandler? pathFailureHandler = null,
        ParcelCompletionDelegate? completionDelegate = null,
        IParcelTraceSink? traceSink = null,
        ILogger<PathExecutionMiddleware>? logger = null)
    {
        _pathExecutor = pathExecutor ?? throw new ArgumentNullException(nameof(pathExecutor));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _pathFailureHandler = pathFailureHandler;
        _completionDelegate = completionDelegate;
        _traceSink = traceSink;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async ValueTask InvokeAsync(SortingPipelineContext context, Func<SortingPipelineContext, ValueTask> next)
    {
        context.CurrentStage = "Execution";
        _logger?.LogDebug("包裹 {ParcelId} 开始执行路径", context.ParcelId);

        if (context.PlannedPath == null)
        {
            _logger?.LogError("包裹 {ParcelId} 没有规划路径，无法执行", context.ParcelId);
            context.IsSuccess = false;
            _completionDelegate?.Invoke(context.ParcelId, _clock.LocalNow, false);
            return;
        }

        var startTime = _clock.LocalNowOffset;

        try
        {
            PathExecutionResult executionResult;

            // 优先使用 IPathExecutionService（统一管线）
            if (_pathExecutionService != null)
            {
                executionResult = await _pathExecutionService.ExecutePathAsync(
                    context.ParcelId,
                    context.PlannedPath);
            }
            else if (_pathExecutor != null)
            {
                // 兼容旧接口：直接使用 ISwitchingPathExecutor
                executionResult = await _pathExecutor.ExecuteAsync(context.PlannedPath);
                
                // 兼容旧接口时，需要手动调用 IPathFailureHandler
                if (!executionResult.IsSuccess && _pathFailureHandler != null)
                {
                    _pathFailureHandler.HandlePathFailure(
                        context.ParcelId,
                        context.PlannedPath,
                        executionResult.FailureReason ?? "未知错误",
                        executionResult.FailedSegment);
                }
            }
            else
            {
                throw new InvalidOperationException("PathExecutionMiddleware 未配置执行服务");
            }

            var elapsedMs = (_clock.LocalNowOffset - startTime).TotalMilliseconds;
            context.ExecutionLatencyMs = elapsedMs;
            context.ActualChuteId = executionResult.ActualChuteId;

            if (executionResult.IsSuccess)
            {
                context.IsSuccess = true;
                var isOverloadException = context.ShouldForceException && context.ExceptionType == ExceptionType.Overload;

                _logger?.LogInformation("包裹 {ParcelId} 成功分拣到格口 {ActualChuteId}{OverloadFlag}", 
                    context.ParcelId, executionResult.ActualChuteId, isOverloadException ? " [超载异常]" : "");

                // 记录成功落格事件
                if (context.ShouldForceException)
                {
                    await WriteTraceAsync(new ParcelTraceEventArgs
                    {
                        ItemId = context.ParcelId,
                        BarCode = context.Barcode,
                        TargetChuteId = context.TargetChuteId,
                        ActualChuteId = executionResult.ActualChuteId,
                        OccurredAt = _clock.LocalNowOffset,
                        Stage = "ExceptionDiverted",
                        Source = "Execution",
                        Details = $"ChuteId={executionResult.ActualChuteId}, Reason={context.ExceptionReason}, Type={context.ExceptionType}"
                    });
                }
                else
                {
                    await WriteTraceAsync(new ParcelTraceEventArgs
                    {
                        ItemId = context.ParcelId,
                        BarCode = context.Barcode,
                        TargetChuteId = context.TargetChuteId,
                        ActualChuteId = executionResult.ActualChuteId,
                        OccurredAt = _clock.LocalNowOffset,
                        Stage = "Diverted",
                        Source = "Execution",
                        Details = $"ChuteId={executionResult.ActualChuteId}, TargetChuteId={context.TargetChuteId}"
                    });
                }

                _completionDelegate?.Invoke(context.ParcelId, _clock.LocalNow, true);
            }
            else
            {
                context.IsSuccess = false;

                _logger?.LogError("包裹 {ParcelId} 分拣失败: {FailureReason}，实际到达格口: {ActualChuteId}", 
                    context.ParcelId, executionResult.FailureReason, executionResult.ActualChuteId);

                // 记录异常落格事件
                await WriteTraceAsync(new ParcelTraceEventArgs
                {
                    ItemId = context.ParcelId,
                    BarCode = context.Barcode,
                    TargetChuteId = context.TargetChuteId,
                    ActualChuteId = executionResult.ActualChuteId,
                    OccurredAt = _clock.LocalNowOffset,
                    Stage = "ExceptionDiverted",
                    Source = "Execution",
                    Details = $"ChuteId={executionResult.ActualChuteId}, Reason={executionResult.FailureReason}"
                });

                _completionDelegate?.Invoke(context.ParcelId, _clock.LocalNow, false);

                // 注意：失败处理已在 IPathExecutionService 或兼容模式中完成
                // 此处不再重复调用 IPathFailureHandler
            }

            _logger?.LogDebug("包裹 {ParcelId} 完成路径执行", context.ParcelId);

            // 执行后续中间件（如果有）
            await next(context);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "包裹 {ParcelId} 执行路径时发生异常", context.ParcelId);
            context.IsSuccess = false;
            _completionDelegate?.Invoke(context.ParcelId, _clock.LocalNow, false);

            // 记录异常
            await WriteTraceAsync(new ParcelTraceEventArgs
            {
                ItemId = context.ParcelId,
                BarCode = context.Barcode,
                TargetChuteId = context.TargetChuteId,
                OccurredAt = _clock.LocalNowOffset,
                Stage = "ExceptionDiverted",
                Source = "Execution",
                Details = $"ExecutionException: {ex.Message}"
            });
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
