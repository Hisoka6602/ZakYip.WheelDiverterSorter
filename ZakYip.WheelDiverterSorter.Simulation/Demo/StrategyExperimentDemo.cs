using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Sorting.Overload;
using ZakYip.WheelDiverterSorter.Execution;
using ZakYip.WheelDiverterSorter.Simulation.Strategies;

namespace ZakYip.WheelDiverterSorter.Simulation.Demo;

/// <summary>
/// 策略实验演示程序
/// Strategy experiment demo program
/// </summary>
/// <remarks>
/// 演示如何使用策略实验框架进行 A/B/N 对比测试
/// Demonstrates how to use the strategy experiment framework for A/B/N comparison testing
/// </remarks>
public class StrategyExperimentDemo
{
    /// <summary>
    /// 运行演示 / Run demo
    /// </summary>
    public static async Task RunDemoAsync()
    {
        Console.WriteLine("=== 策略实验演示 / Strategy Experiment Demo ===");
        Console.WriteLine();

        // 创建日志工厂
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        var factoryLogger = loggerFactory.CreateLogger<DefaultStrategyFactory>();
        var runnerLogger = loggerFactory.CreateLogger<StrategyExperimentRunner>();

        // 创建策略工厂
        var strategyFactory = new DefaultStrategyFactory(factoryLogger);

        // 创建实验运行器
        var experimentRunner = new StrategyExperimentRunner(strategyFactory, runnerLogger);

        // 定义实验配置
        var config = CreateDemoExperimentConfig();

        Console.WriteLine($"实验配置 / Experiment Configuration:");
        Console.WriteLine($"  策略数量 / Profile Count: {config.Profiles.Count}");
        Console.WriteLine($"  包裹数量 / Parcel Count: {config.ParcelCount}");
        Console.WriteLine($"  放包间隔 / Release Interval: {config.ReleaseInterval}");
        Console.WriteLine($"  随机种子 / Random Seed: {config.RandomSeed}");
        Console.WriteLine($"  输出目录 / Output Directory: {config.OutputDirectory}");
        Console.WriteLine();

        // 运行实验
        Console.WriteLine("开始运行实验... / Starting experiment...");
        var results = await experimentRunner.RunExperimentAsync(config);

        // 打印结果摘要
        Console.WriteLine();
        Console.WriteLine("=== 实验结果摘要 / Experiment Results Summary ===");
        Console.WriteLine();

        foreach (var result in results)
        {
            Console.WriteLine($"Profile: {result.Profile.ProfileName}");
            Console.WriteLine($"  描述 / Description: {result.Profile.Description}");
            Console.WriteLine($"  成功率 / Success Rate: {result.SuccessRatio:P2}");
            Console.WriteLine($"  异常率 / Exception Rate: {result.ExceptionRatio:P2}");
            Console.WriteLine($"  Overload 事件 / Overload Events: {result.OverloadEvents}");
            Console.WriteLine($"  平均延迟 / Average Latency: {result.AverageLatency.TotalMilliseconds:F2}ms");
            Console.WriteLine($"  最大延迟 / Max Latency: {result.MaxLatency.TotalMilliseconds:F2}ms");
            Console.WriteLine();
        }

        Console.WriteLine($"报表已生成 / Reports generated in: {config.OutputDirectory}");
        Console.WriteLine();
        Console.WriteLine("演示完成 / Demo completed!");
    }

    private static StrategyExperimentConfig CreateDemoExperimentConfig()
    {
        return new StrategyExperimentConfig
        {
            ParcelCount = 500,
            ReleaseInterval = TimeSpan.FromMilliseconds(300),
            RandomSeed = 12345,
            Profiles = new[]
            {
                new StrategyProfile
                {
                    ProfileName = "Baseline",
                    Description = "基线策略（生产默认配置）",
                    OverloadPolicy = new OverloadPolicyConfiguration
                    {
                        Enabled = true,
                        ForceExceptionOnSevere = true,
                        ForceExceptionOnOverCapacity = false,
                        ForceExceptionOnTimeout = true,
                        ForceExceptionOnWindowMiss = false,
                        MaxInFlightParcels = null,
                        MinRequiredTtlMs = 500,
                        MinArrivalWindowMs = 200
                    },
                    RouteTimeBudgetFactor = 1.0m
                },
                new StrategyProfile
                {
                    ProfileName = "AggressiveOverload",
                    Description = "更激进的超载策略（更低阈值，更早触发异常）",
                    OverloadPolicy = new OverloadPolicyConfiguration
                    {
                        Enabled = true,
                        ForceExceptionOnSevere = true,
                        ForceExceptionOnOverCapacity = true,
                        ForceExceptionOnTimeout = true,
                        ForceExceptionOnWindowMiss = true,
                        MaxInFlightParcels = 50,
                        MinRequiredTtlMs = 800,
                        MinArrivalWindowMs = 300
                    },
                    RouteTimeBudgetFactor = 0.9m
                },
                new StrategyProfile
                {
                    ProfileName = "Conservative",
                    Description = "更保守的策略（更高阈值，更少触发异常）",
                    OverloadPolicy = new OverloadPolicyConfiguration
                    {
                        Enabled = true,
                        ForceExceptionOnSevere = false,
                        ForceExceptionOnOverCapacity = false,
                        ForceExceptionOnTimeout = false,
                        ForceExceptionOnWindowMiss = false,
                        MaxInFlightParcels = null,
                        MinRequiredTtlMs = 300,
                        MinArrivalWindowMs = 100
                    },
                    RouteTimeBudgetFactor = 1.2m
                }
            },
            OutputDirectory = "./reports/strategy"
        };
    }
}
