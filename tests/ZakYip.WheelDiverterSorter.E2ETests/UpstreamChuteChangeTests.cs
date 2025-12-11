using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using Xunit;
using ZakYip.WheelDiverterSorter.Core.LineModel;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.LineModel.Routing;
using ZakYip.WheelDiverterSorter.Core.LineModel.Topology;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Application.Services;
using ZakYip.WheelDiverterSorter.Application.Services.Config;
using ZakYip.WheelDiverterSorter.Application.Services.Caching;
using ZakYip.WheelDiverterSorter.Application.Services.Health;
using ZakYip.WheelDiverterSorter.Application.Services.Sorting;
using ZakYip.WheelDiverterSorter.Application.Services.Simulation;
using ZakYip.WheelDiverterSorter.Application.Services.Metrics;
using ZakYip.WheelDiverterSorter.Application.Services.Topology;
using ZakYip.WheelDiverterSorter.Application.Services.Debug;
using ZakYip.WheelDiverterSorter.Execution;
using ZakYip.WheelDiverterSorter.Execution.Routing;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Execution;
using Microsoft.Extensions.Logging;
using Moq;
using ZakYip.WheelDiverterSorter.E2ETests.Simulation;
using ZakYip.WheelDiverterSorter.Core.Enums.Sorting;

namespace ZakYip.WheelDiverterSorter.E2ETests;

/// <summary>
/// 上游改口功能端到端测试
/// </summary>
public class UpstreamChuteChangeTests
{
    [Fact]
    [SimulationScenario("UpstreamChuteChange_PlanCreated_AcceptChange")]
    public async Task ChuteChange_WhenPlanIsCreated_ShouldAcceptChange()
    {
        // Arrange
        var repository = new TestRoutePlanRepository();
        var pathGenerator = new Mock<ISwitchingPathGenerator>();
        var replanner = new RouteReplanner(
            pathGenerator.Object,
            Mock.Of<ILogger<RouteReplanner>>());
        var service = new ChangeParcelChuteService(
            repository,
            replanner,
            Mock.Of<ISystemClock>(),
            Mock.Of<ILogger<ChangeParcelChuteService>>());

        var parcelId = 12345L;
        var originalChuteId = 10;
        var newChuteId = 20;

        // 创建初始路由计划
        var routePlan = new RoutePlan(parcelId, originalChuteId, DateTimeOffset.Now);
        await repository.SaveAsync(routePlan);

        // 配置路径生成器返回新路径
        pathGenerator.Setup(g => g.GeneratePath(newChuteId))
            .Returns(new SwitchingPath
            {
                TargetChuteId = newChuteId,
                Segments = new List<SwitchingPathSegment>(),
                GeneratedAt = DateTimeOffset.Now,
                FallbackChuteId = 999
            });

        var command = new ChangeParcelChuteCommand
        {
            ParcelId = parcelId,
            RequestedChuteId = newChuteId
        };

        // Act
        var result = await service.ChangeParcelChuteAsync(command);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(ChuteChangeOutcome.Accepted, result.Outcome);
        Assert.Equal(newChuteId, result.EffectiveChuteId);
    }

    [Fact]
    [SimulationScenario("UpstreamChuteChange_PlanCompleted_IgnoreChange")]
    public async Task ChuteChange_WhenPlanIsCompleted_ShouldIgnoreChange()
    {
        // Arrange
        var repository = new TestRoutePlanRepository();
        var replanner = new RouteReplanner(
            Mock.Of<ISwitchingPathGenerator>(),
            Mock.Of<ILogger<RouteReplanner>>());
        var service = new ChangeParcelChuteService(
            repository,
            replanner,
            Mock.Of<ISystemClock>(), 
            Mock.Of<ILogger<ChangeParcelChuteService>>());

        var parcelId = 12345L;
        var originalChuteId = 10;
        var newChuteId = 20;

        // 创建已完成的路由计划
        var routePlan = new RoutePlan(parcelId, originalChuteId, DateTimeOffset.Now);
        routePlan.MarkAsCompleted(DateTimeOffset.Now);
        await repository.SaveAsync(routePlan);

        var command = new ChangeParcelChuteCommand
        {
            ParcelId = parcelId,
            RequestedChuteId = newChuteId
        };

        // Act
        var result = await service.ChangeParcelChuteAsync(command);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(ChuteChangeOutcome.IgnoredAlreadyCompleted, result.Outcome);
        Assert.Equal(originalChuteId, result.EffectiveChuteId);
    }

