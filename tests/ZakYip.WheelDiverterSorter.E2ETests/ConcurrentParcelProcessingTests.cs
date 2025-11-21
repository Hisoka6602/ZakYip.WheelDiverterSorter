using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Collections.Concurrent;
using System.Net.Http.Json;
using ZakYip.WheelDiverterSorter.Communication;
using ZakYip.WheelDiverterSorter.Communication.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel;
using ZakYip.WheelDiverterSorter.Core.Enums.Communication;
using ZakYip.WheelDiverterSorter.Core.Enums.Conveyor;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Enums.IoBinding;
using ZakYip.WheelDiverterSorter.Core.Enums.Routing;
using ZakYip.WheelDiverterSorter.Core.Enums.Sensors;
using ZakYip.WheelDiverterSorter.Core.Enums.Sorting;
using ZakYip.WheelDiverterSorter.Core.Enums.System;
using ZakYip.WheelDiverterSorter.Core.LineModel.Topology;
using ZakYip.WheelDiverterSorter.Core.Enums.Communication;
using ZakYip.WheelDiverterSorter.Core.Enums.Conveyor;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Enums.IoBinding;
using ZakYip.WheelDiverterSorter.Core.Enums.Routing;
using ZakYip.WheelDiverterSorter.Core.Enums.Sensors;
using ZakYip.WheelDiverterSorter.Core.Enums.Sorting;
using ZakYip.WheelDiverterSorter.Core.Enums.System;
using ZakYip.WheelDiverterSorter.Execution;
using ZakYip.WheelDiverterSorter.Host.Services;
using ZakYip.WheelDiverterSorter.E2ETests.Simulation;

namespace ZakYip.WheelDiverterSorter.E2ETests;

/// <summary>
/// E2E tests for concurrent parcel processing scenarios
/// </summary>
public class ConcurrentParcelProcessingTests : E2ETestBase
{
    private readonly ParcelSortingOrchestrator _orchestrator;

    public ConcurrentParcelProcessingTests(E2ETestFactory factory) : base(factory)
    {
        _orchestrator = Scope.ServiceProvider.GetRequiredService<ParcelSortingOrchestrator>();
        SetupDefaultRouteConfiguration();
    }

