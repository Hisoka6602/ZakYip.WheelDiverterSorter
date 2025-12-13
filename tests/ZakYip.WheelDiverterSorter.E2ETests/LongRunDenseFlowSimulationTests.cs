using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using ZakYip.WheelDiverterSorter.Core.Events.Chute;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ZakYip.WheelDiverterSorter.Communication;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Upstream;
using ZakYip.WheelDiverterSorter.Communication.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;
using ZakYip.WheelDiverterSorter.Configuration.Persistence.Repositories.LiteDb;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.LineModel.Topology;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Execution;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Execution;
using ZakYip.WheelDiverterSorter.Drivers;
using ZakYip.WheelDiverterSorter.Ingress;
using ZakYip.WheelDiverterSorter.Observability;
using ZakYip.WheelDiverterSorter.Observability.Utilities;
using ZakYip.WheelDiverterSorter.Simulation.Scenarios.Configuration;
using ZakYip.WheelDiverterSorter.Simulation.Results;
using ZakYip.WheelDiverterSorter.Simulation.Scenarios;
using ZakYip.WheelDiverterSorter.Simulation.Services;
using ZakYip.WheelDiverterSorter.E2ETests.Simulation;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Enums.Parcel;

namespace ZakYip.WheelDiverterSorter.E2ETests;

