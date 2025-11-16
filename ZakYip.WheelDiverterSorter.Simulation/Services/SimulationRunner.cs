using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZakYip.WheelDiverterSorter.Communication;
using ZakYip.WheelDiverterSorter.Communication.Abstractions;
using ZakYip.WheelDiverterSorter.Core;
using ZakYip.WheelDiverterSorter.Execution;
using ZakYip.WheelDiverterSorter.Ingress;
using ZakYip.WheelDiverterSorter.Ingress.Models;
using ZakYip.WheelDiverterSorter.Simulation.Configuration;
using ZakYip.WheelDiverterSorter.Simulation.Results;

namespace ZakYip.WheelDiverterSorter.Simulation.Services;

/// <summary>
/// 仿真运行器
/// </summary>
/// <remarks>
/// 负责协调整个仿真流程：生成虚拟包裹、触发检测事件、执行分拣、收集结果
/// </remarks>
public class SimulationRunner
{
    private readonly SimulationOptions _options;
    private readonly IRuleEngineClient _ruleEngineClient;
    private readonly ISwitchingPathGenerator _pathGenerator;
    private readonly ISwitchingPathExecutor _pathExecutor;
    private readonly ParcelTimelineFactory _timelineFactory;
    private readonly SimulationReportPrinter _reportPrinter;
    private readonly ILogger<SimulationRunner> _logger;
    
    private readonly Dictionary<long, TaskCompletionSource<int>> _pendingAssignments = new();
    private readonly Dictionary<long, ParcelSimulationResultEventArgs> _parcelResults = new();
    private readonly object _lockObject = new();

