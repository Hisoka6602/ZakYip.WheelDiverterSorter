using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Upstream;
using ZakYip.WheelDiverterSorter.Core.LineModel;
using ZakYip.WheelDiverterSorter.Core.LineModel.Topology;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Execution;
using ZakYip.WheelDiverterSorter.Ingress;
using ZakYip.WheelDiverterSorter.Ingress.Models;
using ZakYip.WheelDiverterSorter.Observability;
using ZakYip.WheelDiverterSorter.Simulation.Scenarios.Configuration;
using ZakYip.WheelDiverterSorter.Simulation.Results;
using ZakYip.WheelDiverterSorter.Core.Sorting.Interfaces;
using ZakYip.WheelDiverterSorter.Core.Sorting.Models;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Enums.Monitoring;
using ZakYip.WheelDiverterSorter.Core.Enums.Parcel;

namespace ZakYip.WheelDiverterSorter.Simulation.Services;

/// <summary>
/// 仿真运行器
/// </summary>
/// <remarks>
/// 负责协调整个仿真流程：生成虚拟包裹、触发检测事件、执行分拣、收集结果
/// PR-U1: 使用 IUpstreamRoutingClient 替代 IRuleEngineClient
/// </remarks>
public class SimulationRunner
{
    private readonly SimulationOptions _options;
    private readonly IUpstreamRoutingClient _upstreamClient;
    private readonly ISwitchingPathGenerator _pathGenerator;
    private readonly ISwitchingPathExecutor _pathExecutor;
    private readonly ParcelTimelineFactory _timelineFactory;
    private readonly SimulationReportPrinter _reportPrinter;
    private readonly PrometheusMetrics _metrics;
    private readonly ILogger<SimulationRunner> _logger;
    private readonly IParcelLifecycleLogger? _lifecycleLogger;
    private readonly ICongestionDetector? _congestionDetector;
    private readonly CongestionMetricsCollector? _metricsCollector;
    
    // 使用 ConcurrentDictionary 实现线程安全 / Use ConcurrentDictionary for thread safety
    private readonly ConcurrentDictionary<long, TaskCompletionSource<long>> _pendingAssignments = new();
    private readonly ConcurrentDictionary<long, SimulatedParcelResultEventArgs> _parcelResults = new();
    private readonly object _lockObject = new();
    private long _misSortCount = 0;
    private DateTimeOffset? _previousEntryTime = null;
    private int _currentConcurrentParcels = 0;
    private int _maxConcurrentParcelsObserved = 0;
    private CongestionLevel _currentCongestionLevel = CongestionLevel.Normal;
    private int _currentReleaseIntervalMs = 300;

    /// <summary>
    /// 构造函数
    /// </summary>
    public SimulationRunner(
        IOptions<SimulationOptions> options,
        IUpstreamRoutingClient upstreamClient,
        ISwitchingPathGenerator pathGenerator,
        ISwitchingPathExecutor pathExecutor,
        ParcelTimelineFactory timelineFactory,
        SimulationReportPrinter reportPrinter,
        PrometheusMetrics metrics,
        ILogger<SimulationRunner> logger,
        IParcelLifecycleLogger? lifecycleLogger = null,
        ICongestionDetector? congestionDetector = null,
        ReleaseThrottleConfiguration? throttleConfig = null)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _upstreamClient = upstreamClient ?? throw new ArgumentNullException(nameof(upstreamClient));
        _pathGenerator = pathGenerator ?? throw new ArgumentNullException(nameof(pathGenerator));
        _pathExecutor = pathExecutor ?? throw new ArgumentNullException(nameof(pathExecutor));
        _timelineFactory = timelineFactory ?? throw new ArgumentNullException(nameof(timelineFactory));
        _reportPrinter = reportPrinter ?? throw new ArgumentNullException(nameof(reportPrinter));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _lifecycleLogger = lifecycleLogger;
        _congestionDetector = congestionDetector;