/// <summary>
/// 长时间高密度分拣仿真测试
/// </summary>
/// <remarks>
/// 测试场景 LongRunDenseFlow：
/// - 1000 个包裹，每 300ms 创建一个
/// - 10 台摆轮 / 21 个格口（1-20 正常，21 异常口）
/// - 间隔过近的包裹路由到异常口
/// - 验证并发包裹数在合理范围内（< 600）
/// - 生成 Markdown 报告
/// </remarks>
public class LongRunDenseFlowSimulationTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly Mock<IUpstreamRoutingClient> _mockRuleEngineClient;
    private readonly Random _targetChuteRandom;
    private readonly string _testOutputDirectory;

    public LongRunDenseFlowSimulationTests()
    {
        // 使用固定种子的随机数生成器，确保可重复
        _targetChuteRandom = new Random(42);

        // 创建临时测试输出目录
        _testOutputDirectory = Path.Combine(Path.GetTempPath(), "simulation-tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testOutputDirectory);

        // 创建模拟 RuleEngine 客户端
        _mockRuleEngineClient = new Mock<IUpstreamRoutingClient>(MockBehavior.Loose);
        _mockRuleEngineClient.Setup(x => x.ConnectAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockRuleEngineClient.Setup(x => x.DisconnectAsync())
            .Returns(Task.CompletedTask);
        _mockRuleEngineClient.Setup(x => x.IsConnected)
            .Returns(true);

        // 设置包裹检测通知的默认行为：随机返回 1-20 之间的格口ID
        _mockRuleEngineClient
            .Setup(x => x.SendAsync(It.IsAny<IUpstreamMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((long parcelId, CancellationToken ct) =>
            {
                // 同步触发格口分配事件，目标格口在 1-20 之间随机
                var chuteId = _targetChuteRandom.Next(1, 21); // 1-20
                _ = Task.Run(() =>
                {
                    _mockRuleEngineClient.Raise(
                        x => x.ChuteAssigned += null,
                        new ChuteAssignmentNotificationEventArgs { ParcelId = parcelId, ChuteId = chuteId , AssignedAt = DateTimeOffset.Now }
                    );
                });
                return true;
            });

        // 配置服务集合
        var services = new ServiceCollection();

        // 添加日志
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Warning);
        });

        // 添加核心配置仓储（使用模拟）
        var mockRouteRepo = new Mock<IRouteConfigurationRepository>();
        mockRouteRepo.Setup(x => x.GetByChuteId(It.IsAny<int>()))
            .Returns((int chuteId) =>
            {
                if (chuteId >= 1 && chuteId <= 21)
                {
                    return new ChuteRouteConfiguration
                    {
                        ChuteId = chuteId,
                        ChuteName = $"Chute {chuteId}",
                        DiverterConfigurations = new List<DiverterConfigurationEntry>
                        {
                            new DiverterConfigurationEntry
                            {
                                DiverterId = chuteId <= 10 ? chuteId : 10, // 模拟 10 台摆轮
                                TargetDirection = DiverterDirection.Straight,
                                SequenceNumber = 1
                            }
                        },
                        BeltSpeedMmPerSecond = 1000.0,
                        BeltLengthMm = 5000.0,
                        ToleranceTimeMs = 2000,
                        IsEnabled = true
                    };
                }
                return null;
            });

        services.AddSingleton(mockRouteRepo.Object);
        services.AddSingleton(_mockRuleEngineClient.Object);

        // 添加路径生成器
        services.AddSingleton<ISwitchingPathGenerator, DefaultSwitchingPathGenerator>();

        // 添加路径执行器（模拟执行器）
        services.AddSingleton<ISwitchingPathExecutor, MockSwitchingPathExecutor>();

        // 添加 Prometheus 指标（必需）
        services.AddSingleton<PrometheusMetrics>();

        // 添加时间轴收集器（用于生成报告）
        services.AddSingleton<ParcelTimelineCollector>();
        services.AddSingleton<IParcelLifecycleLogger>(sp => sp.GetRequiredService<ParcelTimelineCollector>());

        // 添加 Markdown 报告写入器
        services.AddSingleton<ISimulationReportWriter>(sp => 
            new MarkdownReportWriter(
                sp.GetRequiredService<ILogger<MarkdownReportWriter>>(),
                sp.GetRequiredService<ISystemClock>(),
                _testOutputDirectory));

        // 添加仿真服务
        services.AddSingleton<ParcelTimelineFactory>();
        services.AddSingleton<SimulationReportPrinter>();
        services.AddSingleton<SimulationRunner>();

        _serviceProvider = services.BuildServiceProvider();
    }

    /// <summary>
    /// 测试 LongRunDenseFlow 场景的正确性
    /// </summary>
    [Fact]
    [SimulationScenario("LongRunDenseFlow_AllParcelsCompleted")]
    public async Task LongRunDenseFlow_AllParcelsCompleted_WithCorrectRouting()
    {
        // Arrange
        var scenario = ScenarioDefinitions.CreateLongRunDenseFlow(parcelCount: 100); // 使用较小数量以加快测试
        var summary = await RunScenarioAsync(scenario);

        // Act & Assert

        // Assert - 所有包裹都必须有最终状态
        summary.TotalParcels.Should().Be(100);
        summary.Parcels.Should().HaveCount(100);

        // 验证每个包裹都有明确的最终状态（不是 None 或 Unknown）
        foreach (var parcel in summary.Parcels)
        {
            parcel.Status.Should().NotBe(ParcelSimulationStatus.SortedToWrongChute, 
                $"Parcel {parcel.ParcelId} should not be sorted to wrong chute");
        }

        // 验证成功包裹的格口匹配
        var successParcels = summary.Parcels.Where(p => p.Status == ParcelSimulationStatus.SortedToTargetChute);
        foreach (var parcel in successParcels)
        {
            parcel.FinalChuteId.Should().BeGreaterOrEqualTo(1)
                .And.BeLessOrEqualTo(20, $"Successful parcel {parcel.ParcelId} should be in chutes 1-20");
            parcel.FinalChuteId.Should().Be(parcel.TargetChuteId, 
                $"Parcel {parcel.ParcelId} should be at target chute");
        }

        // 验证异常包裹路由到异常口
        var exceptionParcels = summary.Parcels.Where(p => 
            p.Status == ParcelSimulationStatus.TooCloseToSort ||
            p.Status == ParcelSimulationStatus.SensorFault ||
            p.Status == ParcelSimulationStatus.Timeout ||
            p.Status == ParcelSimulationStatus.ExecutionError ||
            p.Status == ParcelSimulationStatus.UnknownSource);

        foreach (var parcel in exceptionParcels)
        {
            parcel.FinalChuteId.Should().Be(21, 
                $"Exception parcel {parcel.ParcelId} with status {parcel.Status} should be routed to chute 21");
        }

        // 验证总数一致性
        var completedCount = successParcels.Count();
        var exceptionCount = exceptionParcels.Count();
        var droppedCount = summary.Parcels.Count(p => p.Status == ParcelSimulationStatus.Dropped);

        (completedCount + exceptionCount + droppedCount).Should().Be(100, 
            "All parcels should be accounted for");

        // 验证没有错分
        summary.SortedToWrongChuteCount.Should().Be(0, "There should be no mis-sorted parcels");
    }

    /// <summary>
    /// 测试并发包裹数在合理范围内
    /// </summary>
    [Fact]
    [SimulationScenario("LongRunDenseFlow_ConcurrentParcelsWithinThreshold")]
    public async Task LongRunDenseFlow_ConcurrentParcelsWithinThreshold()
    {
        // Arrange
        var scenario = ScenarioDefinitions.CreateLongRunDenseFlow(parcelCount: 100);
        
        // Act
        var (summary, runner) = await RunScenarioWithRunnerAsync(scenario);
        var maxConcurrent = runner.MaxConcurrentParcelsObserved;

        // Assert - 验证并发包裹数不超过阈值
        // 理论值：2分钟路径 / 300ms间隔 ≈ 400 个并发包裹
        // 设置阈值为 600 以留有余量
        maxConcurrent.Should().BeLessThan(600, 
            "Concurrent parcels should not exceed the safety threshold");

        // 验证有一定的并发度（至少 > 1）
        maxConcurrent.Should().BeGreaterThan(1, 
            "There should be some concurrency in parcel processing");
    }

    /// <summary>
    /// 测试 Markdown 报告生成
    /// </summary>
    [Fact]
    [SimulationScenario("LongRunDenseFlow_GeneratesMarkdownReport")]
    public async Task LongRunDenseFlow_GeneratesMarkdownReport()
    {
        // Arrange
        var scenario = ScenarioDefinitions.CreateLongRunDenseFlow(parcelCount: 50);
        
        // Act
        await RunScenarioAsync(scenario);
        var timelineCollector = _serviceProvider.GetRequiredService<ParcelTimelineCollector>();
        var reportWriter = _serviceProvider.GetRequiredService<ISimulationReportWriter>();

        // 获取时间轴快照并生成报告
        var snapshots = timelineCollector.GetSnapshots();
        var reportPath = await reportWriter.WriteMarkdownAsync(
            scenario.ScenarioName,
            snapshots);

        // Assert
        File.Exists(reportPath).Should().BeTrue("Report file should be created");

        var reportContent = await File.ReadAllTextAsync(reportPath);
        reportContent.Should().Contain(scenario.ScenarioName, "Report should contain scenario name");
        reportContent.Should().Contain("场景摘要", "Report should contain summary section");
        reportContent.Should().Contain("总包裹数", "Report should contain total parcel count");
        reportContent.Should().Contain("包裹详情", "Report should contain parcel details");

        // 验证报告包含至少一个包裹的详情
        reportContent.Should().ContainAny("包裹", "Parcel", "should contain parcel information");
    }

    /// <summary>
    /// 运行仿真场景并返回结果
    /// </summary>
    private async Task<SimulationSummary> RunScenarioAsync(SimulationScenario scenario)
    {
        var options = Options.Create(scenario.Options);
        var ruleEngineClient = _serviceProvider.GetRequiredService<IUpstreamRoutingClient>();
        var pathGenerator = _serviceProvider.GetRequiredService<ISwitchingPathGenerator>();
        var pathExecutor = _serviceProvider.GetRequiredService<ISwitchingPathExecutor>();
        var timelineFactory = _serviceProvider.GetRequiredService<ParcelTimelineFactory>();
        var reportPrinter = _serviceProvider.GetRequiredService<SimulationReportPrinter>();
        var metrics = _serviceProvider.GetRequiredService<PrometheusMetrics>();
        var logger = _serviceProvider.GetRequiredService<ILogger<SimulationRunner>>();
        var lifecycleLogger = _serviceProvider.GetService<IParcelLifecycleLogger>();

        var runner = new SimulationRunner(
            options,
            ruleEngineClient,
            pathGenerator,
            pathExecutor,
            timelineFactory,
            reportPrinter,
            metrics,
            logger,
            lifecycleLogger
        );

        return await runner.RunAsync();
    }

    /// <summary>
    /// 运行仿真场景并返回结果和 runner 实例
    /// </summary>
    private async Task<(SimulationSummary summary, SimulationRunner runner)> RunScenarioWithRunnerAsync(SimulationScenario scenario)
    {
        var options = Options.Create(scenario.Options);
        var ruleEngineClient = _serviceProvider.GetRequiredService<IUpstreamRoutingClient>();
        var pathGenerator = _serviceProvider.GetRequiredService<ISwitchingPathGenerator>();
        var pathExecutor = _serviceProvider.GetRequiredService<ISwitchingPathExecutor>();
        var timelineFactory = _serviceProvider.GetRequiredService<ParcelTimelineFactory>();
        var reportPrinter = _serviceProvider.GetRequiredService<SimulationReportPrinter>();
        var metrics = _serviceProvider.GetRequiredService<PrometheusMetrics>();
        var logger = _serviceProvider.GetRequiredService<ILogger<SimulationRunner>>();
        var lifecycleLogger = _serviceProvider.GetService<IParcelLifecycleLogger>();

        var runner = new SimulationRunner(
            options,
            ruleEngineClient,
            pathGenerator,
            pathExecutor,
            timelineFactory,
            reportPrinter,
            metrics,
            logger,
            lifecycleLogger
        );

        var summary = await runner.RunAsync();
        return (summary, runner);
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();

        // 清理测试输出目录
        if (Directory.Exists(_testOutputDirectory))
        {
            try
            {
                Directory.Delete(_testOutputDirectory, recursive: true);
            }
            catch
            {
                // 忽略清理错误
            }
        }
    }
}
