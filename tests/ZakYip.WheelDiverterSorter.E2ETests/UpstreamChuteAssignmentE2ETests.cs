using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Upstream;
using ZakYip.WheelDiverterSorter.Core.Enums.Sorting;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel.Routing;
using ZakYip.WheelDiverterSorter.Core.Sorting.Orchestration;
using ZakYip.WheelDiverterSorter.Core.Utilities;

namespace ZakYip.WheelDiverterSorter.E2ETests;

/// <summary>
/// 上游格口分配端到端测试
/// </summary>
/// <remarks>
/// 这是一个关键的E2E测试，验证整个系统的核心功能：
/// 1. 包裹检测后发送上游通知
/// 2. 接收上游的格口分配
/// 3. 格口分配正确保存到 RoutePlan
/// 4. 队列任务使用正确的格口ID生成
/// 
/// 如果这个测试失败，说明系统的核心功能被破坏了。
/// </remarks>
public class UpstreamChuteAssignmentE2ETests : E2ETestBase
{
    private readonly ISortingOrchestrator _orchestrator;
    private readonly IRoutePlanRepository _routePlanRepository;
    private readonly ISystemClock _systemClock;
    private readonly Random _random = new Random(12345); // 使用固定种子以确保测试可重现

    public UpstreamChuteAssignmentE2ETests(E2ETestFactory factory) : base(factory)
    {
        _orchestrator = Scope.ServiceProvider.GetRequiredService<ISortingOrchestrator>();
        _routePlanRepository = Scope.ServiceProvider.GetRequiredService<IRoutePlanRepository>();
        _systemClock = Scope.ServiceProvider.GetRequiredService<ISystemClock>();
        SetupDefaultRouteConfiguration();
    }

    /// <summary>
    /// 生成随机延迟时间（1-5秒）
    /// </summary>
    private int GetRandomUpstreamDelay()
    {
        return _random.Next(1000, 5001); // 1000ms (1s) 到 5000ms (5s)
    }

    /// <summary>
    /// 关键测试：验证上游格口分配的完整流程
    /// </summary>
    /// <remarks>
    /// 这个测试验证了系统的核心功能：
    /// 1. 配置系统为 Formal 模式
    /// 2. 设置 Mock 在收到发送请求时立即触发格口分配事件
    /// 3. 调用 ProcessParcelAsync 并等待完成
    /// 4. **验证 RoutePlan 是否正确保存了格口**
    /// 
    /// 这是最基础、最关键的测试。如果这个测试失败，说明事件订阅或 RoutePlan 保存有问题。
    /// </remarks>
    [Fact]
    [SimulationScenario("UpstreamChuteAssignment_EventSubscription_ShouldSaveToRoutePlan")]
    public async Task UpstreamChuteAssignment_EventSubscription_ShouldSaveToRoutePlan()
    {
        // Arrange
        var parcelId = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        var sensorId = 1L;
        var assignedChuteId = 3L;

        // 设置系统为 Formal 模式
        var systemConfig = SystemRepository.Get();
        systemConfig.SortingMode = SortingMode.Formal;
        SystemRepository.Update(systemConfig);

        // 设置 Mock
        Factory.MockRuleEngineClient!
            .Setup(x => x.IsConnected)
            .Returns(true);

        // 关键：设置 SendAsync 被调用时，在后台延迟后触发 ChuteAssigned 事件
        Factory.MockRuleEngineClient
            .Setup(x => x.SendAsync(It.IsAny<ParcelDetectedMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .Callback<IUpstreamMessage, CancellationToken>((msg, ct) =>
            {
                if (msg is ParcelDetectedMessage pdm)
                {
                    // 模拟上游延迟响应（随机1-5秒）
                    var delay = GetRandomUpstreamDelay();
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(delay, ct);
                        if (!ct.IsCancellationRequested)
                        {
                            var args = new ChuteAssignmentEventArgs
                            {
                                ParcelId = pdm.ParcelId,
                                ChuteId = assignedChuteId,
                                AssignedAt = new DateTimeOffset(_systemClock.LocalNow)
                            };
                            Factory.MockRuleEngineClient.Raise(
                                x => x.ChuteAssigned += null,
                                Factory.MockRuleEngineClient.Object,
                                args);
                        }
                    }, ct);
                }
            });

        // 启动 Orchestrator（这会订阅 ChuteAssigned 事件）
        await _orchestrator.StartAsync();

        // Act - 处理包裹（会发送上游通知并等待响应）
        var result = await _orchestrator.ProcessParcelAsync(parcelId, sensorId);

        // Assert - **关键验证**：检查分拣结果使用了上游分配的格口
        result.Should().NotBeNull("应该返回分拣结果");
        result.ActualChuteId.Should().Be(assignedChuteId,
            $"应该使用上游分配的格口 {assignedChuteId}，而不是异常格口");

        // **关键验证**：检查 RoutePlan 是否正确保存了上游分配的格口
        var routePlan = await _routePlanRepository.GetByParcelIdAsync(parcelId);
        routePlan.Should().NotBeNull($"包裹 {parcelId} 应该有 RoutePlan");
        routePlan!.CurrentTargetChuteId.Should().Be(assignedChuteId,
            $"RoutePlan 中应该保存上游分配的格口 {assignedChuteId}");

        // 验证上游通知被发送了
        Factory.MockRuleEngineClient.Verify(
            x => x.SendAsync(It.Is<ParcelDetectedMessage>(m => m.ParcelId == parcelId), It.IsAny<CancellationToken>()),
            Times.Once,
            "应该向上游发送包裹检测通知");

        await _orchestrator.StopAsync();
    }

