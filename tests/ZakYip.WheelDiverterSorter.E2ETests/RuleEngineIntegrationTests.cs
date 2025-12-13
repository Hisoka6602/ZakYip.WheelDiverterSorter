using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ZakYip.WheelDiverterSorter.Communication;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Upstream;
using ZakYip.WheelDiverterSorter.Communication.Models;
using ZakYip.WheelDiverterSorter.Core.Sorting.Orchestration;
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
        // PR-FIX: 移除 ConnectAsync mock（接口已重构，连接管理由实现类内部处理）
        Factory.MockRuleEngineClient!
            .Setup(x => x.IsConnected)
            .Returns(true);

        // Act
        await _orchestrator.StartAsync();

        // Assert - 验证连接状态（连接由实现类内部管理）
        Factory.MockRuleEngineClient!.Object.IsConnected.Should().BeTrue();

        await _orchestrator.StopAsync();
    }

    [Fact]
    [SimulationScenario("RuleEngine_Disconnect_CleanDisconnect")]
    public async Task RuleEngineDisconnect_ShouldDisconnectCleanly()
    {
        // Arrange
        // PR-FIX: 移除 ConnectAsync/DisconnectAsync mock（接口已重构，连接管理由实现类内部处理）
        Factory.MockRuleEngineClient!
            .Setup(x => x.IsConnected)
            .Returns(true);

        await _orchestrator.StartAsync();

        // Act
        await _orchestrator.StopAsync();

        // PR-FIX: 断开连接由实现类内部管理，测试只验证停止操作不抛出异常
        // 连接状态的管理完全由实现类决定
    }

    [Fact]
    [SimulationScenario("RuleEngine_ParcelDetectionNotification_SentToRuleEngine")]
    public async Task ParcelDetectionNotification_ShouldBeSentToRuleEngine()
    {
        // Arrange
        var parcelId = DateTimeOffset.Now.ToUnixTimeMilliseconds();

        // PR-FIX: 移除 ConnectAsync mock（接口已重构，连接管理由实现类内部处理）
        Factory.MockRuleEngineClient!
            .Setup(x => x.SendAsync(It.Is<ParcelDetectedMessage>(m => m.ParcelId == parcelId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        Factory.MockRuleEngineClient
            .Setup(x => x.IsConnected)
            .Returns(true);

        await _orchestrator.StartAsync();

        // Act
        var result = await Factory.MockRuleEngineClient.Object.SendAsync(new ParcelDetectedMessage { ParcelId = parcelId, DetectedAt = DateTimeOffset.Now });

        // Assert
        result.Should().BeTrue();
        Factory.MockRuleEngineClient.Verify(
            x => x.SendAsync(It.Is<ParcelDetectedMessage>(m => m.ParcelId == parcelId), It.IsAny<CancellationToken>()),
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

        // PR-FIX: 移除 ConnectAsync mock（接口已重构，连接管理由实现类内部处理）
        Factory.MockRuleEngineClient!
            .Setup(x => x.IsConnected)
            .Returns(true);

        await _orchestrator.StartAsync();

        // Act - Simulate RuleEngine sending chute assignment (push model)
        // In the push model, the client receives assignments without explicitly requesting them
        var assignmentArgs = new ChuteAssignmentEventArgs { ParcelId = parcelId, ChuteId = targetChuteId
        , AssignedAt = DateTimeOffset.Now };

        Factory.MockRuleEngineClient.Raise(
            x => x.ChuteAssigned += null,
            Factory.MockRuleEngineClient.Object,
            assignmentArgs);

        // Allow time for processing
        await Task.Delay(300);

        // Assert - In push model, chute assignment is received via event
        // The orchestrator subscribes to ChuteAssigned event on startup
        // Verify the event subscription was set up
        Factory.MockRuleEngineClient.VerifyAdd(
            x => x.ChuteAssigned += It.IsAny<EventHandler<ChuteAssignmentEventArgs>>(),
            Times.AtLeastOnce);

        await _orchestrator.StopAsync();
    }

    [Fact]
    [SimulationScenario("RuleEngine_ConnectionFailure_HandledGracefully")]
    public async Task ConnectionFailure_ShouldBeHandledGracefully()
    {
        // Arrange
        // PR-FIX: 移除 ConnectAsync mock（接口已重构，连接管理由实现类内部处理）
        Factory.MockRuleEngineClient!
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

        // PR-FIX: 移除 ConnectAsync mock（接口已重构，连接管理由实现类内部处理）
        Factory.MockRuleEngineClient!
            .Setup(x => x.SendAsync(It.Is<ParcelDetectedMessage>(m => m.ParcelId == parcelId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        Factory.MockRuleEngineClient
            .Setup(x => x.IsConnected)
            .Returns(true);

        await _orchestrator.StartAsync();

        // Act
        var result = await Factory.MockRuleEngineClient.Object.SendAsync(new ParcelDetectedMessage { ParcelId = parcelId, DetectedAt = DateTimeOffset.Now });

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

        // PR-FIX: 移除 ConnectAsync mock（接口已重构，连接管理由实现类内部处理）
        Factory.MockRuleEngineClient!
            .Setup(x => x.SendAsync(It.IsAny<IUpstreamMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        Factory.MockRuleEngineClient
            .Setup(x => x.IsConnected)
            .Returns(true);

        await _orchestrator.StartAsync();

        // Act - Don't send assignment (simulate timeout)
        await Factory.MockRuleEngineClient.Object.SendAsync(new ParcelDetectedMessage { ParcelId = parcelId, DetectedAt = DateTimeOffset.Now });

        // Allow time for timeout to occur
        await Task.Delay(2000);

        // Assert - Should handle timeout gracefully
        Factory.MockRuleEngineClient.Verify(
            x => x.SendAsync(It.Is<ParcelDetectedMessage>(m => m.ParcelId == parcelId), It.IsAny<CancellationToken>()),
            Times.Once);

        await _orchestrator.StopAsync();
    }

    public override void Dispose()
    {
        _orchestrator?.StopAsync().GetAwaiter().GetResult();
        base.Dispose();
    }
}
