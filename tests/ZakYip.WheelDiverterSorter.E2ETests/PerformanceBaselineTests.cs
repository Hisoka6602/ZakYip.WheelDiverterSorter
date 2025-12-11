using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using ZakYip.WheelDiverterSorter.Core.Events.Chute;
#pragma warning disable CS0618 // 向后兼容：测试中使用已废弃字段
using System.Diagnostics;
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
using ZakYip.WheelDiverterSorter.Execution;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Execution;
using ZakYip.WheelDiverterSorter.Observability;
using ZakYip.WheelDiverterSorter.Simulation.Scenarios.Configuration;
using ZakYip.WheelDiverterSorter.Simulation.Results;
using ZakYip.WheelDiverterSorter.Simulation.Scenarios;
using ZakYip.WheelDiverterSorter.Simulation.Services;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;

namespace ZakYip.WheelDiverterSorter.E2ETests;

/// <summary>
/// PR-5: Performance baseline tests for 1000 parcel simulation
/// 用于建立性能基准和验证优化效果
/// </summary>
public class PerformanceBaselineTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly Mock<IUpstreamRoutingClient> _mockRuleEngineClient;
    private readonly Random _targetChuteRandom;

    public PerformanceBaselineTests()
    {
        _targetChuteRandom = new Random(42);

        _mockRuleEngineClient = new Mock<IUpstreamRoutingClient>(MockBehavior.Loose);
        _mockRuleEngineClient.Setup(x => x.ConnectAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockRuleEngineClient.Setup(x => x.DisconnectAsync())
            .Returns(Task.CompletedTask);
        _mockRuleEngineClient.Setup(x => x.IsConnected)
            .Returns(true);

        _mockRuleEngineClient
            .Setup(x => x.NotifyParcelDetectedAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((long parcelId, CancellationToken ct) =>
            {
                var chuteId = _targetChuteRandom.Next(1, 21);
                _ = Task.Run(() =>
                {
                    _mockRuleEngineClient.Raise(
                        x => x.ChuteAssigned += null,
                        new ChuteAssignmentNotificationEventArgs { ParcelId = parcelId, ChuteId = chuteId , AssignedAt = DateTimeOffset.Now }
                    );
                });
                return true;
            });

        var services = new ServiceCollection();

        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Warning);
        });

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
                                DiverterId = 1,
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

        var mockSystemRepo = new Mock<ISystemConfigurationRepository>();
        mockSystemRepo.Setup(x => x.Get())
            .Returns(new SystemConfiguration
            {
                ExceptionChuteId = 21,
                ChuteAssignmentTimeout = new ChuteAssignmentTimeoutOptions { FallbackTimeoutSeconds = 10m }
            });
        services.AddSingleton(mockSystemRepo.Object);

        services.AddSingleton(_mockRuleEngineClient.Object);

        var mockPathGenerator = new Mock<ISwitchingPathGenerator>();
        mockPathGenerator.Setup(x => x.GeneratePath(It.IsAny<int>()))
            .Returns((int targetChuteId) =>
            {
                if (targetChuteId >= 1 && targetChuteId <= 21)
                {
                    return new SwitchingPath
                    {
                        TargetChuteId = targetChuteId,
                        FallbackChuteId = 21,
                        GeneratedAt = DateTimeOffset.Now,
                        Segments = new List<SwitchingPathSegment>
                        {
                            new SwitchingPathSegment
                            {
                                DiverterId = 1,
                                TargetDirection = DiverterDirection.Straight,
                                SequenceNumber = 1,
                                TtlMilliseconds = 2000
                            }
                        }
                    };
                }
                return null;
            });
        services.AddSingleton(mockPathGenerator.Object);

        services.AddSingleton<ISwitchingPathExecutor>(sp =>
        {
            var mockExecutor = new Mock<ISwitchingPathExecutor>();
            mockExecutor.Setup(x => x.ExecuteAsync(It.IsAny<SwitchingPath>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((SwitchingPath path, CancellationToken ct) =>
                {
                    return new PathExecutionResult
                    {
                        IsSuccess = true,
                        ActualChuteId = path.TargetChuteId
                    };
                });
            return mockExecutor.Object;
        });

        services.AddSingleton<ParcelTimelineFactory>();
        services.AddSingleton<SimulationReportPrinter>();
        services.AddSingleton<PrometheusMetrics>();

        _serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public async Task Baseline_1000Parcels_RecordPerformanceMetrics()
    {
        // Arrange
        var scenario = ScenarioDefinitions.CreateLongRunDenseFlow(parcelCount: 1000);
        
        // Record GC stats before - force full collection for reliable baseline
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        var gen0Before = GC.CollectionCount(0);
        var gen1Before = GC.CollectionCount(1);
        var gen2Before = GC.CollectionCount(2);
        var memBefore = GC.GetTotalMemory(forceFullCollection: false);
        
        var stopwatch = Stopwatch.StartNew();

        // Act
        var summary = await RunScenarioAsync(scenario);
        
        stopwatch.Stop();
        
        // Record GC stats after
        var gen0After = GC.CollectionCount(0);
        var gen1After = GC.CollectionCount(1);
        var gen2After = GC.CollectionCount(2);
        var memAfter = GC.GetTotalMemory(forceFullCollection: false);

        // Assert basic correctness
        summary.TotalParcels.Should().Be(1000);
        summary.SortedToWrongChuteCount.Should().Be(0);

        // Output performance metrics
        var elapsedMs = stopwatch.Elapsed.TotalMilliseconds;
        var gen0Collections = gen0After - gen0Before;
        var gen1Collections = gen1After - gen1Before;
        var gen2Collections = gen2After - gen2Before;
        var memDeltaMB = (memAfter - memBefore) / 1024.0 / 1024.0;

        Console.WriteLine("=== Performance Baseline (1000 Parcels) ===");
        Console.WriteLine($"Total Time: {elapsedMs:F0} ms ({elapsedMs / 1000.0:F2} seconds)");
        Console.WriteLine($"Throughput: {1000.0 / (elapsedMs / 1000.0):F2} parcels/second");
        Console.WriteLine($"Gen0 Collections: {gen0Collections}");
        Console.WriteLine($"Gen1 Collections: {gen1Collections}");
        Console.WriteLine($"Gen2 Collections: {gen2Collections}");
        Console.WriteLine($"Memory Delta: {memDeltaMB:F2} MB");
        Console.WriteLine($"Success Rate: {summary.SortedToTargetChuteCount}/{summary.TotalParcels} ({100.0 * summary.SortedToTargetChuteCount / summary.TotalParcels:F1}%)");
        Console.WriteLine("==========================================");
        
        // Save baseline to dedicated results directory
        var resultsDir = Path.Combine(Environment.CurrentStateirectory, "performance-results");
        Directory.CreateDirectory(resultsDir);
        var baselinePath = Path.Combine(resultsDir, $"baseline-{DateTime.Now:yyyy-MM-dd-HHmmss}.txt");
        
        await File.WriteAllTextAsync(baselinePath, 
            $"Baseline Performance Metrics (1000 Parcels)\n" +
            $"Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
            $"Total Time: {elapsedMs:F0} ms\n" +
            $"Throughput: {1000.0 / (elapsedMs / 1000.0):F2} parcels/second\n" +
            $"Gen0 Collections: {gen0Collections}\n" +
            $"Gen1 Collections: {gen1Collections}\n" +
            $"Gen2 Collections: {gen2Collections}\n" +
            $"Memory Delta: {memDeltaMB:F2} MB\n" +
            $"Success Rate: {summary.SortedToTargetChuteCount}/{summary.TotalParcels}\n");
        
        Console.WriteLine($"\nBaseline saved to: {baselinePath}");
    }

    private async Task<SimulationSummary> RunScenarioAsync(SimulationScenario scenario)
    {
        using var scope = _serviceProvider.CreateScope();
        var services = scope.ServiceProvider;

        var options = Options.Create(scenario.Options);
        var ruleEngineClient = services.GetRequiredService<IUpstreamRoutingClient>();
        var pathGenerator = services.GetRequiredService<ISwitchingPathGenerator>();
        var pathExecutor = services.GetRequiredService<ISwitchingPathExecutor>();
        var timelineFactory = services.GetRequiredService<ParcelTimelineFactory>();
        var reportPrinter = services.GetRequiredService<SimulationReportPrinter>();
        var metrics = services.GetRequiredService<PrometheusMetrics>();
        var logger = services.GetRequiredService<ILogger<SimulationRunner>>();

        var runner = new SimulationRunner(
            options,
            ruleEngineClient,
            pathGenerator,
            pathExecutor,
            timelineFactory,
            reportPrinter,
            metrics,
            logger
        );

        return await runner.RunAsync();
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();
    }
}
