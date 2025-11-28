using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Sorting.Pipeline;
using ZakYip.WheelDiverterSorter.Core.LineModel;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;
using ZakYip.WheelDiverterSorter.Core.LineModel.Topology;
using ZakYip.WheelDiverterSorter.Core.LineModel.Tracing;
using ZakYip.WheelDiverterSorter.Execution.Health;
using ZakYip.WheelDiverterSorter.Core.Sorting.Models;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Utilities;

namespace ZakYip.WheelDiverterSorter.Execution.Pipeline.Middlewares;

/// <summary>
/// 路径规划中间件，负责生成摆轮路径
/// </summary>
public sealed class RoutePlanningMiddleware : ISortingPipelineMiddleware
{
    private readonly ISwitchingPathGenerator _pathGenerator;
    private readonly ISystemConfigurationRepository _systemConfigRepository;
    private readonly PathHealthChecker? _pathHealthChecker;
    private readonly IParcelTraceSink? _traceSink;
    private readonly ILogger<RoutePlanningMiddleware>? _logger;
    private readonly ISystemClock _clock;

    /// <summary>
    /// 构造函数
    /// </summary>
    public RoutePlanningMiddleware(
        ISwitchingPathGenerator pathGenerator,
        ISystemConfigurationRepository systemConfigRepository,
        ISystemClock clock,
        PathHealthChecker? pathHealthChecker = null,
        IParcelTraceSink? traceSink = null,
        ILogger<RoutePlanningMiddleware>? logger = null)
    {
        _pathGenerator = pathGenerator ?? throw new ArgumentNullException(nameof(pathGenerator));
        _systemConfigRepository = systemConfigRepository ?? throw new ArgumentNullException(nameof(systemConfigRepository));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _pathHealthChecker = pathHealthChecker;
        _traceSink = traceSink;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async ValueTask InvokeAsync(SortingPipelineContext context, Func<SortingPipelineContext, ValueTask> next)
    {
        context.CurrentStage = "RoutePlanning";
        _logger?.LogDebug("包裹 {ParcelId} 开始路径规划", context.ParcelId);

        var startTime = _clock.LocalNowOffset;

        try
        {
            if (!context.TargetChuteId.HasValue)
            {
                throw new InvalidOperationException($"包裹 {context.ParcelId} 尚未分配目标格口");
            }

            // 生成摆轮路径
            var path = _pathGenerator.GeneratePath(context.TargetChuteId.Value);

            if (path == null)
            {
                _logger?.LogWarning("包裹 {ParcelId} 无法生成到格口 {TargetChuteId} 的路径", context.ParcelId, context.TargetChuteId);
                
                // 尝试生成到异常格口的路径
                var systemConfig = _systemConfigRepository.Get();
                var exceptionChuteId = systemConfig.ExceptionChuteId;
                path = _pathGenerator.GeneratePath(exceptionChuteId);

                if (path == null)
                {
                    throw new InvalidOperationException($"包裹 {context.ParcelId} 连异常格口路径都无法生成");
                }

                context.ShouldForceException = true;
                context.ExceptionReason = "NoPathToTargetChute";
                context.ExceptionType = ExceptionType.PathFailure;
                context.TargetChuteId = exceptionChuteId;
            }

            context.PlannedPath = path;

            // 计算路径所需的总时间
            double totalRouteTimeMs = path.Segments.Sum(s => (double)s.TtlMilliseconds);

            // 节点健康检查
            if (_pathHealthChecker != null && !context.ShouldForceException)
            {
                var pathHealthResult = _pathHealthChecker.ValidatePath(path);

                if (!pathHealthResult.IsHealthy)
                {
                    var unhealthyNodeList = string.Join(", ", pathHealthResult.UnhealthyNodeIds);
                    _logger?.LogWarning("包裹 {ParcelId} 的路径经过不健康节点 [{UnhealthyNodes}]，将重定向到异常格口", context.ParcelId, unhealthyNodeList);

                    // 记录节点降级决策
                    await WriteTraceAsync(new ParcelTraceEventArgs
                    {
                        ItemId = context.ParcelId,
                        BarCode = context.Barcode,
                        TargetChuteId = context.TargetChuteId,
                        OccurredAt = _clock.LocalNowOffset,
                        Stage = "OverloadEvaluated",
                        Source = "NodeHealthCheck",
                        Details = $"Reason=NodeDegraded, UnhealthyNodes=[{unhealthyNodeList}], OriginalTargetChute={context.TargetChuteId}"
                    });

                    // 重新生成到异常格口的路径
                    var systemConfig = _systemConfigRepository.Get();
                    var exceptionChuteId = systemConfig.ExceptionChuteId;
                    path = _pathGenerator.GeneratePath(exceptionChuteId);

                    if (path == null)
                    {
                        throw new InvalidOperationException($"包裹 {context.ParcelId} 连异常格口路径都无法生成（节点降级）");
                    }

                    context.ShouldForceException = true;
                    context.ExceptionReason = $"NodeDegraded: {unhealthyNodeList}";
                    context.ExceptionType = ExceptionType.NodeDegraded;
                    context.TargetChuteId = exceptionChuteId;
                    context.PlannedPath = path;
                    totalRouteTimeMs = path.Segments.Sum(s => (double)s.TtlMilliseconds);
                }
            }

            var elapsedMs = (_clock.LocalNowOffset - startTime).TotalMilliseconds;
            context.PlanningLatencyMs = elapsedMs;

            // 记录路径规划完成事件
            await WriteTraceAsync(new ParcelTraceEventArgs
            {
                ItemId = context.ParcelId,
                BarCode = context.Barcode,
                TargetChuteId = context.TargetChuteId,
                OccurredAt = _clock.LocalNowOffset,
                Stage = "RoutePlanned",
                Source = "Execution",
                Details = $"TargetChuteId={context.TargetChuteId}, SegmentCount={path.Segments.Count}, EstimatedTimeMs={totalRouteTimeMs:F0}"
            });

            _logger?.LogDebug("包裹 {ParcelId} 完成路径规划，段数: {SegmentCount}", context.ParcelId, path.Segments.Count);

            // 执行后续中间件
            await next(context);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "包裹 {ParcelId} 路径规划失败", context.ParcelId);
            context.ShouldForceException = true;
            context.ExceptionReason = $"RoutePlanningError: {ex.Message}";
            context.ExceptionType = ExceptionType.PathFailure;

            // 尝试继续执行（如果有路径）
            if (context.PlannedPath != null)
            {
                await next(context);
            }
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
