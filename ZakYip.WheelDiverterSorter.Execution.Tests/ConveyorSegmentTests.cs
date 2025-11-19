using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using ZakYip.WheelDiverterSorter.Core.LineModel;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;
using ZakYip.WheelDiverterSorter.Core.LineModel.Enums;
using ZakYip.WheelDiverterSorter.Drivers.Vendors.Simulated;
using ZakYip.WheelDiverterSorter.Execution;

namespace ZakYip.WheelDiverterSorter.Execution.Tests;

public class ConveyorSegmentTests
{
    private ConveyorSegment CreateTestSegment(
        string segmentKey = "Middle1",
        int? runningInputChannel = 201)
    {
        var mapping = new ConveyorIoMapping
        {
            SegmentKey = segmentKey,
            DisplayName = "中段皮带1",
            StartOutputChannel = 100,
            RunningInputChannel = runningInputChannel,
            Priority = 1,
            StartTimeoutMs = 1000,
            StopTimeoutMs = 1000
        };

        var driver = new SimulatedConveyorSegmentDriver(
            mapping,
            NullLogger<SimulatedConveyorSegmentDriver>.Instance);

        return new ConveyorSegment(driver, NullLogger<ConveyorSegment>.Instance);
    }

    [Fact]
    public void Constructor_ShouldInitializeWithStoppedState()
    {
        // Arrange & Act
        var segment = CreateTestSegment();

        // Assert
        Assert.Equal(ConveyorSegmentState.Stopped, segment.State);
        Assert.Equal("Middle1", segment.SegmentId.Key);
    }

    [Fact]
    public async Task StartAsync_ShouldTransitionToRunningState()
    {
        // Arrange
        var segment = CreateTestSegment();

        // Act
        var result = await segment.StartAsync();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(ConveyorSegmentState.Running, segment.State);
    }

    [Fact]
    public async Task StartAsync_WhenAlreadyRunning_ShouldReturnSuccess()
    {
        // Arrange
        var segment = CreateTestSegment();
        await segment.StartAsync();

        // Act
        var result = await segment.StartAsync();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(ConveyorSegmentState.Running, segment.State);
    }

    [Fact]
    public async Task StopAsync_ShouldTransitionToStoppedState()
    {
        // Arrange
        var segment = CreateTestSegment();
        await segment.StartAsync();

        // Act
        var result = await segment.StopAsync();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(ConveyorSegmentState.Stopped, segment.State);
    }

    [Fact]
    public async Task StopAsync_WhenAlreadyStopped_ShouldReturnSuccess()
    {
        // Arrange
        var segment = CreateTestSegment();

        // Act
        var result = await segment.StopAsync();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(ConveyorSegmentState.Stopped, segment.State);
    }

    [Fact]
    public async Task StartAsync_WithoutRunningFeedback_ShouldAssumeSuccess()
    {
        // Arrange - 没有运行反馈配置
        var segment = CreateTestSegment(runningInputChannel: null);

        // Act
        var result = await segment.StartAsync();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(ConveyorSegmentState.Running, segment.State);
    }

    [Fact]
    public void GetFaultInfo_WhenNoFault_ShouldReturnNull()
    {
        // Arrange
        var segment = CreateTestSegment();

        // Act
        var faultInfo = segment.GetFaultInfo();

        // Assert
        Assert.Null(faultInfo);
    }

    [Fact]
    public async Task StartAndStop_MultipleCycles_ShouldWorkCorrectly()
    {
        // Arrange
        var segment = CreateTestSegment();

        // Act & Assert - 第一轮
        var startResult1 = await segment.StartAsync();
        Assert.True(startResult1.IsSuccess);
        Assert.Equal(ConveyorSegmentState.Running, segment.State);

        var stopResult1 = await segment.StopAsync();
        Assert.True(stopResult1.IsSuccess);
        Assert.Equal(ConveyorSegmentState.Stopped, segment.State);

        // Act & Assert - 第二轮
        var startResult2 = await segment.StartAsync();
        Assert.True(startResult2.IsSuccess);
        Assert.Equal(ConveyorSegmentState.Running, segment.State);

        var stopResult2 = await segment.StopAsync();
        Assert.True(stopResult2.IsSuccess);
        Assert.Equal(ConveyorSegmentState.Stopped, segment.State);
    }
}
