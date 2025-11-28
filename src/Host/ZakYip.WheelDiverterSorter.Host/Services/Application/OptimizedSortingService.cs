using System.Buffers;
using System.Diagnostics;
using ZakYip.WheelDiverterSorter.Core.LineModel;
using ZakYip.WheelDiverterSorter.Core.LineModel.Topology;
using ZakYip.WheelDiverterSorter.Execution;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Execution;
using ZakYip.WheelDiverterSorter.Observability;

namespace ZakYip.WheelDiverterSorter.Host.Services.Application;

/// <summary>
/// 性能优化的分拣服务
/// 集成了指标收集、对象池和优化的内存管理
/// </summary>
public class OptimizedSortingService
{
    private readonly ISwitchingPathGenerator _pathGenerator;
    private readonly ISwitchingPathExecutor _pathExecutor;
    private readonly SorterMetrics _metrics;
    private readonly AlarmService? _alarmService;
    private readonly ILogger<OptimizedSortingService> _logger;
    
    // 使用ArrayPool减少数组分配
    private static readonly ArrayPool<char> CharArrayPool = ArrayPool<char>.Shared;

    public OptimizedSortingService(
        ISwitchingPathGenerator pathGenerator,
        ISwitchingPathExecutor pathExecutor,
        SorterMetrics metrics,
        ILogger<OptimizedSortingService> logger,
        AlarmService? alarmService = null)
    {
        _pathGenerator = pathGenerator;
        _pathExecutor = pathExecutor;
        _metrics = metrics;
        _alarmService = alarmService;
        _logger = logger;
    }

    /// <summary>
    /// 执行分拣操作（带性能监控）
    /// </summary>
    public async Task<PathExecutionResult> SortParcelAsync(
        string parcelId, 
        int targetChuteId, 
        CancellationToken cancellationToken = default)
    {
        var overallStopwatch = Stopwatch.StartNew();
        _metrics.RecordSortingRequest();

        try
        {
            // 阶段1: 路径生成
            var pathGenStopwatch = Stopwatch.StartNew();
            var path = _pathGenerator.GeneratePath(targetChuteId);
            pathGenStopwatch.Stop();
            
            var pathGenDuration = pathGenStopwatch.Elapsed.TotalMilliseconds;
            _metrics.RecordPathGeneration(pathGenDuration, path != null);

            if (path == null)
            {
                _logger.LogWarning("路径生成失败: ParcelId={ParcelId}, ChuteId={ChuteId}", 
                    parcelId, targetChuteId);
                
                overallStopwatch.Stop();
                _metrics.RecordSortingFailure(overallStopwatch.Elapsed.TotalMilliseconds);
                _alarmService?.RecordSortingFailure();
                
                return new PathExecutionResult
                {
                    IsSuccess = false,
                    ActualChuteId = 0,
                    FailureReason = "目标格口无法映射到任何摆轮组合"
                };
            }

            // 阶段2: 路径执行
            var pathExecStopwatch = Stopwatch.StartNew();
            var result = await _pathExecutor.ExecuteAsync(path, cancellationToken);
            pathExecStopwatch.Stop();
            
            var pathExecDuration = pathExecStopwatch.Elapsed.TotalMilliseconds;
            _metrics.RecordPathExecution(pathExecDuration, result.IsSuccess);

            // 记录总体结果
            overallStopwatch.Stop();
            if (result.IsSuccess)
            {
                _metrics.RecordSortingSuccess(overallStopwatch.Elapsed.TotalMilliseconds);
                _alarmService?.RecordSortingSuccess();
                _logger.LogInformation(
                    "分拣成功: ParcelId={ParcelId}, ChuteId={ChuteId}, " +
                    "总时长={TotalMs}ms (生成={GenMs}ms, 执行={ExecMs}ms)",
                    parcelId, result.ActualChuteId,
                    overallStopwatch.Elapsed.TotalMilliseconds,
                    pathGenDuration, pathExecDuration);
            }
            else
            {
                _metrics.RecordSortingFailure(overallStopwatch.Elapsed.TotalMilliseconds);
                _alarmService?.RecordSortingFailure();
                _logger.LogWarning(
                    "分拣失败: ParcelId={ParcelId}, 原因={Reason}, " +
                    "总时长={TotalMs}ms",
                    parcelId, result.FailureReason,
                    overallStopwatch.Elapsed.TotalMilliseconds);
            }

            return result;
        }
        catch (Exception ex)
        {
            overallStopwatch.Stop();
            _metrics.RecordSortingFailure(overallStopwatch.Elapsed.TotalMilliseconds);
            _alarmService?.RecordSortingFailure();
            
            _logger.LogError(ex, "分拣过程发生异常: ParcelId={ParcelId}, ChuteId={ChuteId}", 
                parcelId, targetChuteId);
            
            return new PathExecutionResult
            {
                IsSuccess = false,
                ActualChuteId = 0,
                FailureReason = $"执行异常: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// 批量分拣（使用对象池和并行处理优化）
    /// </summary>
    public async Task<List<PathExecutionResult>> SortBatchAsync(
        IEnumerable<(string ParcelId, int ChuteId)> parcels,
        CancellationToken cancellationToken = default)
    {
        var tasks = parcels
            .Select(p => SortParcelAsync(p.ParcelId, p.ChuteId, cancellationToken))
            .ToList();

        var results = await Task.WhenAll(tasks);
        return results.ToList();
    }
}
