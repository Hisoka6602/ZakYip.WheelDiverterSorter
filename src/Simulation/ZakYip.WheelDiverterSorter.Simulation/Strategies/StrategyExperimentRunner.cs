using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Sorting.Overload;
using ZakYip.WheelDiverterSorter.Simulation.Strategies.Reports;
using ZakYip.WheelDiverterSorter.Observability.Utilities;

namespace ZakYip.WheelDiverterSorter.Simulation.Strategies;

/// <summary>
/// 策略实验运行器，用于 A/B/N 对比试验
/// Strategy experiment runner for A/B/N comparison testing
/// </summary>
/// <remarks>
/// 在同一条"虚拟包裹流"（相同随机种子、相同放包节奏、相同上游指令分布）下，
/// 分别运行多套策略，比较处理件数、正常落格比例、异常口比例、Overload 触发次数等指标。
/// 
/// Runs multiple strategy profiles with the same "virtual parcel flow" (same random seed, same release rhythm, 
/// same upstream instruction distribution) to compare metrics like throughput, success rate, exception rate, 
/// and overload trigger counts.
/// </remarks>
public class StrategyExperimentRunner
{
    private readonly IStrategyFactory _strategyFactory;
    private readonly ILogger<StrategyExperimentRunner> _logger;
    private readonly ISystemClock _clock;
    private readonly StrategyExperimentReportWriter _reportWriter;

    /// <summary>
    /// 构造函数
    /// Constructor
    /// </summary>
    public StrategyExperimentRunner(
        IStrategyFactory strategyFactory,
        ILogger<StrategyExperimentRunner> logger,
        ISystemClock clock)
    {
        _strategyFactory = strategyFactory ?? throw new ArgumentNullException(nameof(strategyFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _reportWriter = new StrategyExperimentReportWriter(_clock);
    }

    /// <summary>
    /// 运行策略实验
    /// Run strategy experiment
    /// </summary>
    /// <param name="config">实验配置 / Experiment configuration</param>
    /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
    /// <returns>实验结果列表 / List of experiment results</returns>
    public async Task<IReadOnlyList<StrategyExperimentResult>> RunExperimentAsync(
        StrategyExperimentConfig config,
        CancellationToken cancellationToken = default)
    {
        if (config == null)
        {
            throw new ArgumentNullException(nameof(config));
        }

        if (config.Profiles == null || config.Profiles.Count == 0)
        {
            throw new ArgumentException("至少需要一个策略 Profile / At least one strategy profile is required", nameof(config));
        }

        _logger.LogInformation(
            "开始策略实验 / Starting strategy experiment: {ProfileCount} profiles, {ParcelCount} parcels per profile, seed={RandomSeed}",
            config.Profiles.Count, config.ParcelCount, config.RandomSeed);

        var results = new List<StrategyExperimentResult>();

        foreach (var profile in config.Profiles)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("策略实验被取消 / Strategy experiment cancelled");
                break;
            }

            _logger.LogInformation(
                "运行策略 Profile: {ProfileName} - {Description}",
                profile.ProfileName, profile.Description);

            // 创建策略实例
            var overloadPolicy = _strategyFactory.CreateOverloadPolicy(profile);

            // 运行仿真并收集结果
            // 注意：这里的实现是一个框架，实际的仿真运行需要根据现有 SimulationRunner 进行适配
            // Note: This is a framework implementation. The actual simulation run needs to be adapted 
            // based on the existing SimulationRunner
            var result = await RunSingleProfileSimulationAsync(
                profile, 
                overloadPolicy, 
                config.RandomSeed, 
                config.ParcelCount, 
                config.ReleaseInterval,
                cancellationToken);

            results.Add(result);

            _logger.LogInformation(
                "Profile {ProfileName} 完成 / completed: Success={SuccessRatio:P2}, Exception={ExceptionRatio:P2}, Overload={OverloadEvents}",
                profile.ProfileName, result.SuccessRatio, result.ExceptionRatio, result.OverloadEvents);
        }

        // 生成报表
        GenerateReports(results, config.OutputDirectory);

        _logger.LogInformation("策略实验完成 / Strategy experiment completed");

        return results;
    }

