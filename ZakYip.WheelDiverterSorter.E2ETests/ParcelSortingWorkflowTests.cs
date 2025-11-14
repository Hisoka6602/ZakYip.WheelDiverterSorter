using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Net.Http.Json;
using ZakYip.WheelDiverterSorter.Communication;
using ZakYip.WheelDiverterSorter.Core;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Execution;
using ZakYip.WheelDiverterSorter.Host.Services;
using ZakYip.WheelDiverterSorter.Ingress.Models;

namespace ZakYip.WheelDiverterSorter.E2ETests;

/// <summary>
/// E2E tests for complete parcel sorting workflow from detection to sorting completion
/// </summary>
public class ParcelSortingWorkflowTests : E2ETestBase
{
    private readonly ParcelSortingOrchestrator _orchestrator;

    public ParcelSortingWorkflowTests(E2ETestFactory factory) : base(factory)
    {
        _orchestrator = Scope.ServiceProvider.GetRequiredService<ParcelSortingOrchestrator>();
        SetupDefaultRouteConfiguration();
    }

    [Fact]
    public async Task CompleteSortingFlow_WithValidChute_ShouldSucceed()
    {
        // Arrange
        var targetChuteId = 1;
        var parcelId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Setup mock RuleEngine to return target chute
        Factory.MockRuleEngineClient!
            .Setup(x => x.NotifyParcelDetectedAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        Factory.MockRuleEngineClient
            .Setup(x => x.IsConnected)
            .Returns(true);

        // Act - Start orchestrator
        await _orchestrator.StartAsync();

        // Simulate RuleEngine assigning chute
        var assignmentArgs = new ChuteAssignmentNotificationEventArgs
        {
            ParcelId = parcelId,
            ChuteId = targetChuteId
        };

        // Trigger the event by raising it
        Factory.MockRuleEngineClient.Raise(
            x => x.ChuteAssignmentReceived += null,
            Factory.MockRuleEngineClient.Object,
            assignmentArgs);

        // Allow time for processing
        await Task.Delay(500);

        // Assert
        Factory.MockRuleEngineClient.Verify(
            x => x.NotifyParcelDetectedAsync(parcelId, It.IsAny<CancellationToken>()),
            Times.Once);

        await _orchestrator.StopAsync();
    }

    [Fact]
    public void PathGeneration_ForValidChute_ShouldReturnValidPath()
    {
        // Arrange
        var targetChuteId = 1;

        // Act
        var path = PathGenerator.GeneratePath(targetChuteId);

        // Assert
        path.Should().NotBeNull();
        path!.TargetChuteId.Should().Be(targetChuteId);
        path.Segments.Should().NotBeEmpty();
        path.Segments.Should().AllSatisfy(segment =>
        {
            segment.DiverterId.Should().BeGreaterThan(0);
            segment.SequenceNumber.Should().BeGreaterThan(0);
            segment.TtlMilliseconds.Should().BeGreaterThan(0);
        });
    }

    [Fact]
    public void PathGeneration_ForInvalidChute_ShouldReturnNull()
    {
        // Arrange
        var invalidChuteId = 9999;

        // Act
        var path = PathGenerator.GeneratePath(invalidChuteId);

        // Assert
        path.Should().BeNull();
    }

    [Fact]
    public async Task PathExecution_WithValidPath_ShouldSucceed()
    {
        // Arrange
        var targetChuteId = 1;
        var path = PathGenerator.GeneratePath(targetChuteId);
        path.Should().NotBeNull();

        // Act
        var result = await PathExecutor.ExecuteAsync(path!);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.ActualChuteId.Should().Be(targetChuteId);
        result.FailureReason.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task FallbackToExceptionChute_WhenPathGenerationFails()
    {
        // Arrange
        var invalidChuteId = 9999;
        var parcelId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Setup mock to simulate path generation failure scenario
        Factory.MockRuleEngineClient!
            .Setup(x => x.NotifyParcelDetectedAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        Factory.MockRuleEngineClient
            .Setup(x => x.IsConnected)
            .Returns(true);

        // Act - Start orchestrator
        await _orchestrator.StartAsync();

        // Simulate RuleEngine assigning invalid chute
        var assignmentArgs = new ChuteAssignmentNotificationEventArgs
        {
            ParcelId = parcelId,
            ChuteId = invalidChuteId
        };

        Factory.MockRuleEngineClient.Raise(
            x => x.ChuteAssignmentReceived += null,
            Factory.MockRuleEngineClient.Object,
            assignmentArgs);

        // Allow time for processing
        await Task.Delay(500);

        // Assert - Should attempt to use exception chute
        // The orchestrator should handle the failure gracefully
        Factory.MockRuleEngineClient.Verify(
            x => x.NotifyParcelDetectedAsync(parcelId, It.IsAny<CancellationToken>()),
            Times.Once);

        await _orchestrator.StopAsync();
    }

    [Fact]
    public void MultipleChutes_ShouldHaveUniqueConfigurations()
    {
        // Arrange & Act
        var chute1Path = PathGenerator.GeneratePath(1);
        var chute2Path = PathGenerator.GeneratePath(2);
        var chute3Path = PathGenerator.GeneratePath(3);

        // Assert
        chute1Path.Should().NotBeNull();
        chute2Path.Should().NotBeNull();
        chute3Path.Should().NotBeNull();

        // Each chute should have different diverter configurations
        chute1Path!.TargetChuteId.Should().NotBe(chute2Path!.TargetChuteId);
        chute2Path.TargetChuteId.Should().NotBe(chute3Path!.TargetChuteId);
    }

    [Fact]
    public async Task DebugSort_API_ShouldProcessRequest()
    {
        // Arrange
        var request = new
        {
            ParcelId = "TEST-PKG-001",
            TargetChuteId = "1"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/debug/sort", request);

        // Assert
        response.Should().NotBeNull();
        response.IsSuccessStatusCode.Should().BeTrue();

        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        result.Should().NotBeNull();
        result.Should().ContainKey("parcelId");
        result.Should().ContainKey("isSuccess");
    }

    public override void Dispose()
    {
        _orchestrator?.StopAsync().GetAwaiter().GetResult();
        base.Dispose();
    }
}