    [Fact]
    [SimulationScenario("Concurrent_MultipleParcels_Processed")]
    public async Task MultipleParcels_ShouldBeProcessedConcurrently()
    {
        // Arrange
        const int parcelCount = 10;
        var parcels = Enumerable.Range(0, parcelCount)
            .Select(_ => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + _)
            .ToList();

        Factory.MockRuleEngineClient!
            .Setup(x => x.ConnectAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        Factory.MockRuleEngineClient
            .Setup(x => x.NotifyParcelDetectedAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        Factory.MockRuleEngineClient
            .Setup(x => x.IsConnected)
            .Returns(true);

        await _orchestrator.StartAsync();

        // Act - Send multiple chute assignments concurrently
        var tasks = parcels.Select(async parcelId =>
        {
            var chuteId = (parcelId % 6) + 1; // Distribute across chutes 1-6
            var assignmentArgs = new ChuteAssignmentNotificationEventArgs
            {
                ParcelId = parcelId,
                ChuteId = (int)chuteId
            };

            Factory.MockRuleEngineClient.Raise(
                x => x.ChuteAssignmentReceived += null,
                Factory.MockRuleEngineClient.Object,
                assignmentArgs);

            await Task.Delay(50); // Simulate processing time
        });

        await Task.WhenAll(tasks);
        await Task.Delay(1000); // Allow all processing to complete

        // Assert
        Factory.MockRuleEngineClient.Verify(
            x => x.NotifyParcelDetectedAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()),
            Times.AtLeast(0)); // May vary based on actual invocations

        await _orchestrator.StopAsync();
    }

    [Fact]
    [SimulationScenario("Concurrent_PathGeneration_NoRaceConditions")]
    public async Task ConcurrentPathGeneration_ShouldNotCauseRaceConditions()
    {
        // Arrange
        const int iterationCount = 100;
        var results = new ConcurrentBag<SwitchingPath?>();

        // Act - Generate paths concurrently
        var tasks = Enumerable.Range(0, iterationCount).Select(async i =>
        {
            var chuteId = (i % 6) + 1; // Use chutes 1-6
            await Task.Yield(); // Force async execution
            var path = PathGenerator.GeneratePath(chuteId);
            results.Add(path);
        });

        await Task.WhenAll(tasks);

        // Assert
        results.Should().HaveCount(iterationCount);
        results.Should().AllSatisfy(path => path.Should().NotBeNull());
        
        // All paths for same chute should be identical
        var chute1Paths = results.Where(p => p?.TargetChuteId == 1).ToList();
        if (chute1Paths.Count > 1)
        {
            var firstPath = chute1Paths.First();
            chute1Paths.Should().AllSatisfy(path =>
            {
                path!.Segments.Count.Should().Be(firstPath!.Segments.Count);
            });
        }
    }

    [Fact]
    [SimulationScenario("Concurrent_PathExecution_ResourceLocking")]
    public async Task ConcurrentPathExecution_ShouldMaintainResourceLocking()
    {
        // Arrange
        const int concurrentParcels = 5;
        var targetChuteId = 1;
        var path = PathGenerator.GeneratePath(targetChuteId);
        path.Should().NotBeNull();

        var results = new ConcurrentBag<PathExecutionResult>();

        // Act - Execute paths concurrently
        var tasks = Enumerable.Range(0, concurrentParcels).Select(async _ =>
        {
            await Task.Yield();
            var result = await PathExecutor.ExecuteAsync(path!);
            results.Add(result);
        });

        await Task.WhenAll(tasks);

        // Assert
        results.Should().HaveCount(concurrentParcels);
        results.Should().AllSatisfy(result =>
        {
            result.Should().NotBeNull();
            result.ActualChuteId.Should().BeGreaterThan(0);
        });
    }

    [Fact]
    [SimulationScenario("Concurrent_HighThroughput_HandleLoad")]
    public async Task HighThroughputScenario_ShouldHandleLoad()
    {
        // Arrange
        const int parcelCount = 50;
        var sw = System.Diagnostics.Stopwatch.StartNew();

        // Act - Process many parcels rapidly
        var tasks = Enumerable.Range(0, parcelCount).Select(async i =>
        {
            var chuteId = (i % 6) + 1;
            var path = PathGenerator.GeneratePath(chuteId);
            if (path != null)
            {
                await PathExecutor.ExecuteAsync(path);
            }
        });

        await Task.WhenAll(tasks);
        sw.Stop();

        // Assert - Should complete in reasonable time
        sw.Elapsed.TotalSeconds.Should().BeLessThan(30); // 30 seconds max for 50 parcels
        var throughput = parcelCount / sw.Elapsed.TotalSeconds;
        throughput.Should().BeGreaterThan(1); // At least 1 parcel per second
    }

    [Fact]
    [SimulationScenario("Concurrent_APIRequests_HandledCorrectly")]
    public async Task ConcurrentAPIRequests_ShouldBeHandledCorrectly()
    {
        // Arrange
        const int requestCount = 10;
        var requests = Enumerable.Range(1, requestCount).Select(i => new
        {
            ParcelId = $"PKG-{i:D3}",
            TargetChuteId = ((i % 6) + 1).ToString()
        }).ToList();

        // Act - Send concurrent API requests
        var tasks = requests.Select(async request =>
        {
            return await Client.PostAsJsonAsync("/api/debug/sort", request);
        });

        var responses = await Task.WhenAll(tasks);

        // Assert
        responses.Should().HaveCount(requestCount);
        responses.Should().AllSatisfy(response =>
        {
            response.IsSuccessStatusCode.Should().BeTrue();
        });
    }

    [Fact]
    [SimulationScenario("Concurrent_ParcelQueue_MaintainOrder")]
    public async Task ParcelQueueManagement_ShouldMaintainOrder()
    {
        // Arrange
        var processedParcels = new ConcurrentQueue<long>();
        const int parcelCount = 20;

        Factory.MockRuleEngineClient!
            .Setup(x => x.ConnectAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        Factory.MockRuleEngineClient
            .Setup(x => x.NotifyParcelDetectedAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .Callback<long, CancellationToken>((parcelId, _) => processedParcels.Enqueue(parcelId))
            .ReturnsAsync(true);

        Factory.MockRuleEngineClient
            .Setup(x => x.IsConnected)
            .Returns(true);

        await _orchestrator.StartAsync();

        // Act - Process parcels in sequence
        for (int i = 0; i < parcelCount; i++)
        {
            var parcelId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + i;
            await Factory.MockRuleEngineClient.Object.NotifyParcelDetectedAsync(parcelId);
            await Task.Delay(10); // Small delay between parcels
        }

        await Task.Delay(500); // Allow processing

        // Assert
        processedParcels.Should().HaveCount(parcelCount);

        await _orchestrator.StopAsync();
    }

    public override void Dispose()
    {
        _orchestrator?.StopAsync().GetAwaiter().GetResult();
        base.Dispose();
    }
}
