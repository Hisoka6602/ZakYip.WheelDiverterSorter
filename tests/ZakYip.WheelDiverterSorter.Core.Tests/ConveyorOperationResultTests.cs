using Xunit;
using ZakYip.WheelDiverterSorter.Core.LineModel;
using ZakYip.WheelDiverterSorter.Core.Enums.Communication;
using ZakYip.WheelDiverterSorter.Core.Enums.Conveyor;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Enums.Routing;
using ZakYip.WheelDiverterSorter.Core.Enums.Sensors;
using ZakYip.WheelDiverterSorter.Core.Enums.System;


using ZakYip.WheelDiverterSorter.Core.LineModel.Segments;namespace ZakYip.WheelDiverterSorter.Core.Tests;

public class ConveyorOperationResultTests
{
    [Fact]
    public void Success_ShouldCreateSuccessResult()
    {
        // Act
        var result = ConveyorOperationResult.Success();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Null(result.FailureReason);
    }

    [Fact]
    public void Success_WithSegmentId_ShouldIncludeSegmentId()
    {
        // Arrange
        var segmentId = new ConveyorSegmentId
        {
            Key = "Middle1",
            DisplayName = "中段皮带1",
            Priority = 1
        };

        // Act
        var result = ConveyorOperationResult.Success(segmentId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(segmentId, result.SegmentId);
    }

    [Fact]
    public void Failure_ShouldCreateFailureResult()
    {
        // Arrange
        var reason = "启动超时";

        // Act
        var result = ConveyorOperationResult.Failure(reason);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(reason, result.FailureReason);
    }

    [Fact]
    public void Failure_WithSegmentId_ShouldIncludeSegmentIdAndReason()
    {
        // Arrange
        var segmentId = new ConveyorSegmentId
        {
            Key = "Middle2",
            DisplayName = "中段皮带2",
            Priority = 2
        };
        var reason = "检测到故障信号";

        // Act
        var result = ConveyorOperationResult.Failure(reason, segmentId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(reason, result.FailureReason);
        Assert.Equal(segmentId, result.SegmentId);
    }

    [Fact]
    public void Result_ShouldHaveTimestamp()
    {
        // Act
        var result = ConveyorOperationResult.Success();

        // Assert
        Assert.NotEqual(default, result.Timestamp);
        Assert.True((DateTimeOffset.UtcNow - result.Timestamp).TotalSeconds < 1);
    }
}