    [Fact]
    [SimulationScenario("UpstreamChuteChange_PlanExceptionRouted_IgnoreChange")]
    public async Task ChuteChange_WhenPlanIsExceptionRouted_ShouldIgnoreChange()
    {
        // Arrange
        var repository = new TestRoutePlanRepository();
        var replanner = new RouteReplanner(
            Mock.Of<ISwitchingPathGenerator>(),
            Mock.Of<ILogger<RouteReplanner>>());
        var service = new ChangeParcelChuteService(
            repository,
            replanner,
            Mock.Of<ISystemClock>(), 
            Mock.Of<ILogger<ChangeParcelChuteService>>());

        var parcelId = 12345L;
        var originalChuteId = 10;
        var newChuteId = 20;

        // 创建已进入异常路径的路由计划
        var routePlan = new RoutePlan(parcelId, originalChuteId, DateTimeOffset.Now);
        routePlan.MarkAsExceptionRouted(DateTimeOffset.Now);
        await repository.SaveAsync(routePlan);

        var command = new ChangeParcelChuteCommand
        {
            ParcelId = parcelId,
            RequestedChuteId = newChuteId
        };

        // Act
        var result = await service.ChangeParcelChuteAsync(command);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(ChuteChangeOutcome.IgnoredExceptionRouted, result.Outcome);
        Assert.Equal(originalChuteId, result.EffectiveChuteId);
    }

    [Fact]
    [SimulationScenario("UpstreamChuteChange_AfterDeadline_RejectChange")]
    public async Task ChuteChange_WhenAfterDeadline_ShouldRejectChange()
    {
        // Arrange
        var repository = new TestRoutePlanRepository();
        var replanner = new RouteReplanner(
            Mock.Of<ISwitchingPathGenerator>(),
            Mock.Of<ILogger<RouteReplanner>>());
        var service = new ChangeParcelChuteService(
            repository,
            replanner,
            Mock.Of<ISystemClock>(), 
            Mock.Of<ILogger<ChangeParcelChuteService>>());

        var parcelId = 12345L;
        var originalChuteId = 10;
        var newChuteId = 20;

        var createdAt = DateTimeOffset.Now;
        var deadline = createdAt.AddSeconds(10);

        // 创建有截止时间的路由计划
        var routePlan = new RoutePlan(parcelId, originalChuteId, createdAt, deadline);
        await repository.SaveAsync(routePlan);

        var command = new ChangeParcelChuteCommand
        {
            ParcelId = parcelId,
            RequestedChuteId = newChuteId,
            RequestedAt = deadline.AddSeconds(5) // 超过截止时间
        };

        // Act
        var result = await service.ChangeParcelChuteAsync(command);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(ChuteChangeOutcome.RejectedTooLate, result.Outcome);
        Assert.Equal(originalChuteId, result.EffectiveChuteId);
    }

    [Fact]
    [SimulationScenario("UpstreamChuteChange_PlanNotFound_ReturnFailure")]
    public async Task ChuteChange_WhenPlanNotFound_ShouldReturnFailure()
    {
        // Arrange
        var repository = new TestRoutePlanRepository();
        var replanner = new RouteReplanner(
            Mock.Of<ISwitchingPathGenerator>(),
            Mock.Of<ILogger<RouteReplanner>>());
        var service = new ChangeParcelChuteService(
            repository,
            replanner,
            Mock.Of<ISystemClock>(), 
            Mock.Of<ILogger<ChangeParcelChuteService>>());

        var command = new ChangeParcelChuteCommand
        {
            ParcelId = 99999L, // 不存在的包裹
            RequestedChuteId = 20
        };

        // Act
        var result = await service.ChangeParcelChuteAsync(command);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("not found", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 测试用的路由计划仓储实现
    /// </summary>
    private class TestRoutePlanRepository : IRoutePlanRepository
    {
        private readonly Dictionary<long, RoutePlan> _plans = new();

        public Task<RoutePlan?> GetByParcelIdAsync(long parcelId, CancellationToken cancellationToken = default)
        {
            _plans.TryGetValue(parcelId, out var plan);
            return Task.FromResult(plan);
        }

        public Task SaveAsync(RoutePlan routePlan, CancellationToken cancellationToken = default)
        {
            _plans[routePlan.ParcelId] = routePlan;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(long parcelId, CancellationToken cancellationToken = default)
        {
            _plans.Remove(parcelId);
            return Task.CompletedTask;
        }
    }
}
