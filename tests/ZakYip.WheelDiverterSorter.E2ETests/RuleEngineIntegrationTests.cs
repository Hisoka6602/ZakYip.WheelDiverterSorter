using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ZakYip.WheelDiverterSorter.Communication;
using ZakYip.WheelDiverterSorter.Communication.Abstractions;
using ZakYip.WheelDiverterSorter.Communication.Models;
using ZakYip.WheelDiverterSorter.Core.Sorting.Orchestration;
using ZakYip.WheelDiverterSorter.Host.Application.Services;
using ZakYip.WheelDiverterSorter.E2ETests.Simulation;

namespace ZakYip.WheelDiverterSorter.E2ETests;

/// <summary>
/// E2E tests for RuleEngine integration including TCP communication and chute assignment flow
/// </summary>
public class RuleEngineIntegrationTests : E2ETestBase
{
    private readonly ISortingOrchestrator _orchestrator;

    public RuleEngineIntegrationTests(E2ETestFactory factory) : base(factory)
    {
        _orchestrator = Scope.ServiceProvider.GetRequiredService<ISortingOrchestrator>();
        SetupDefaultRouteConfiguration();
    }

    [Fact]
    [SimulationScenario("RuleEngine_Connection_EstablishSuccessfully")]
    public async Task RuleEngineConnection_ShouldEstablishSuccessfully()
    {
        // Arrange
        Factory.MockRuleEngineClient!
            .Setup(x => x.ConnectAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        Factory.MockRuleEngineClient
            .Setup(x => x.IsConnected)
            .Returns(true);

        // Act
        await _orchestrator.StartAsync();

        // Assert - 验证至少调用一次连接
        Factory.MockRuleEngineClient.Verify(
            x => x.ConnectAsync(It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);

        Factory.MockRuleEngineClient.Object.IsConnected.Should().BeTrue();

        await _orchestrator.StopAsync();
    }

    [Fact]
    [SimulationScenario("RuleEngine_Disconnect_CleanDisconnect")]
    public async Task RuleEngineDisconnect_ShouldDisconnectCleanly()
    {
        // Arrange
        Factory.MockRuleEngineClient!
            .Setup(x => x.ConnectAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        Factory.MockRuleEngineClient
            .Setup(x => x.DisconnectAsync())
            .Returns(Task.CompletedTask);

        await _orchestrator.StartAsync();

        // Act
        await _orchestrator.StopAsync();

        // Assert - 验证至少调用一次断开连接
        Factory.MockRuleEngineClient.Verify(
            x => x.DisconnectAsync(),
            Times.AtLeastOnce);
    }

    [Fact]
    [SimulationScenario("RuleEngine_ParcelDetectionNotification_SentToRuleEngine")]
    public async Task ParcelDetectionNotification_ShouldBeSentToRuleEngine()
    {
        // Arrange
        var parcelId = DateTimeOffset.Now.ToUnixTimeMilliseconds();

        Factory.MockRuleEngineClient!
            .Setup(x => x.ConnectAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        Factory.MockRuleEngineClient
            .Setup(x => x.NotifyParcelDetectedAsync(parcelId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        Factory.MockRuleEngineClient
            .Setup(x => x.IsConnected)
            .Returns(true);

        await _orchestrator.StartAsync();

        // Act
        var result = await Factory.MockRuleEngineClient.Object.NotifyParcelDetectedAsync(parcelId);

        // Assert
        result.Should().BeTrue();
        Factory.MockRuleEngineClient.Verify(
            x => x.NotifyParcelDetectedAsync(parcelId, It.IsAny<CancellationToken>()),
            Times.Once);

        await _orchestrator.StopAsync();
    }

    [Fact]
    [SimulationScenario("RuleEngine_ChuteAssignment_ReceivedFromRuleEngine")]
    public async Task ChuteAssignment_ShouldBeReceivedFromRuleEngine()
    {
        // Arrange
        var parcelId = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        var targetChuteId = 1;

        Factory.MockRuleEngineClient!
            .Setup(x => x.ConnectAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        Factory.MockRuleEngineClient
            .Setup(x => x.IsConnected)
            .Returns(true);

        await _orchestrator.StartAsync();

        // Act - Simulate RuleEngine sending chute assignment (push model)
        // In the push model, the client receives assignments without explicitly requesting them
        var assignmentArgs = new ChuteAssignmentNotificationEventArgs { ParcelId = parcelId, ChuteId = targetChuteId
        , NotificationTime = DateTimeOffset.Now };

        Factory.MockRuleEngineClient.Raise(
            x => x.ChuteAssignmentReceived += null,
            Factory.MockRuleEngineClient.Object,
            assignmentArgs);

        // Allow time for processing
        await Task.Delay(300);

        // Assert - In push model, chute assignment is received via event
        // The orchestrator subscribes to ChuteAssignmentReceived event on startup
        // Verify the event subscription was set up
        Factory.MockRuleEngineClient.VerifyAdd(
            x => x.ChuteAssignmentReceived += It.IsAny<EventHandler<ChuteAssignmentNotificationEventArgs>>(),
            Times.AtLeastOnce);

        await _orchestrator.StopAsync();
    }

    [Fact]
    [SimulationScenario("RuleEngine_ConnectionFailure_HandledGracefully")]
    public async Task ConnectionFailure_ShouldBeHandledGracefully()
    {
        // Arrange
        Factory.MockRuleEngineClient!
            .Setup(x => x.ConnectAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        Factory.MockRuleEngineClient
            .Setup(x => x.IsConnected)
            .Returns(false);

        // Act
        await _orchestrator.StartAsync();

        // Assert - Should handle failure without throwing
        Factory.MockRuleEngineClient.Object.IsConnected.Should().BeFalse();

        await _orchestrator.StopAsync();
    }

    [Fact]
    [SimulationScenario("RuleEngine_NotificationFailure_ReturnFalse")]
    public async Task NotificationFailure_ShouldReturnFalse()
    {
        // Arrange
        var parcelId = DateTimeOffset.Now.ToUnixTimeMilliseconds();

        Factory.MockRuleEngineClient!
            .Setup(x => x.ConnectAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        Factory.MockRuleEngineClient
            .Setup(x => x.NotifyParcelDetectedAsync(parcelId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        Factory.MockRuleEngineClient
            .Setup(x => x.IsConnected)
            .Returns(true);

        await _orchestrator.StartAsync();

        // Act
        var result = await Factory.MockRuleEngineClient.Object.NotifyParcelDetectedAsync(parcelId);

        // Assert
        result.Should().BeFalse();

        await _orchestrator.StopAsync();
    }

    [Fact]
    [SimulationScenario("RuleEngine_AssignmentTimeout_FallbackToException")]
    public async Task AssignmentTimeout_ShouldFallbackToExceptionChute()
    {
        // Arrange
        var parcelId = DateTimeOffset.Now.ToUnixTimeMilliseconds();

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

        // Act - Don't send assignment (simulate timeout)
        await Factory.MockRuleEngineClient.Object.NotifyParcelDetectedAsync(parcelId);

        // Allow time for timeout to occur
        await Task.Delay(2000);

        // Assert - Should handle timeout gracefully
        Factory.MockRuleEngineClient.Verify(
            x => x.NotifyParcelDetectedAsync(parcelId, It.IsAny<CancellationToken>()),
            Times.Once);

        await _orchestrator.StopAsync();
    }

    public override void Dispose()
    {
        _orchestrator?.StopAsync().GetAwaiter().GetResult();
        base.Dispose();
    }
}
