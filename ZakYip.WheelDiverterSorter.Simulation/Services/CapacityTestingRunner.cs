using ZakYip.Sorting.Core.Runtime;
using ZakYip.WheelDiverterSorter.Simulation.Configuration;
using ZakYip.WheelDiverterSorter.Simulation.Results;
using ZakYip.WheelDiverterSorter.Simulation.Scenarios;
using Microsoft.Extensions.Logging;

namespace ZakYip.WheelDiverterSorter.Simulation.Services;

/// <summary>
/// 产能测试场景运行器：使用不同的放包间隔运行多次仿真，收集产能数据。
/// </summary>
public class CapacityTestingRunner
{
    private readonly SimulationRunner _simulationRunner;
    private readonly ILogger<CapacityTestingRunner> _logger;

    public CapacityTestingRunner(
        SimulationRunner simulationRunner,
        ILogger<CapacityTestingRunner> logger)
    {
        _simulationRunner = simulationRunner ?? throw new ArgumentNullException(nameof(simulationRunner));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 运行产能测试：使用不同放包间隔运行仿真，收集结果。
    /// </summary>
    /// <param name="baseScenario">基础场景配置</param>
    /// <param name="testIntervals">要测试的放包间隔列表（毫秒）</param>
    /// <param name="parcelsPerTest">每次测试的包裹数</param>
    /// <returns>产能测试结果</returns>
    public async Task<CapacityTestResults> RunCapacityTestAsync(
        SimulationScenario baseScenario,
        IReadOnlyList<int> testIntervals,
        int parcelsPerTest)
    {
        _logger.LogInformation(
            "开始产能测试：{Count} 个间隔，每个测试 {Parcels} 个包裹",
            testIntervals.Count, parcelsPerTest);

        var results = new List<CapacityTestResult>();

        foreach (var intervalMs in testIntervals)
        {
            _logger.LogInformation("测试间隔：{IntervalMs}ms", intervalMs);

            // 创建测试场景：修改基础场景的放包间隔和包裹数
            var testOptions = baseScenario.Options with
            {
                ParcelInterval = TimeSpan.FromMilliseconds(intervalMs),
                ParcelCount = parcelsPerTest,
                IsEnableVerboseLogging = false, // 关闭详细日志以加快测试
                IsPauseAtEnd = false
            };

            // 注意：这里需要实际的仿真运行器来执行，暂时用占位逻辑
            // TODO: 实际实现时需要重新初始化 SimulationRunner 或使用工厂模式

            _logger.LogInformation(
                "间隔 {IntervalMs}ms 测试完成：暂未实际运行",
                intervalMs);

            // 创建占位结果
            var testResult = new CapacityTestResult
            {
                IntervalMs = intervalMs,
                ParcelCount = parcelsPerTest,
                SuccessRate = 0.95,
                AverageLatencyMs = 1000,
                MaxLatencyMs = 2000,
                ExceptionRate = 0.05,
                OverloadTriggerCount = 0
            };

            results.Add(testResult);
        }

        _logger.LogInformation("产能测试完成，共 {Count} 组数据", results.Count);

        return new CapacityTestResults
        {
            BaseScenarioName = baseScenario.ScenarioName,
            TestResults = results,
            TestTimestamp = DateTime.UtcNow
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

        // 统计超载触发次数（暂时设为0，后续可以从运行日志中提取）
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
}