    /// <summary>
    /// 构造函数
    /// </summary>
    public SimulationRunner(
        IOptions<SimulationOptions> options,
        IRuleEngineClient ruleEngineClient,
        ISwitchingPathGenerator pathGenerator,
        ISwitchingPathExecutor pathExecutor,
        ParcelTimelineFactory timelineFactory,
        SimulationReportPrinter reportPrinter,
        ILogger<SimulationRunner> logger)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _ruleEngineClient = ruleEngineClient ?? throw new ArgumentNullException(nameof(ruleEngineClient));
        _pathGenerator = pathGenerator ?? throw new ArgumentNullException(nameof(pathGenerator));
        _pathExecutor = pathExecutor ?? throw new ArgumentNullException(nameof(pathExecutor));
        _timelineFactory = timelineFactory ?? throw new ArgumentNullException(nameof(timelineFactory));
        _reportPrinter = reportPrinter ?? throw new ArgumentNullException(nameof(reportPrinter));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // 订阅格口分配事件
        _ruleEngineClient.ChuteAssignmentReceived += OnChuteAssignmentReceived;
    }

    /// <summary>
    /// 运行仿真
    /// </summary>
    public async Task<SimulationSummary> RunAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("开始仿真...");
        _reportPrinter.PrintConfigurationSummary(_options);

        var startTime = DateTimeOffset.UtcNow;

        // 连接到RuleEngine
        var connected = await _ruleEngineClient.ConnectAsync(cancellationToken);
        if (!connected)
        {
            _logger.LogError("无法连接到RuleEngine，仿真终止");
            throw new InvalidOperationException("无法连接到RuleEngine");
        }

        _logger.LogInformation("已连接到RuleEngine（模拟）");

        // 生成并处理虚拟包裹
        for (int i = 0; i < _options.ParcelCount; i++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("仿真被取消");
                break;
            }

            var parcelId = GenerateParcelId(i);
            
            if (_options.IsEnableVerboseLogging)
            {
                _logger.LogInformation("处理包裹 {Index}/{Total}，包裹ID: {ParcelId}", 
                    i + 1, _options.ParcelCount, parcelId);
            }

            try
            {
                // 模拟包裹到达并处理分拣
                var result = await ProcessParcelAsync(parcelId, startTime.AddMilliseconds(i * _options.ParcelInterval.TotalMilliseconds), cancellationToken);
                
                lock (_lockObject)
                {
                    _parcelResults[parcelId] = result;
                }

                if (_options.IsEnableVerboseLogging)
                {
                    var statusMsg = GetStatusMessage(result.Status);
                    _logger.LogInformation("包裹 {ParcelId}: {Status}", parcelId, statusMsg);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理包裹 {ParcelId} 时发生错误", parcelId);
                
                lock (_lockObject)
                {
                    _parcelResults[parcelId] = new ParcelSimulationResultEventArgs
                    {
                        ParcelId = parcelId,
                        Status = ParcelSimulationStatus.ExecutionError,
                        FinalChuteId = _options.ExceptionChuteId,
                        FailureReason = ex.Message
                    };
                }
            }

            // 等待下一个包裹到达
            if (i < _options.ParcelCount - 1)
            {
                await Task.Delay(_options.ParcelInterval, cancellationToken);
            }
        }

        var endTime = DateTimeOffset.UtcNow;
        var totalDuration = endTime - startTime;

        // 统计结果
        var summary = GenerateSummary(totalDuration);
        
        // 打印报告
        _reportPrinter.PrintStatisticsReport(summary);

        // 断开连接
        await _ruleEngineClient.DisconnectAsync();

        _logger.LogInformation("仿真完成");

        return summary;
    }

    /// <summary>
    /// 生成包裹ID
    /// </summary>
    private long GenerateParcelId(int index)
    {
        // 使用当前时间戳加上索引作为包裹ID
        var baseTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return baseTimestamp + index;
    }

    /// <summary>
    /// 处理单个包裹的分拣
    /// </summary>
    private async Task<ParcelSimulationResultEventArgs> ProcessParcelAsync(
        long parcelId, 
        DateTimeOffset entryTime,
        CancellationToken cancellationToken)
    {
        var processingStartTime = DateTimeOffset.UtcNow;
        
        // 创建等待格口分配的任务
        var tcs = new TaskCompletionSource<int>();
        
        lock (_lockObject)
        {
            _pendingAssignments[parcelId] = tcs;
        }

        try
        {
            // 通知RuleEngine包裹到达
            var notified = await _ruleEngineClient.NotifyParcelDetectedAsync(parcelId, cancellationToken);
            
            if (!notified)
            {
                lock (_lockObject)
                {
                    _pendingAssignments.Remove(parcelId);
                }
                
                return new ParcelSimulationResultEventArgs
                {
                    ParcelId = parcelId,
                    Status = ParcelSimulationStatus.RuleEngineTimeout,
                    FailureReason = "无法通知RuleEngine"
                };
            }

            // 等待格口分配（带超时）
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
            
            int targetChuteId;
            try
            {
                targetChuteId = await tcs.Task.WaitAsync(linkedCts.Token);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("等待格口分配超时：包裹 {ParcelId}", parcelId);
                
                return new ParcelSimulationResultEventArgs
                {
                    ParcelId = parcelId,
                    Status = ParcelSimulationStatus.RuleEngineTimeout,
                    FailureReason = "等待格口分配超时"
                };
            }
            
            // 生成路径
            var path = _pathGenerator.GeneratePath(targetChuteId);
            
            if (path == null)
            {
                _logger.LogWarning("无法为包裹 {ParcelId} 生成到格口 {ChuteId} 的路径", parcelId, targetChuteId);
                
                return new ParcelSimulationResultEventArgs
                {
                    ParcelId = parcelId,
                    TargetChuteId = targetChuteId,
                    FinalChuteId = _options.ExceptionChuteId,
                    Status = ParcelSimulationStatus.ExecutionError,
                    FailureReason = "无法生成路径"
                };
            }
            
            // 生成包裹时间轴（应用摩擦因子和掉包模拟）
            var timeline = _timelineFactory.GenerateTimeline(parcelId, path, entryTime);
            
            // 如果掉包，直接返回掉包结果
            if (timeline.IsDropped)
            {
                var travelTime = timeline.SensorEvents.Last().TriggerTime - entryTime;
                
                return new ParcelSimulationResultEventArgs
                {
                    ParcelId = parcelId,
                    TargetChuteId = targetChuteId,
                    FinalChuteId = null,
                    Status = ParcelSimulationStatus.Dropped,
                    IsDropped = true,
                    DropoutLocation = timeline.DropoutLocation,
                    TravelTime = travelTime
                };
            }
            
            // 执行路径
            var execResult = await _pathExecutor.ExecuteAsync(path, cancellationToken);
            
            var finalChuteId = execResult.ActualChuteId;
            var totalTravelTime = timeline.ExpectedArrivalTime - entryTime;
            
            // 判断状态
            ParcelSimulationStatus status;
            if (!execResult.IsSuccess)
            {
                status = ParcelSimulationStatus.ExecutionError;
            }
            else if (finalChuteId == targetChuteId)
            {
                status = ParcelSimulationStatus.SortedToTargetChute;
            }
            else
            {
                // 这种情况不应该发生！
                status = ParcelSimulationStatus.SortedToWrongChute;
                _logger.LogError(
                    "包裹 {ParcelId} 错误分拣！目标={Target}, 实际={Actual}", 
                    parcelId, targetChuteId, finalChuteId);
            }
            
            return new ParcelSimulationResultEventArgs
            {
                ParcelId = parcelId,
                TargetChuteId = targetChuteId,
                FinalChuteId = finalChuteId,
                Status = status,
                TravelTime = totalTravelTime,
                IsTimeout = !execResult.IsSuccess,
                FailureReason = execResult.FailureReason
            };
        }
        finally
        {
            lock (_lockObject)
            {
                _pendingAssignments.Remove(parcelId);
            }
        }
    }

    /// <summary>
    /// 处理格口分配事件
    /// </summary>
    private void OnChuteAssignmentReceived(object? sender, ChuteAssignmentNotificationEventArgs e)
    {
        lock (_lockObject)
        {
            if (_pendingAssignments.TryGetValue(e.ParcelId, out var tcs))
            {
                tcs.TrySetResult(e.ChuteId);
                
                if (_options.IsEnableVerboseLogging)
                {
                    _logger.LogDebug("收到格口分配：包裹 {ParcelId} -> 格口 {ChuteId}", 
                        e.ParcelId, e.ChuteId);
                }
            }
        }
    }

    /// <summary>
    /// 生成汇总统计
    /// </summary>
    private SimulationSummary GenerateSummary(TimeSpan totalDuration)
    {
        var summary = new SimulationSummary
        {
            TotalParcels = _options.ParcelCount,
            TotalDuration = totalDuration
        };

        var travelTimes = new List<TimeSpan>();

        // 统计每个状态和格口的分拣数量
        lock (_lockObject)
        {
            foreach (var (parcelId, result) in _parcelResults)
            {
                // 统计状态
                if (!summary.StatusStatistics.ContainsKey(result.Status))
                {
                    summary.StatusStatistics[result.Status] = 0;
                }
                summary.StatusStatistics[result.Status]++;

                // 统计各状态计数
                switch (result.Status)
                {
                    case ParcelSimulationStatus.SortedToTargetChute:
                        summary.SortedToTargetChuteCount++;
                        break;
                    case ParcelSimulationStatus.Timeout:
                        summary.TimeoutCount++;
                        break;
                    case ParcelSimulationStatus.Dropped:
                        summary.DroppedCount++;
                        break;
                    case ParcelSimulationStatus.ExecutionError:
                        summary.ExecutionErrorCount++;
                        break;
                    case ParcelSimulationStatus.RuleEngineTimeout:
                        summary.RuleEngineTimeoutCount++;
                        break;
                    case ParcelSimulationStatus.SortedToWrongChute:
                        summary.SortedToWrongChuteCount++;
                        break;
                }

                // 统计格口
                if (result.FinalChuteId.HasValue)
                {
                    if (!summary.ChuteStatistics.ContainsKey(result.FinalChuteId.Value))
                    {
                        summary.ChuteStatistics[result.FinalChuteId.Value] = 0;
                    }
                    summary.ChuteStatistics[result.FinalChuteId.Value]++;
                }

                // 收集行程时间
                if (result.TravelTime.HasValue)
                {
                    travelTimes.Add(result.TravelTime.Value);
                }
            }
        }

        // 计算行程时间统计
        if (travelTimes.Count > 0)
        {
            summary.AverageTravelTime = TimeSpan.FromTicks((long)travelTimes.Average(t => t.Ticks));
            summary.MinTravelTime = travelTimes.Min();
            summary.MaxTravelTime = travelTimes.Max();
        }

        return summary;
    }

    /// <summary>
    /// 获取状态消息
    /// </summary>
    private string GetStatusMessage(ParcelSimulationStatus status)
    {
        return status switch
        {
            ParcelSimulationStatus.SortedToTargetChute => "成功分拣到目标格口",
            ParcelSimulationStatus.Timeout => "超时",
            ParcelSimulationStatus.Dropped => "掉包",
            ParcelSimulationStatus.ExecutionError => "执行错误",
            ParcelSimulationStatus.RuleEngineTimeout => "规则引擎超时",
            ParcelSimulationStatus.SortedToWrongChute => "错误分拣",
            _ => status.ToString()
        };
    }
}