        // 策略相关代码已删除
        if (_congestionDetector != null && throttleConfig != null)
        {
            _metricsCollector = new CongestionMetricsCollector(
                TimeSpan.FromSeconds(throttleConfig.MetricsTimeWindowSeconds));
            _currentReleaseIntervalMs = throttleConfig.NormalReleaseIntervalMs;
            
            _logger.LogInformation("拥堵检测功能已启用");
        }

        // 订阅格口分配事件
        _upstreamClient.ChuteAssigned += OnChuteAssigned;
    }

    /// <summary>
    /// 获取观察到的最大并发包裹数
    /// </summary>
    public int MaxConcurrentParcelsObserved => _maxConcurrentParcelsObserved;

    /// <summary>
    /// 运行仿真
    /// </summary>
    public async Task<SimulationSummary> RunAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("开始仿真...");
        _reportPrinter.PrintConfigurationSummary(_options);

        var startTime = DateTimeOffset.Now;

        // 连接到RuleEngine
        var connected = await _upstreamClient.ConnectAsync(cancellationToken);
        if (!connected)
        {
            _logger.LogError("无法连接到RuleEngine，仿真终止");
            throw new InvalidOperationException("无法连接到RuleEngine");
        }

        _logger.LogInformation("已连接到RuleEngine（模拟）");

        if (_options.IsLongRunMode)
        {
            await RunLongModeAsync(startTime, cancellationToken);
        }
        else
        {
            await RunNormalModeAsync(startTime, cancellationToken);
        }

        var endTime = DateTimeOffset.Now;
        var totalDuration = endTime - startTime;

        // 统计结果
        var summary = GenerateSummary(totalDuration);
        
        // 打印报告
        _reportPrinter.PrintStatisticsReport(summary);

        // 断开连接
        await _upstreamClient.DisconnectAsync();

        _logger.LogInformation("仿真完成");

        return summary;
    }

    /// <summary>
    /// 运行正常模式（固定包裹数量）
    /// </summary>
    private async Task RunNormalModeAsync(DateTimeOffset startTime, CancellationToken cancellationToken)
    {
        for (int i = 0; i < _options.ParcelCount; i++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("仿真被取消");
                break;
            }

            // 策略相关暂停逻辑已删除

            await ProcessSingleParcelAsync(i, startTime, cancellationToken);

            // 等待下一个包裹到达 - 使用动态间隔
            if (i < _options.ParcelCount - 1)
            {
                var interval = GetCurrentReleaseInterval();
                await Task.Delay(interval, cancellationToken);
            }
        }
    }

    /// <summary>
    /// 运行长跑模式（基于时长或最大包裹数）
    /// </summary>
    private async Task RunLongModeAsync(DateTimeOffset startTime, CancellationToken cancellationToken)
    {
        _logger.LogInformation("长跑模式启动，持续时间: {Duration}, 最大包裹数: {MaxParcels}", 
            _options.LongRunDuration?.ToString() ?? "无限制", 
            _options.MaxLongRunParcels?.ToString() ?? "无限制");

        int parcelIndex = 0;
        var lastMetricsTime = DateTimeOffset.Now;
        
        // 场景切换：每1000个包裹更换一次摩擦配置（模拟不同工况）
        int scenarioBatchSize = 1000;
        int currentScenarioIndex = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            var currentTime = DateTimeOffset.Now;
            var elapsedTime = currentTime - startTime;

            // 检查是否达到持续时间限制
            if (_options.LongRunDuration.HasValue && elapsedTime >= _options.LongRunDuration.Value)
            {
                _logger.LogInformation("达到长跑持续时间限制: {Duration}", _options.LongRunDuration.Value);
                break;
            }

            // 检查是否达到最大包裹数限制
            if (_options.MaxLongRunParcels.HasValue && parcelIndex >= _options.MaxLongRunParcels.Value)
            {
                _logger.LogInformation("达到长跑最大包裹数限制: {MaxParcels}", _options.MaxLongRunParcels.Value);
                break;
            }

            // 场景切换：每批次修改摩擦模型（模拟不同工况）
            if (parcelIndex > 0 && parcelIndex % scenarioBatchSize == 0)
            {
                currentScenarioIndex++;
                _logger.LogInformation("切换场景批次 #{ScenarioIndex}，已处理 {ParcelCount} 个包裹", 
                    currentScenarioIndex, parcelIndex);
            }

            await ProcessSingleParcelAsync(parcelIndex, startTime, cancellationToken);

            // 定期输出统计信息
            var timeSinceLastMetrics = (currentTime - lastMetricsTime).TotalSeconds;
            if (timeSinceLastMetrics >= _options.MetricsPushIntervalSeconds)
            {
                PrintIntermediateStats(parcelIndex + 1, elapsedTime);
                lastMetricsTime = currentTime;
            }

            parcelIndex++;

            // 等待下一个包裹到达 - 使用动态间隔
            var interval = GetCurrentReleaseInterval();
            
            // 策略相关暂停逻辑已删除

            await Task.Delay(interval, cancellationToken);
        }

        _logger.LogInformation("长跑模式结束，共处理 {TotalParcels} 个包裹", parcelIndex);
    }

    /// <summary>
    /// 处理单个包裹（统一接口）
    /// </summary>
    private async Task ProcessSingleParcelAsync(int index, DateTimeOffset startTime, CancellationToken cancellationToken)
    {
        var parcelId = GenerateParcelId(index);
        
        // 记录包裹开始（用于拥堵检测）
        _metricsCollector?.RecordParcelStarted();
        
        // 追踪并发包裹数
        var currentConcurrent = Interlocked.Increment(ref _currentConcurrentParcels);
        // Use Interlocked.CompareExchange for thread-safe maximum tracking
        int currentMax;
        do
        {
            currentMax = _maxConcurrentParcelsObserved;
            if (currentConcurrent <= currentMax)
                break;
        } while (Interlocked.CompareExchange(ref _maxConcurrentParcelsObserved, currentConcurrent, currentMax) != currentMax);
        
        // 更新在途包裹数指标
        _metrics.SetInFlightParcels(currentConcurrent);
        
        // 策略相关拥堵级别更新已删除
        
        if (_options.IsEnableVerboseLogging)
        {
            _logger.LogInformation("处理包裹 {Index}，包裹ID: {ParcelId}", 
                index + 1, parcelId);
        }

        var processingStartTime = DateTimeOffset.Now;

        try
        {
            // 模拟包裹到达并处理分拣
            var result = await ProcessParcelAsync(parcelId, startTime.AddMilliseconds(index * _options.ParcelInterval.TotalMilliseconds), cancellationToken);
            
            var processingEndTime = DateTimeOffset.Now;
            var latencyMs = (processingEndTime - processingStartTime).TotalMilliseconds;
            var isSuccess = result.Status == ParcelSimulationStatus.SortedToTargetChute;
            
            // 记录包裹完成（用于拥堵检测）
            _metricsCollector?.RecordParcelCompleted(isSuccess, latencyMs);
            
            // ConcurrentDictionary is thread-safe for simple add/update operations
            _parcelResults[parcelId] = result;

            // 记录Prometheus指标
            RecordMetrics(result);

            // 检查错分并处理fail-fast
            if (result.Status == ParcelSimulationStatus.SortedToWrongChute)
            {
                HandleMisSort(parcelId, result);
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
            
            var processingEndTime = DateTimeOffset.Now;
            var latencyMs = (processingEndTime - processingStartTime).TotalMilliseconds;
            
            // 记录包裹完成（失败）
            _metricsCollector?.RecordParcelCompleted(false, latencyMs);
            
            var errorResult = new SimulatedParcelResultEventArgs
            {
                ParcelId = parcelId,
                Status = ParcelSimulationStatus.ExecutionError,
                FinalChuteId = _options.ExceptionChuteId,
                FailureReason = ex.Message
            };

            // ConcurrentDictionary is thread-safe for simple add/update operations
            _parcelResults[parcelId] = errorResult;

            RecordMetrics(errorResult);
        }
        finally
        {
            // 减少并发包裹计数
            var newConcurrent = Interlocked.Decrement(ref _currentConcurrentParcels);
            _metrics.SetInFlightParcels(newConcurrent);
        }
    }

    /// <summary>
    /// 记录Prometheus指标
    /// </summary>
    private void RecordMetrics(SimulatedParcelResultEventArgs result)
    {
        var statusLabel = result.Status.ToString();
        var travelTimeSeconds = result.TravelTime?.TotalSeconds;
        
        _metrics.RecordSimulationParcel(statusLabel, travelTimeSeconds);

        // PR-05: 记录总处理包裹数
        _metrics.RecordSortingTotalParcels();

        if (result.Status == ParcelSimulationStatus.SortedToWrongChute)
        {
            _metrics.RecordSimulationMisSort();
        }

        // PR-05: 记录成功包裹延迟
        if (result.Status == ParcelSimulationStatus.SortedToTargetChute && travelTimeSeconds.HasValue)
        {
            _metrics.RecordSortingSuccessLatency(travelTimeSeconds.Value);
        }

        // PR-05: 记录失败包裹（按原因分类）
        if (result.Status != ParcelSimulationStatus.SortedToTargetChute)
        {
            var failureReason = result.Status switch
            {
                ParcelSimulationStatus.Timeout => "upstream_timeout",
                ParcelSimulationStatus.Dropped => "dropped",
                ParcelSimulationStatus.ExecutionError => "execution_error",
                ParcelSimulationStatus.RuleEngineTimeout => "ruleengine_timeout",
                ParcelSimulationStatus.SortedToWrongChute => "wrong_chute",
                ParcelSimulationStatus.SensorFault => "sensor_fault",
                ParcelSimulationStatus.TooCloseToSort => "too_close_to_sort",
                ParcelSimulationStatus.UnknownSource => "unknown_source",
                _ => "unknown"
            };
            _metrics.RecordSortingFailedParcel(failureReason);
        }

        // 记录高密度包裹指标
        if (result.IsDenseParcel)
        {
            var scenario = "default"; // 在实际应用中可以从配置或上下文获取
            var strategy = _options.DenseParcelStrategy.ToString();
            var headwayTimeSeconds = result.HeadwayTime?.TotalSeconds;
            var headwayDistanceMm = result.HeadwayMm.HasValue ? (double)result.HeadwayMm.Value : (double?)null;

            _metrics.RecordSimulationDenseParcel(scenario, strategy, headwayTimeSeconds, headwayDistanceMm);
        }
    }

    /// <summary>
    /// 处理错分情况
    /// </summary>
    private void HandleMisSort(long parcelId, SimulatedParcelResultEventArgs result)
    {
        Interlocked.Increment(ref _misSortCount);
        
        // 记录ERROR日志
        _logger.LogError("❌❌❌ 检测到错分！包裹ID: {ParcelId}, 目标格口: {Target}, 实际格口: {Actual} ❌❌❌",
            parcelId, result.TargetChuteId, result.FinalChuteId);

        // 在控制台打印醒目的中文警告
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine();
        Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║                    ⚠️  严重错误  ⚠️                        ║");
        Console.WriteLine("╠════════════════════════════════════════════════════════════╣");
        Console.WriteLine($"║  检测到包裹错分！                                          ║");
        Console.WriteLine($"║  包裹ID: {parcelId,-47}║");
        Console.WriteLine($"║  目标格口: {result.TargetChuteId,-45}║");
        Console.WriteLine($"║  实际格口: {result.FinalChuteId,-45}║");
        Console.WriteLine("║                                                            ║");
        Console.WriteLine($"║  当前错分总数: {_misSortCount,-41}║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
        Console.WriteLine();
        Console.ResetColor();

        // 如果配置了快速失败，则退出程序
        if (_options.FailFastOnMisSort)
        {
            _logger.LogCritical("FailFastOnMisSort=true，程序即将退出");
            Console.WriteLine("按 FailFastOnMisSort 配置，程序将立即退出...");
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// 打印中间统计信息
    /// </summary>
    private void PrintIntermediateStats(int parcelCount, TimeSpan elapsed)
    {
        int sortedCount = 0;
        int timeoutCount = 0;
        int droppedCount = 0;
        int errorCount = 0;
        int misSortCount = 0;

        // ConcurrentDictionary.Values is thread-safe for iteration
        // (though results may change during iteration, it won't crash)
        foreach (var result in _parcelResults.Values)
        {
            switch (result.Status)
            {
                case ParcelSimulationStatus.SortedToTargetChute:
                    sortedCount++;
                    break;
                case ParcelSimulationStatus.Timeout:
                    timeoutCount++;
                    break;
                case ParcelSimulationStatus.Dropped:
                    droppedCount++;
                    break;
                case ParcelSimulationStatus.ExecutionError:
                case ParcelSimulationStatus.RuleEngineTimeout:
                    errorCount++;
                    break;
                case ParcelSimulationStatus.SortedToWrongChute:
                    misSortCount++;
                    break;
            }
        }

        _logger.LogInformation(
            "📊 [中间统计] 已运行: {Elapsed:hh\\:mm\\:ss}, 处理: {Total}, 成功: {Success}, 超时: {Timeout}, 掉包: {Dropped}, 错误: {Error}, 错分: {MisSort}",
            elapsed, parcelCount, sortedCount, timeoutCount, droppedCount, errorCount, misSortCount);
    }

    /// <summary>
    /// 生成包裹ID
    /// </summary>
    private long GenerateParcelId(int index)
    {
        // 使用当前时间戳加上索引作为包裹ID
        var baseTimestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        return baseTimestamp + index;
    }

    /// <summary>
    /// 处理单个包裹的分拣
    /// </summary>
    private async Task<SimulatedParcelResultEventArgs> ProcessParcelAsync(
        long parcelId, 
        DateTimeOffset entryTime,
        CancellationToken cancellationToken)
    {
        var processingStartTime = DateTimeOffset.Now;
        
        // 创建等待格口分配的任务
        var tcs = new TaskCompletionSource<long>();
        
        // ConcurrentDictionary is thread-safe for simple add/update operations
        _pendingAssignments[parcelId] = tcs;

        try
        {
            // 通知RuleEngine包裹到达
            var notified = await _upstreamClient.NotifyParcelDetectedAsync(parcelId, cancellationToken);
            
            if (!notified)
            {
                // TryRemove is thread-safe
                _pendingAssignments.TryRemove(parcelId, out _);
                
                LogParcelException(parcelId, null, "无法通知RuleEngine");
                LogParcelCompleted(parcelId, null, null, ParcelFinalStatus.RuleEngineTimeout);
                
                return new SimulatedParcelResultEventArgs
                {
                    ParcelId = parcelId,
                    Status = ParcelSimulationStatus.RuleEngineTimeout,
                    FailureReason = "无法通知RuleEngine"
                };
            }

            // 等待格口分配（带超时）
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
            
            long targetChuteId;
            try
            {
                targetChuteId = await tcs.Task.WaitAsync(linkedCts.Token);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("等待格口分配超时：包裹 {ParcelId}", parcelId);
                
                LogParcelException(parcelId, null, "等待格口分配超时");
                LogParcelCompleted(parcelId, null, null, ParcelFinalStatus.RuleEngineTimeout);
                
                return new SimulatedParcelResultEventArgs
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
                
                return new SimulatedParcelResultEventArgs
                {
                    ParcelId = parcelId,
                    TargetChuteId = targetChuteId,
                    FinalChuteId = _options.ExceptionChuteId,
                    Status = ParcelSimulationStatus.ExecutionError,
                    FailureReason = "无法生成路径"
                };
            }
            
            // 记录包裹创建事件
            LogParcelCreated(parcelId, entryTime, targetChuteId);

            // 生成包裹时间轴（应用摩擦因子、掉包模拟和高密度检测）
            DateTimeOffset? prevEntryTime;
            lock (_lockObject)
            {
                prevEntryTime = _previousEntryTime;
                _previousEntryTime = entryTime;
            }
            
            var timeline = _timelineFactory.GenerateTimeline(parcelId, path, entryTime, prevEntryTime);
            
            // 记录传感器通过事件
            LogSensorEvents(parcelId, timeline);

            // 检查传感器故障和抖动，但允许继续获取格口分配
            // 这样可以让仿真流程完整运行，最后再标记为异常
            var hasSensorIssue = timeline.IsSensorFault || timeline.HasJitter;
            var sensorIssueReason = timeline.IsSensorFault ? "摆轮前传感器故障" : "传感器抖动产生重复检测";
            
            // 如果是高密度包裹，根据策略处理
            if (timeline.IsDenseParcel)
            {
                var denseResult = ApplyDenseParcelStrategy(parcelId, targetChuteId, timeline);
                LogParcelException(parcelId, (denseResult.FinalChuteId ?? _options.ExceptionChuteId), "高密度包裹");
                LogParcelCompleted(parcelId, targetChuteId, (denseResult.FinalChuteId ?? _options.ExceptionChuteId), 
                    denseResult.Status == ParcelSimulationStatus.Timeout ? ParcelFinalStatus.Timeout : ParcelFinalStatus.ExceptionRouted);
                return denseResult;
            }
            
            // 如果掉包，直接返回掉包结果
            if (timeline.IsDropped)
            {
                var travelTime = timeline.SensorEvents.Last().TriggerTime - entryTime;
                
                var result = new SimulatedParcelResultEventArgs
                {
                    ParcelId = parcelId,
                    TargetChuteId = targetChuteId,
                    FinalChuteId = null,
                    Status = ParcelSimulationStatus.Dropped,
                    IsDropped = true,
                    DropoutLocation = timeline.DropoutLocation,
                    TravelTime = travelTime,
                    HeadwayTime = timeline.HeadwayTime,
                    HeadwayMm = timeline.HeadwayMm,
                    IsDenseParcel = timeline.IsDenseParcel
                };
                LogParcelException(parcelId, null, "包裹掉落");
                LogParcelCompleted(parcelId, targetChuteId, null, ParcelFinalStatus.Dropped);
                return result;
            }
            
            // 记录格口分配
            LogChuteAssigned(parcelId, targetChuteId);

            // 如果有传感器问题，直接路由到异常口
            if (hasSensorIssue)
            {
                var result = new SimulatedParcelResultEventArgs
                {
                    ParcelId = parcelId,
                    TargetChuteId = targetChuteId,
                    FinalChuteId = _options.ExceptionChuteId,
                    Status = ParcelSimulationStatus.SensorFault,
                    FailureReason = sensorIssueReason,
                    HeadwayTime = timeline.HeadwayTime,
                    HeadwayMm = timeline.HeadwayMm,
                    IsDenseParcel = timeline.IsDenseParcel
                };
                LogParcelException(parcelId, _options.ExceptionChuteId, sensorIssueReason);
                LogParcelCompleted(parcelId, targetChuteId, _options.ExceptionChuteId, ParcelFinalStatus.SensorFault);
                return result;
            }

            // 执行路径
            var execResult = await _pathExecutor.ExecuteAsync(path, cancellationToken);
            
            var finalChuteId = execResult.ActualChuteId;
            var totalTravelTime = timeline.ExpectedArrivalTime - entryTime;
            
            // 判断状态
            ParcelSimulationStatus status;
            ParcelFinalStatus finalStatus;
            if (!execResult.IsSuccess)
            {
                status = ParcelSimulationStatus.ExecutionError;
                finalStatus = ParcelFinalStatus.ExecutionError;
                LogParcelException(parcelId, finalChuteId, execResult.FailureReason ?? "执行错误");
            }
            else if (finalChuteId == targetChuteId)
            {
                status = ParcelSimulationStatus.SortedToTargetChute;
                finalStatus = ParcelFinalStatus.Success;
            }
            else
            {
                // 这种情况不应该发生！
                status = ParcelSimulationStatus.SortedToWrongChute;
                finalStatus = ParcelFinalStatus.ExecutionError;
                _logger.LogError(
                    "包裹 {ParcelId} 错误分拣！目标={Target}, 实际={Actual}", 
                    parcelId, targetChuteId, finalChuteId);
                LogParcelException(parcelId, finalChuteId, "错误分拣");
            }
            
            // 记录包裹完成
            LogParcelCompleted(parcelId, targetChuteId, finalChuteId, finalStatus);
            
            return new SimulatedParcelResultEventArgs
            {
                ParcelId = parcelId,
                TargetChuteId = targetChuteId,
                FinalChuteId = finalChuteId,
                Status = status,
                TravelTime = totalTravelTime,
                IsTimeout = !execResult.IsSuccess,
                FailureReason = execResult.FailureReason,
                HeadwayTime = timeline.HeadwayTime,
                HeadwayMm = timeline.HeadwayMm,
                IsDenseParcel = timeline.IsDenseParcel
            };
        }
        finally
        {
            // TryRemove is thread-safe
            _pendingAssignments.TryRemove(parcelId, out _);
        }
    }

    /// <summary>
    /// 应用高密度包裹策略
    /// </summary>
    /// <param name="parcelId">包裹ID</param>
    /// <param name="targetChuteId">目标格口ID</param>
    /// <param name="timeline">包裹时间轴</param>
    /// <returns>包裹仿真结果</returns>
    private SimulatedParcelResultEventArgs ApplyDenseParcelStrategy(
        long parcelId,
        long targetChuteId,
        ParcelTimeline timeline)
    {
        _logger.LogWarning(
            "包裹 {ParcelId} 违反最小安全头距规则，应用策略: {Strategy}",
            parcelId, _options.DenseParcelStrategy);

        return _options.DenseParcelStrategy switch
        {
            DenseParcelStrategy.RouteToException => new SimulatedParcelResultEventArgs
            {
                ParcelId = parcelId,
                TargetChuteId = targetChuteId,
                FinalChuteId = _options.ExceptionChuteId,
                Status = ParcelSimulationStatus.TooCloseToSort,
                FailureReason = "违反最小安全头距规则，路由到异常格口",
                HeadwayTime = timeline.HeadwayTime,
                HeadwayMm = timeline.HeadwayMm,
                IsDenseParcel = true
            },

            DenseParcelStrategy.MarkAsTimeout => new SimulatedParcelResultEventArgs
            {
                ParcelId = parcelId,
                TargetChuteId = targetChuteId,
                FinalChuteId = _options.ExceptionChuteId,
                Status = ParcelSimulationStatus.Timeout,
                FailureReason = "违反最小安全头距规则，标记为超时",
                IsTimeout = true,
                HeadwayTime = timeline.HeadwayTime,
                HeadwayMm = timeline.HeadwayMm,
                IsDenseParcel = true
            },

            DenseParcelStrategy.MarkAsDropped => new SimulatedParcelResultEventArgs
            {
                ParcelId = parcelId,
                TargetChuteId = targetChuteId,
                FinalChuteId = null,
                Status = ParcelSimulationStatus.Dropped,
                FailureReason = "违反最小安全头距规则，标记为掉包",
                IsDropped = true,
                HeadwayTime = timeline.HeadwayTime,
                HeadwayMm = timeline.HeadwayMm,
                IsDenseParcel = true
            },

            _ => throw new InvalidOperationException($"未知的高密度包裹策略: {_options.DenseParcelStrategy}")
        };
    }

    /// <summary>
    /// 处理格口分配事件
    /// </summary>
    private void OnChuteAssigned(object? sender, ChuteAssignmentEventArgs e)
    {
        // TryGetValue is thread-safe on ConcurrentDictionary
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
                // 添加到包裹列表
                summary.Parcels.Add(result);

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
                    case ParcelSimulationStatus.SensorFault:
                        summary.ExecutionErrorCount++; // 传感器故障计入执行错误
                        break;
                    case ParcelSimulationStatus.UnknownSource:
                        summary.ExecutionErrorCount++; // 未知来源计入执行错误
                        break;
                    case ParcelSimulationStatus.TooCloseToSort:
                        summary.ExecutionErrorCount++; // 间隔过近计入执行错误
                        break;
                }

                // 统计高密度包裹
                if (result.IsDenseParcel)
                {
                    summary.DenseParcelCount++;
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
            ParcelSimulationStatus.SensorFault => "传感器故障",
            ParcelSimulationStatus.UnknownSource => "来源不明",
            ParcelSimulationStatus.TooCloseToSort => "间隔过近无法分拣",
            _ => status.ToString()
        };
    }

    /// <summary>
    /// 记录包裹创建事件
    /// </summary>
    private void LogParcelCreated(long parcelId, DateTimeOffset entryTime, long targetChuteId)
    {
        _lifecycleLogger?.LogCreated(new ParcelLifecycleContext
        {
            ParcelId = parcelId,
            EntryTime = entryTime,
            TargetChuteId = targetChuteId,
            EventTime = DateTimeOffset.Now,
            IsSimulation = true
        });
    }

    /// <summary>
    /// 记录传感器通过事件
    /// </summary>
    private void LogSensorEvents(long parcelId, ParcelTimeline timeline)
    {
        if (_lifecycleLogger == null) return;

        foreach (var sensorEvent in timeline.SensorEvents)
        {
            _lifecycleLogger.LogSensorPassed(new ParcelLifecycleContext
            {
                ParcelId = parcelId,
                EventTime = sensorEvent.TriggerTime,
                IsSimulation = true
            }, sensorEvent.SensorId.ToString());
        }
    }

    /// <summary>
    /// 记录格口分配事件
    /// </summary>
    private void LogChuteAssigned(long parcelId, long chuteId)
    {
        _lifecycleLogger?.LogChuteAssigned(new ParcelLifecycleContext
        {
            ParcelId = parcelId,
            TargetChuteId = chuteId,
            EventTime = DateTimeOffset.Now,
            IsSimulation = true
        }, chuteId);
    }

    /// <summary>
    /// 记录包裹完成事件
    /// </summary>
    private void LogParcelCompleted(long parcelId, long? targetChuteId, long? actualChuteId, ParcelFinalStatus status)
    {
        _lifecycleLogger?.LogCompleted(new ParcelLifecycleContext
        {
            ParcelId = parcelId,
            TargetChuteId = targetChuteId,
            ActualChuteId = actualChuteId,
            EventTime = DateTimeOffset.Now,
            IsSimulation = true
        }, status);
    }

    /// <summary>
    /// 记录异常事件
    /// </summary>
    private void LogParcelException(long parcelId, long? chuteId, string reason)
    {
        _lifecycleLogger?.LogException(new ParcelLifecycleContext
        {
            ParcelId = parcelId,
            ActualChuteId = chuteId,
            EventTime = DateTimeOffset.Now,
            IsSimulation = true
        }, reason);
    }

    /// <summary>
    /// 获取当前放包间隔
    /// </summary>
    private TimeSpan GetCurrentReleaseInterval()
    {
        // 策略相关动态间隔已删除，使用配置的固定间隔
        return _options.ParcelInterval;
    }

    /// <summary>
    /// 更新拥堵级别
    /// </summary>
    private void UpdateCongestionLevel()
    {
        if (_congestionDetector == null || _metricsCollector == null)
        {
            return;
        }

        var metrics = _metricsCollector.GetCurrentMetrics();
        var congestionLevel = _congestionDetector.DetectCongestionLevel(metrics);
        
        if (congestionLevel != _currentCongestionLevel)
        {
            UpdateCongestionMetrics(congestionLevel);
        }
    }

    /// <summary>
    /// 更新拥堵指标并记录
    /// </summary>
    private void UpdateCongestionMetrics(CongestionLevel newLevel)
    {
        var oldLevel = _currentCongestionLevel;
        _currentCongestionLevel = newLevel;
        _metrics.SetCongestionLevel((int)newLevel);
        
        _logger.LogInformation("拥堵级别变化: {OldLevel} -> {NewLevel}", oldLevel, newLevel);
    }
}
