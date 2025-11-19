using System.Diagnostics;
using ZakYip.WheelDiverterSorter.Core.LineModel;
using ZakYip.WheelDiverterSorter.Core.LineModel.Topology;
using ZakYip.WheelDiverterSorter.Execution;
using ZakYip.WheelDiverterSorter.Host.Models;
using ZakYip.WheelDiverterSorter.Host.StateMachine;
using ZakYip.WheelDiverterSorter.Host.Utilities;
using ZakYip.WheelDiverterSorter.Observability;

namespace ZakYip.WheelDiverterSorter.Host.Services;

/// <summary>
/// 调试服务，用于测试直线摆轮分拣方案
/// </summary>
public class DebugSortService
{
    private readonly ISwitchingPathGenerator _pathGenerator;
    private readonly ISwitchingPathExecutor _pathExecutor;
    private readonly ILogger<DebugSortService> _logger;
    private readonly PrometheusMetrics _prometheusMetrics;
    private readonly ISystemStateManager _stateManager;

    public DebugSortService(
        ISwitchingPathGenerator pathGenerator,
        ISwitchingPathExecutor pathExecutor,
        ILogger<DebugSortService> logger,
        PrometheusMetrics prometheusMetrics,
        ISystemStateManager stateManager)
    {
        _pathGenerator = pathGenerator ?? throw new ArgumentNullException(nameof(pathGenerator));
        _pathExecutor = pathExecutor ?? throw new ArgumentNullException(nameof(pathExecutor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _prometheusMetrics = prometheusMetrics ?? throw new ArgumentNullException(nameof(prometheusMetrics));
        _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
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

            // 0. 检查系统状态，只有Running状态才能投放包裹
            var currentState = _stateManager.CurrentState;
            if (currentState != SystemState.Running)
            {
                _logger.LogWarning("系统未处于运行状态，拒绝分拣请求: 当前状态={CurrentState}", currentState);
                overallStopwatch.Stop();
                _prometheusMetrics.RecordSortingFailure(overallStopwatch.Elapsed.TotalSeconds);

                return new DebugSortResponse
                {
                    ParcelId = parcelId,
                    TargetChuteId = targetChuteId,
                    IsSuccess = false,
                    ActualChuteId = 0,
                    Message = $"系统当前未处于运行状态，无法投放包裹。当前状态: {GetStateDescription(currentState)}",
                    FailureReason = "系统状态检查失败",
                    PathSegmentCount = 0
                };
            }

            // 1. 调用路径生成器生成 SwitchingPath
            var pathGenStopwatch = Stopwatch.StartNew();
            var path = _pathGenerator.GeneratePath(targetChuteId);
            pathGenStopwatch.Stop();
            _prometheusMetrics.RecordPathGeneration(pathGenStopwatch.Elapsed.TotalSeconds);

            if (path == null)
            {
                _logger.LogWarning("无法生成路径: 目标格口={TargetChuteId}", targetChuteId);
                overallStopwatch.Stop();
                _prometheusMetrics.RecordSortingFailure(overallStopwatch.Elapsed.TotalSeconds);
                
                return new DebugSortResponse
                {
                    ParcelId = parcelId,
                    TargetChuteId = targetChuteId,
                    IsSuccess = false,
                    ActualChuteId = 0,
                    Message = "路径生成失败：目标格口无法映射到任何摆轮组合",
                    FailureReason = "目标格口未配置或不存在",
                    PathSegmentCount = 0
                };
            }

            _logger.LogInformation("路径生成成功: 段数={SegmentCount}, 目标格口={TargetChuteId}",
                path.Segments.Count, path.TargetChuteId);

            // 2. 调用执行器执行路径
            var pathExecStopwatch = Stopwatch.StartNew();
            var executionResult = await _pathExecutor.ExecuteAsync(path, cancellationToken);
            pathExecStopwatch.Stop();
            _prometheusMetrics.RecordPathExecution(pathExecStopwatch.Elapsed.TotalSeconds);

            _logger.LogInformation("路径执行完成: 成功={IsSuccess}, 实际格口={ActualChuteId}",
                executionResult.IsSuccess, executionResult.ActualChuteId);

            overallStopwatch.Stop();

            // 3. Record metrics based on success/failure
            if (executionResult.IsSuccess)
            {
                _prometheusMetrics.RecordSortingSuccess(overallStopwatch.Elapsed.TotalSeconds);
            }
            else
            {
                _prometheusMetrics.RecordSortingFailure(overallStopwatch.Elapsed.TotalSeconds);
            }

            // 4. 返回执行结果
            return new DebugSortResponse
            {
                ParcelId = parcelId,
                TargetChuteId = targetChuteId,
                IsSuccess = executionResult.IsSuccess,
                ActualChuteId = executionResult.ActualChuteId,
                Message = executionResult.IsSuccess
                    ? $"分拣成功：包裹 {parcelId} 已成功分拣到格口 {executionResult.ActualChuteId}"
                    : $"分拣失败：包裹 {parcelId} 落入异常格口 {executionResult.ActualChuteId}",
                FailureReason = executionResult.FailureReason,
                PathSegmentCount = path.Segments.Count
            };
        }
        finally
        {
            _prometheusMetrics.DecrementActiveRequests();
        }
    }

    /// <summary>
    /// 获取状态的中文描述
    /// </summary>
    private static string GetStateDescription(SystemState state)
    {
        return state switch
        {
            SystemState.Booting => "启动中",
            SystemState.Ready => "就绪",
            SystemState.Running => "运行中",
            SystemState.Paused => "暂停",
            SystemState.Faulted => "故障",
            SystemState.EmergencyStop => "急停",
            _ => state.ToString()
        };
    }
}
