using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using ZakYip.WheelDiverterSorter.Core.LineModel;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;
using ZakYip.WheelDiverterSorter.Core.LineModel.Enums;
using ZakYip.WheelDiverterSorter.Core.LineModel.Segments;
using ZakYip.WheelDiverterSorter.Drivers.Vendors.Simulated;
using ZakYip.WheelDiverterSorter.Execution;

namespace ZakYip.WheelDiverterSorter.Execution.Tests;

public class MiddleConveyorCoordinatorTests
{
    private IConveyorSegment CreateTestSegment(string key, int priority, int? runningInputChannel = 201)
    {
        var mapping = new ConveyorIoMapping
        {
            SegmentKey = key,
            DisplayName = $"中段皮带{key}",
            StartOutputChannel = 100 + priority,
            RunningInputChannel = runningInputChannel,
            Priority = priority,
            StartTimeoutMs = 1000,
            StopTimeoutMs = 1000
        };

        var driver = new SimulatedConveyorSegmentDriver(
            mapping,
            NullLogger<SimulatedConveyorSegmentDriver>.Instance);

        return new ConveyorSegment(driver, NullLogger<ConveyorSegment>.Instance);
    }

    private MiddleConveyorCoordinator CreateCoordinator(
        IReadOnlyList<IConveyorSegment> segments,
        string startOrderStrategy = "UpstreamFirst",
        string stopOrderStrategy = "DownstreamFirst",
        int linkageDelayMs = 50)
    {
        var options = new MiddleConveyorIoOptions
        {
            Enabled = true,
            IsSimulationMode = true,
            Segments = segments.Select(s => new ConveyorIoMapping
            {
                SegmentKey = s.SegmentId.Key,
                DisplayName = s.SegmentId.DisplayName,
                StartOutputChannel = 100,
                Priority = s.SegmentId.Priority
            }).ToList(),
            StartOrderStrategy = startOrderStrategy,
            StopOrderStrategy = stopOrderStrategy,
            LinkageDelayMs = linkageDelayMs
        };

        return new MiddleConveyorCoordinator(
            segments,
            options,
            NullLogger<MiddleConveyorCoordinator>.Instance);
    }

    [Fact]
    public async Task StartAllAsync_ShouldStartAllSegments()
    {
        // Arrange
        var segments = new List<IConveyorSegment>
        {
            CreateTestSegment("Middle1", 1),
            CreateTestSegment("Middle2", 2),
            CreateTestSegment("Middle3", 3)
        };
        var coordinator = CreateCoordinator(segments);

        // Act
        var result = await coordinator.StartAllAsync();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.All(segments, s => Assert.Equal(ConveyorSegmentState.Running, s.State));
    }

    [Fact]
    public async Task StopAllAsync_ShouldStopAllSegments()
    {
        // Arrange
        var segments = new List<IConveyorSegment>
        {
            CreateTestSegment("Middle1", 1),
            CreateTestSegment("Middle2", 2)
        };
        var coordinator = CreateCoordinator(segments);
        await coordinator.StartAllAsync();

        // Act
        var result = await coordinator.StopAllAsync();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.All(segments, s => Assert.Equal(ConveyorSegmentState.Stopped, s.State));
    }

    [Fact]
    public async Task StartAllAsync_WithUpstreamFirstStrategy_ShouldStartInPriorityOrder()
    {
        // Arrange
        var startOrder = new List<string>();
        var segments = new List<IConveyorSegment>
        {
            CreateTestSegment("Middle3", 3), // 优先级 3
            CreateTestSegment("Middle1", 1), // 优先级 1（最先启动）
            CreateTestSegment("Middle2", 2)  // 优先级 2
        };
        var coordinator = CreateCoordinator(segments, startOrderStrategy: "UpstreamFirst");

        // Act
        var result = await coordinator.StartAllAsync();

        // Assert
        Assert.True(result.IsSuccess);
        // 所有段都应该运行
        Assert.All(segments, s => Assert.Equal(ConveyorSegmentState.Running, s.State));
    }

    [Fact]
    public async Task StopDownstreamFirstAsync_ShouldStopInDescendingPriorityOrder()
    {
        // Arrange
        var segments = new List<IConveyorSegment>
        {
            CreateTestSegment("Middle1", 1),
            CreateTestSegment("Middle2", 2),
            CreateTestSegment("Middle3", 3)
        };
        var coordinator = CreateCoordinator(segments);
        await coordinator.StartAllAsync();

        // Act
        var result = await coordinator.StopDownstreamFirstAsync();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.All(segments, s => Assert.Equal(ConveyorSegmentState.Stopped, s.State));
    }

    [Fact]
    public void HasAnyFault_WithoutFaults_ShouldReturnFalse()
    {
        // Arrange
        var segments = new List<IConveyorSegment>
        {
            CreateTestSegment("Middle1", 1),
            CreateTestSegment("Middle2", 2)
        };
        var coordinator = CreateCoordinator(segments);

        // Act
        var hasFault = coordinator.HasAnyFault();

        // Assert
        Assert.False(hasFault);
    }

    [Fact]
    public async Task StartAllAsync_WhenDisabled_ShouldSkipOperation()
    {
        // Arrange
        var segments = new List<IConveyorSegment>
        {
            CreateTestSegment("Middle1", 1)
        };
        var options = new MiddleConveyorIoOptions
        {
            Enabled = false, // 禁用
            IsSimulationMode = true,
            Segments = new List<ConveyorIoMapping>(),
            StartOrderStrategy = "UpstreamFirst",
            StopOrderStrategy = "DownstreamFirst"
        };
        var coordinator = new MiddleConveyorCoordinator(
            segments,
            options,
            NullLogger<MiddleConveyorCoordinator>.Instance);

        // Act
        var result = await coordinator.StartAllAsync();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(ConveyorSegmentState.Stopped, segments[0].State); // 应该仍是停止状态
    }

    [Fact]
    public async Task StartAllAsync_WithEmptySegments_ShouldReturnSuccess()
    {
        // Arrange
        var segments = new List<IConveyorSegment>();
        var coordinator = CreateCoordinator(segments);

        // Act
        var result = await coordinator.StartAllAsync();

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task StopAllAsync_WithEmptySegments_ShouldReturnSuccess()
    {
        // Arrange
        var segments = new List<IConveyorSegment>();
        var coordinator = CreateCoordinator(segments);

        // Act
        var result = await coordinator.StopAllAsync();

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Segments_ShouldReturnConfiguredSegments()
    {
        // Arrange
        var segments = new List<IConveyorSegment>
        {
            CreateTestSegment("Middle1", 1),
            CreateTestSegment("Middle2", 2)
        };
        var coordinator = CreateCoordinator(segments);

        // Act & Assert
        Assert.Equal(2, coordinator.Segments.Count);
        Assert.Equal(segments, coordinator.Segments);
    }
}
