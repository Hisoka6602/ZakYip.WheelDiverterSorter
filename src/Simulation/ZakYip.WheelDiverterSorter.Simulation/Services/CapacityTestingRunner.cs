using ZakYip.WheelDiverterSorter.Core.Sorting.Runtime;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Simulation.Configuration;
using ZakYip.WheelDiverterSorter.Simulation.Results;
using ZakYip.WheelDiverterSorter.Simulation.Scenarios;
using ZakYip.WheelDiverterSorter.Observability;
using ZakYip.WheelDiverterSorter.Observability.Utilities;
using Microsoft.Extensions.Logging;

namespace ZakYip.WheelDiverterSorter.Simulation.Services;

/// <summary>
/// 产能测试场景运行器：使用不同的放包间隔运行多次仿真，收集产能数据。
/// </summary>
public class CapacityTestingRunner
{
    private readonly ISimulationScenarioRunner _scenarioRunner;
    private readonly ICapacityEstimator _capacityEstimator;
    private readonly PrometheusMetrics? _metrics;
    private readonly ILogger<CapacityTestingRunner> _logger;
    private readonly ISystemClock _clock;

    public CapacityTestingRunner(
        ISimulationScenarioRunner scenarioRunner,
        ICapacityEstimator capacityEstimator,
        ILogger<CapacityTestingRunner> logger,
        ISystemClock clock,
        PrometheusMetrics? metrics = null)
    {
        _scenarioRunner = scenarioRunner ?? throw new ArgumentNullException(nameof(scenarioRunner));
        _capacityEstimator = capacityEstimator ?? throw new ArgumentNullException(nameof(capacityEstimator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _metrics = metrics;
    }

    /// <summary>
    /// 运行产能测试：使用不同放包间隔运行仿真，收集结果。
    /// </summary>
    /// <param name="baseOptions">基础仿真配置</param>
    /// <param name="testIntervals">要测试的放包间隔列表（毫秒）</param>
    /// <param name="parcelsPerTest">每次测试的包裹数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>产能测试结果</returns>
    public async Task<CapacityTestResults> RunCapacityTestAsync(
        SimulationOptions baseOptions,
        IReadOnlyList<int> testIntervals,
        int parcelsPerTest,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "开始产能测试：{Count} 个间隔，每个测试 {Parcels} 个包裹",
            testIntervals.Count, parcelsPerTest);

        var results = new List<CapacityTestResult>();

        foreach (var intervalMs in testIntervals)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("产能测试被取消");
                break;
            }

            _logger.LogInformation("开始测试间隔：{IntervalMs}ms", intervalMs);

            try
            {
                // 创建测试场景：修改基础场景的放包间隔和包裹数
                var testOptions = baseOptions with
                {
                    ParcelInterval = TimeSpan.FromMilliseconds(intervalMs),
                    ParcelCount = parcelsPerTest,
                    IsEnableVerboseLogging = false, // 关闭详细日志以加快测试
                    IsPauseAtEnd = false
                };

                // 运行仿真
                var summary = await _scenarioRunner.RunScenarioAsync(testOptions, cancellationToken);

                // 构建测试结果
                var testResult = BuildTestResult(intervalMs, parcelsPerTest, summary);
                results.Add(testResult);

                _logger.LogInformation(
                    "间隔 {IntervalMs}ms 测试完成：成功率={SuccessRate:P2}, 平均延迟={AvgLatency:F0}ms",
                    intervalMs,
                    testResult.SuccessRate,
                    testResult.AverageLatencyMs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "间隔 {IntervalMs}ms 测试失败", intervalMs);
                
                // 添加失败结果
                results.Add(new CapacityTestResult
                {
                    IntervalMs = intervalMs,
                    ParcelCount = 0,
                    SuccessRate = 0,
                    AverageLatencyMs = 0,
                    MaxLatencyMs = 0,
                    ExceptionRate = 1.0,
                    OverloadTriggerCount = 0
                });
            }
        }

        _logger.LogInformation("产能测试完成，共 {Count} 组数据", results.Count);

        // 使用 ICapacityEstimator 估算安全产能区间
        var history = new CapacityHistory
        {
            TestResults = results
        };

