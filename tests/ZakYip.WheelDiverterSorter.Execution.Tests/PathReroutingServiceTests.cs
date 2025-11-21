using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using ZakYip.WheelDiverterSorter.Core.LineModel;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;
using ZakYip.WheelDiverterSorter.Core.Enums.Communication;
using ZakYip.WheelDiverterSorter.Core.Enums.Conveyor;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Enums.Routing;
using ZakYip.WheelDiverterSorter.Core.Enums.Sensors;
using ZakYip.WheelDiverterSorter.Core.Enums.System;
using ZakYip.WheelDiverterSorter.Core.LineModel.Topology;

namespace ZakYip.WheelDiverterSorter.Execution.Tests;

public class PathReroutingServiceTests
{
    private readonly Mock<IRouteConfigurationRepository> _mockRouteRepo;
    private readonly Mock<ISwitchingPathGenerator> _mockPathGenerator;
    private readonly PathReroutingService _service;

    public PathReroutingServiceTests()
    {
        _mockRouteRepo = new Mock<IRouteConfigurationRepository>();
        _mockPathGenerator = new Mock<ISwitchingPathGenerator>();
        _service = new PathReroutingService(
            _mockRouteRepo.Object,
            _mockPathGenerator.Object,
            NullLogger<PathReroutingService>.Instance);
    }

    [Fact]
    public async Task TryRerouteAsync_WhenPhysicalConstraintFailure_ShouldReturnFailure()
    {
        // Arrange
        var parcelId = 12345L;
        var path = CreateTestPath(101, new[] { 1, 2, 3 });
        var failedNodeId = 2;

        // Act
        var result = await _service.TryRerouteAsync(
            parcelId,
            path,
            failedNodeId,
            PathFailureReason.PhysicalConstraint);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("不适合重规划", result.FailureReason);
    }

    [Fact]
    public async Task TryRerouteAsync_WhenParcelDropout_ShouldReturnFailure()
    {
        // Arrange
        var parcelId = 12345L;
        var path = CreateTestPath(101, new[] { 1, 2, 3 });
        var failedNodeId = 2;

        // Act
        var result = await _service.TryRerouteAsync(
            parcelId,
            path,
            failedNodeId,
            PathFailureReason.ParcelDropout);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("不适合重规划", result.FailureReason);
    }

    [Fact]
    public async Task TryRerouteAsync_WhenFailedNodeNotInPath_ShouldReturnFailure()
    {
        // Arrange
        var parcelId = 12345L;
        var path = CreateTestPath(101, new[] { 1, 2, 3 });
        var failedNodeId = 99; // Not in path

        // Act
        var result = await _service.TryRerouteAsync(
            parcelId,
            path,
            failedNodeId,
            PathFailureReason.SensorTimeout);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("未在路径中找到失败节点", result.FailureReason);
    }

    [Fact]
    public async Task TryRerouteAsync_WhenNoRouteConfig_ShouldReturnFailure()
    {
        // Arrange
        var parcelId = 12345L;
        var path = CreateTestPath(101, new[] { 1, 2, 3 });
        var failedNodeId = 1;

        _mockRouteRepo
            .Setup(r => r.GetByChuteId(101))
            .Returns((ChuteRouteConfiguration?)null);

        // Act
        var result = await _service.TryRerouteAsync(
            parcelId,
            path,
            failedNodeId,
            PathFailureReason.SensorTimeout);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("未找到格口", result.FailureReason);
    }

    [Fact]
    public async Task TryRerouteAsync_WhenNoRemainingSegments_ShouldReturnFailure()
    {
        // Arrange
        var parcelId = 12345L;
        var path = CreateTestPath(101, new[] { 1, 2, 3 });
        var failedNodeId = 3; // Last node

        var routeConfig = CreateTestRouteConfig(101, new[] { 1, 2, 3 });
        _mockRouteRepo
            .Setup(r => r.GetByChuteId(101))
            .Returns(routeConfig);

        // Act
        var result = await _service.TryRerouteAsync(
            parcelId,
            path,
            failedNodeId,
            PathFailureReason.SensorTimeout);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("没有剩余节点", result.FailureReason);
    }

    [Fact]
    public async Task TryRerouteAsync_WhenRemainingSegmentsMatchRequired_ShouldReturnSuccess()
    {
        // Arrange
        var parcelId = 12345L;
        var path = CreateTestPath(101, new[] { 1, 2, 3, 4 });
        var failedNodeId = 2; // Fail at node 2, remaining are 3, 4

        var routeConfig = CreateTestRouteConfig(101, new[] { 3, 4 });
        _mockRouteRepo
            .Setup(r => r.GetByChuteId(101))
            .Returns(routeConfig);

        // Act
        var result = await _service.TryRerouteAsync(
            parcelId,
            path,
            failedNodeId,
            PathFailureReason.SensorTimeout);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.NewPath);
        Assert.Equal(2, result.NewPath!.Segments.Count);
        Assert.Equal(3, result.NewPath.Segments[0].DiverterId);
        Assert.Equal(4, result.NewPath.Segments[1].DiverterId);
    }

    [Fact]
    public async Task TryRerouteAsync_WhenRemainingSegmentsMissingRequired_ShouldReturnFailure()
    {
        // Arrange
        var parcelId = 12345L;
        var path = CreateTestPath(101, new[] { 1, 2, 5 }); // Missing node 3, 4
        var failedNodeId = 1;

        var routeConfig = CreateTestRouteConfig(101, new[] { 2, 3, 4, 5 });
        _mockRouteRepo
            .Setup(r => r.GetByChuteId(101))
            .Returns(routeConfig);

        // Act
        var result = await _service.TryRerouteAsync(
            parcelId,
            path,
            failedNodeId,
            PathFailureReason.TtlExpired);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("无法构成到达目标格口的完整路径", result.FailureReason);
    }

    private SwitchingPath CreateTestPath(int targetChuteId, int[] diverterIds)
    {
        var segments = diverterIds.Select((id, index) => new SwitchingPathSegment
        {
            SequenceNumber = index + 1,
            DiverterId = id,
            TargetDirection = DiverterDirection.Left,
            TtlMilliseconds = 5000
        }).ToList();

        return new SwitchingPath
        {
            TargetChuteId = targetChuteId,
            Segments = segments.AsReadOnly(),
            GeneratedAt = DateTimeOffset.UtcNow,
            FallbackChuteId = 999
        };
    }

    private ChuteRouteConfiguration CreateTestRouteConfig(int chuteId, int[] diverterIds)
    {
        var diverterConfigs = diverterIds.Select((id, index) => new DiverterConfigurationEntry
        {
            SequenceNumber = index + 1,
            DiverterId = id,
            TargetDirection = DiverterDirection.Left,
            SegmentLengthMm = 1000,
            SegmentSpeedMmPerSecond = 1000,
            SegmentToleranceTimeMs = 500
        }).ToList();

        return new ChuteRouteConfiguration
        {
            ChuteId = chuteId,
            DiverterConfigurations = diverterConfigs
        };
    }
}