    /// <summary>
    /// 测试：当上游未及时响应时，应该使用异常格口
    /// </summary>
    [Fact]
    [SimulationScenario("UpstreamChuteAssignment_Timeout_ShouldUseExceptionChute")]
    public async Task UpstreamChuteAssignment_WhenTimeout_ShouldUseExceptionChute()
    {
        // Arrange
        var parcelId = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        var sensorId = 1L;
        var systemConfig = SystemRepository.Get();
        var exceptionChuteId = systemConfig.ExceptionChuteId;

        // 设置超时时间为1秒（用于快速测试）
        systemConfig.ChuteAssignmentTimeout = new ChuteAssignmentTimeoutOptions
        {
            FallbackTimeoutSeconds = 1,
            SafetyFactor = 0.9m
        };

        Factory.MockRuleEngineClient!
            .Setup(x => x.IsConnected)
            .Returns(true);

        Factory.MockRuleEngineClient
            .Setup(x => x.SendAsync(It.IsAny<IUpstreamMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // 设置系统为 Formal 模式
        systemConfig.SortingMode = SortingMode.Formal;
        SystemRepository.Update(systemConfig);

        await _orchestrator.StartAsync();

        // Act - 处理包裹（不触发格口分配事件，模拟超时）
        var result = await _orchestrator.ProcessParcelAsync(parcelId, sensorId);

        // Assert
        result.Should().NotBeNull("应该返回分拣结果");
        result.ActualChuteId.Should().Be(exceptionChuteId,
            "超时时应该使用异常格口");

        // 验证上游通知被发送了
        Factory.MockRuleEngineClient.Verify(
            x => x.SendAsync(It.Is<ParcelDetectedMessage>(m => m.ParcelId == parcelId), It.IsAny<CancellationToken>()),
            Times.Once,
            "应该向上游发送包裹检测通知");

        await _orchestrator.StopAsync();
    }

    /// <summary>
    /// 测试：验证迟到的上游响应不会影响已经超时的包裹
    /// </summary>
    [Fact]
    [SimulationScenario("UpstreamChuteAssignment_LateResponse_ShouldNotAffectTimedOutParcel")]
    public async Task UpstreamChuteAssignment_LateResponse_ShouldUpdateRoutePlanButNotAffectResult()
    {
        // Arrange
        var parcelId = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        var sensorId = 1L;
        var lateChuteId = 5L;
        var systemConfig = SystemRepository.Get();
        var exceptionChuteId = systemConfig.ExceptionChuteId;

        // 设置超时时间为1秒
        systemConfig.ChuteAssignmentTimeout = new ChuteAssignmentTimeoutOptions
        {
            FallbackTimeoutSeconds = 1,
            SafetyFactor = 0.9m
        };

        Factory.MockRuleEngineClient!
            .Setup(x => x.IsConnected)
            .Returns(true);

        Factory.MockRuleEngineClient
            .Setup(x => x.SendAsync(It.IsAny<IUpstreamMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        systemConfig.SortingMode = SortingMode.Formal;
        SystemRepository.Update(systemConfig);

        await _orchestrator.StartAsync();

        // Act - 处理包裹（会超时）
        var result = await _orchestrator.ProcessParcelAsync(parcelId, sensorId);

        // 超时后，模拟上游迟到的响应
        await Task.Delay(500);
        var lateAssignmentArgs = new ChuteAssignmentEventArgs
        {
            ParcelId = parcelId,
            ChuteId = lateChuteId,
            AssignedAt = new DateTimeOffset(_systemClock.LocalNow)
        };

        Factory.MockRuleEngineClient.Raise(
            x => x.ChuteAssigned += null,
            Factory.MockRuleEngineClient.Object,
            lateAssignmentArgs);

        // 给一点时间让迟到的响应被处理
        await Task.Delay(500);

        // Assert
        
        // 1. 分拣结果应该使用异常格口（因为已经超时）
        result.ActualChuteId.Should().Be(exceptionChuteId,
            "已超时的包裹应该使用异常格口");

        // 2. 但 RoutePlan 应该被迟到的响应更新
        var routePlan = await _routePlanRepository.GetByParcelIdAsync(parcelId);
        routePlan.Should().NotBeNull("即使迟到，RoutePlan 也应该被更新");
        routePlan!.CurrentTargetChuteId.Should().Be(lateChuteId,
            "迟到的上游响应应该更新 RoutePlan");

        await _orchestrator.StopAsync();
    }
}
