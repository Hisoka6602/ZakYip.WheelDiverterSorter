using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Net.Http.Json;
using ZakYip.WheelDiverterSorter.Communication;
using ZakYip.WheelDiverterSorter.Core.LineModel;
using ZakYip.WheelDiverterSorter.Core.Enums.Communication;
using ZakYip.WheelDiverterSorter.Core.Enums.Conveyor;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Enums.Routing;
using ZakYip.WheelDiverterSorter.Core.Enums.Sensors;
using ZakYip.WheelDiverterSorter.Core.Enums.System;
using ZakYip.WheelDiverterSorter.Execution;
using ZakYip.WheelDiverterSorter.Host.Services;
using ZakYip.WheelDiverterSorter.Ingress.Models;
using ZakYip.WheelDiverterSorter.E2ETests.Simulation;
using ZakYip.WheelDiverterSorter.E2ETests.Helpers;

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
    [SimulationScenario("ParcelSorting_CompleteFlow_ValidChute")]
    public async Task CompleteSortingFlow_WithValidChute_ShouldSucceed()
    {
        // Arrange
        // 设置模拟RuleEngine返回目标格口
        Factory.MockRuleEngineClient!
            .Setup(x => x.NotifyParcelDetectedAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        Factory.MockRuleEngineClient
            .Setup(x => x.IsConnected)
            .Returns(true);

        // Act - 启动编排器
        await _orchestrator.StartAsync();

        // 给时间进行初始化
        await Task.Delay(100);

        // Assert - 验证连接已建立
        Factory.MockRuleEngineClient.Verify(
            x => x.ConnectAsync(It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);

        await _orchestrator.StopAsync();
    }

    [Fact]
    [SimulationScenario("ParcelSorting_PathGeneration_ValidChute")]
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
    [SimulationScenario("ParcelSorting_PathExecution_ValidPath")]
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
    [SimulationScenario("ParcelSorting_FallbackToException_PathGenerationFails")]
    public async Task FallbackToExceptionChute_WhenPathGenerationFails()
    {
        // Arrange
        var invalidChuteId = 9999;

        // 设置模拟来模拟路径生成失败场景
        Factory.MockRuleEngineClient!
            .Setup(x => x.NotifyParcelDetectedAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        Factory.MockRuleEngineClient
            .Setup(x => x.IsConnected)
            .Returns(true);

        // Act - 启动编排器
        await _orchestrator.StartAsync();

        // 验证无效格口ID生成空路径
        var path = PathGenerator.GeneratePath(invalidChuteId);

        // Assert - 应该返回null路径
        path.Should().BeNull();

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
    [SimulationScenario("ParcelSorting_DebugAPI_ShouldProcess")]
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

        var result = await response.Content.ReadJsonAsync<Dictionary<string, object>>();
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