    /// <summary>
    /// 运行单个 Profile 的仿真
    /// Run simulation for a single profile
    /// </summary>
    private async Task<StrategyExperimentResult> RunSingleProfileSimulationAsync(
        StrategyProfile profile,
        IOverloadHandlingPolicy overloadPolicy,
        int randomSeed,
        int parcelCount,
        TimeSpan releaseInterval,
        CancellationToken cancellationToken)
    {
        // 注意：这是一个简化的实现框架
        // 实际实现需要：
        // 1. 使用 randomSeed 初始化仿真环境（确保包裹流一致）
        // 2. 将 overloadPolicy 注入到仿真环境中
        // 3. 运行 parcelCount 个包裹的仿真，间隔为 releaseInterval
        // 4. 收集统计数据
        //
        // Note: This is a simplified implementation framework.
        // Actual implementation needs to:
        // 1. Initialize simulation environment with randomSeed (ensure consistent parcel flow)
        // 2. Inject overloadPolicy into simulation environment
        // 3. Run simulation with parcelCount parcels at releaseInterval
        // 4. Collect statistics

        // TODO: 集成实际的仿真运行逻辑
        // 这里使用模拟数据作为占位符
        // TODO: Integrate actual simulation run logic
        // Using simulated data as placeholder here

        await Task.Delay(100, cancellationToken); // 模拟运行时间 / Simulate run time

        // 模拟数据 - 实际应从仿真结果中获取
        // Simulated data - should be obtained from actual simulation results
        var random = new Random(randomSeed + profile.ProfileName.GetHashCode());
        var successRatio = 0.85m + (decimal)(random.NextDouble() * 0.14); // 85%-99%
        var successParcels = (int)(parcelCount * successRatio);
        var exceptionParcels = parcelCount - successParcels;
        var overloadEvents = random.Next(5, 100);

        return new StrategyExperimentResult
        {
            Profile = profile,
            TotalParcels = parcelCount,
            SuccessParcels = successParcels,
            ExceptionParcels = exceptionParcels,
            OverloadEvents = overloadEvents,
            SuccessRatio = successRatio,
            ExceptionRatio = 1 - successRatio,
            AverageLatency = TimeSpan.FromMilliseconds(300 + random.Next(0, 300)),
            MaxLatency = TimeSpan.FromMilliseconds(800 + random.Next(0, 800)),
            OverloadReasonDistribution = new Dictionary<OverloadReason, int>
            {
                [OverloadReason.Timeout] = random.Next(0, overloadEvents / 2),
                [OverloadReason.CapacityExceeded] = random.Next(0, overloadEvents / 2),
                [OverloadReason.WindowMiss] = random.Next(0, overloadEvents / 3)
            }
        };
    }

    /// <summary>
    /// 生成 CSV 和 Markdown 报表
    /// Generate CSV and Markdown reports
    /// </summary>
    private void GenerateReports(IReadOnlyList<StrategyExperimentResult> results, string outputDirectory)
    {
        try
        {
            var timestamp = _clock.LocalNow.ToString("yyyy-MM-dd-HHmmss");
            var csvPath = Path.Combine(outputDirectory, $"strategy-experiment-{timestamp}.csv");
            var mdPath = Path.Combine(outputDirectory, $"strategy-experiment-{timestamp}.md");

            _reportWriter.WriteCsvReport(results, csvPath);
            _logger.LogInformation("CSV 报表已生成 / CSV report generated: {Path}", csvPath);

            _reportWriter.WriteMarkdownReport(results, mdPath);
            _logger.LogInformation("Markdown 报表已生成 / Markdown report generated: {Path}", mdPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "生成报表失败 / Failed to generate reports");
        }
    }
}