        var estimation = _capacityEstimator.Estimate(in history);

        // 打印产能评估报告（中文）
        PrintCapacityReport(estimation, results);

        // 更新 Prometheus 指标
        if (_metrics != null && estimation.SafeMaxParcelsPerMinute > 0)
        {
            _metrics.SetRecommendedCapacity(estimation.SafeMaxParcelsPerMinute);
            _logger.LogInformation(
                "已更新 Prometheus 指标: sorting_capacity_recommended_parcels_per_minute = {Capacity}",
                estimation.SafeMaxParcelsPerMinute);
        }

        return new CapacityTestResults
        {
            BaseScenarioName = "CapacityTest",
            TestResults = results,
            TestTimestamp = _clock.LocalNow,
            EstimationResult = estimation
        };
    }

    private CapacityTestResult BuildTestResult(int intervalMs, int parcelCount, SimulationSummary summary)
    {
        var totalParcels = summary.TotalParcels;
        var successCount = summary.SortedToTargetChuteCount;
        var exceptionCount = totalParcels - successCount - summary.TimeoutCount - summary.DroppedCount;
        var timeoutCount = summary.TimeoutCount;

        // 计算成功率
        double successRate = totalParcels > 0 ? (double)successCount / totalParcels : 0;

        // 计算异常率
        double exceptionRate = totalParcels > 0 ? (double)(exceptionCount + timeoutCount) / totalParcels : 0;

        // 使用平均行程时间作为延迟
        double avgLatency = summary.AverageTravelTime?.TotalMilliseconds ?? 0;
        double maxLatency = summary.MaxTravelTime?.TotalMilliseconds ?? 0;

        // 统计超载触发次数（从包裹结果中统计）
        int overloadCount = 0;

        return new CapacityTestResult
        {
            IntervalMs = intervalMs,
            ParcelCount = totalParcels,
            SuccessRate = successRate,
            AverageLatencyMs = avgLatency,
            MaxLatencyMs = maxLatency,
            ExceptionRate = exceptionRate,
            OverloadTriggerCount = overloadCount
        };
    }

    private void PrintCapacityReport(CapacityEstimationResult estimation, List<CapacityTestResult> results)
    {
        _logger.LogInformation("=== 产能测试评估报告 ===");
        _logger.LogInformation("测试数据点数: {Count}", results.Count);
        _logger.LogInformation("安全产能区间: {Min:F0} - {Max:F0} 包裹/分钟",
            estimation.SafeMinParcelsPerMinute,
            estimation.SafeMaxParcelsPerMinute);
        _logger.LogInformation("危险阈值: {Threshold:F0} 包裹/分钟",
            estimation.DangerousThresholdParcelsPerMinute);
        _logger.LogInformation("置信度: {Confidence:P0}", estimation.Confidence);
        
        _logger.LogInformation("=== 各间隔测试结果 ===");
        foreach (var result in results.OrderBy(r => r.IntervalMs))
        {
            var parcelsPerMin = 60000.0 / result.IntervalMs;
            _logger.LogInformation(
                "间隔 {IntervalMs}ms ({ParcelsPerMin:F1} 包裹/分钟): 成功率={SuccessRate:P2}, 异常率={ExceptionRate:P2}, 平均延迟={AvgLatency:F0}ms",
                result.IntervalMs,
                parcelsPerMin,
                result.SuccessRate,
                result.ExceptionRate,
                result.AverageLatencyMs);
        }
        _logger.LogInformation("=== 报告结束 ===");
    }
}

/// <summary>
/// 产能测试结果集合。
/// </summary>
public class CapacityTestResults
{
    /// <summary>
    /// 基础场景名称。
    /// </summary>
    public required string BaseScenarioName { get; init; }

    /// <summary>
    /// 测试结果列表。
    /// </summary>
    public required IReadOnlyList<CapacityTestResult> TestResults { get; init; }

    /// <summary>
    /// 测试时间戳。
    /// </summary>
    public DateTime TestTimestamp { get; init; }

    /// <summary>
    /// 产能估算结果。
    /// </summary>
    public CapacityEstimationResult EstimationResult { get; init; }
}
