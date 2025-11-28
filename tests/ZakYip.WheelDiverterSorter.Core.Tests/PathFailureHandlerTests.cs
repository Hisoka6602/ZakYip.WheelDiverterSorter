using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using ZakYip.WheelDiverterSorter.Core.LineModel;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Execution;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Execution;
using ZakYip.WheelDiverterSorter.Execution.Events;


using ZakYip.WheelDiverterSorter.Core.LineModel.Topology;namespace ZakYip.WheelDiverterSorter.Core.Tests;

/// <summary>
/// 路径失败处理器测试
/// </summary>
public class PathFailureHandlerTests
{
    private readonly Mock<ISwitchingPathGenerator> _pathGeneratorMock;
    private readonly PathFailureHandler _handler;

    public PathFailureHandlerTests()
    {
        _pathGeneratorMock = new Mock<ISwitchingPathGenerator>();
        _handler = new PathFailureHandler(
            _pathGeneratorMock.Object,
            NullLogger<PathFailureHandler>.Instance, new ZakYip.WheelDiverterSorter.Core.Utilities.LocalSystemClock());
    }

    [Fact]
    public void Constructor_WithNullPathGenerator_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new PathFailureHandler(null!, NullLogger<PathFailureHandler>.Instance, new ZakYip.WheelDiverterSorter.Core.Utilities.LocalSystemClock()));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new PathFailureHandler(_pathGeneratorMock.Object, null!, new ZakYip.WheelDiverterSorter.Core.Utilities.LocalSystemClock()));
    }

    [Fact]
    public void HandleSegmentFailure_RaisesSegmentExecutionFailedEvent()
    {
        // Arrange
        var parcelId = 12345L;
        var path = CreateTestPath(101);
        var segment = path.Segments[0];
        var failureReason = "测试失败原因";
        
        PathSegmentExecutionFailedEventArgs? raisedEventArgs = null;
        _handler.SegmentExecutionFailed += (sender, args) => raisedEventArgs = args;

        // Act
        _handler.HandleSegmentFailure(parcelId, path, segment, failureReason);

        // Assert
        Assert.NotNull(raisedEventArgs);
        Assert.Equal(parcelId, raisedEventArgs.Value.ParcelId);
        Assert.Equal(segment, raisedEventArgs.Value.FailedSegment);
        Assert.Equal(path.TargetChuteId, raisedEventArgs.Value.OriginalTargetChuteId);
        Assert.Equal(failureReason, raisedEventArgs.Value.FailureReason);
        Assert.Equal(segment.DiverterId, raisedEventArgs.Value.FailurePosition);
    }

    [Fact]
    public void HandleSegmentFailure_AlsoRaisesPathExecutionFailedEvent()
    {
        // Arrange
        var parcelId = 12345L;
        var path = CreateTestPath(101);
        var segment = path.Segments[0];
        var failureReason = "测试失败原因";
        
        PathExecutionFailedEventArgs? raisedEventArgs = null;
        _handler.PathExecutionFailed += (sender, args) => raisedEventArgs = args;

        // Act
        _handler.HandleSegmentFailure(parcelId, path, segment, failureReason);

        // Assert
        Assert.NotNull(raisedEventArgs);
        Assert.Equal(parcelId, raisedEventArgs.Value.ParcelId);
        Assert.Equal(path, raisedEventArgs.Value.OriginalPath);
        Assert.Equal(segment, raisedEventArgs.Value.FailedSegment);
        Assert.Equal(failureReason, raisedEventArgs.Value.FailureReason);
        Assert.Equal(path.FallbackChuteId, raisedEventArgs.Value.ActualChuteId);
    }

    [Fact]
    public void HandlePathFailure_RaisesPathExecutionFailedEvent()
    {
        // Arrange
        var parcelId = 12345L;
        var path = CreateTestPath(101);
        var failureReason = "路径执行失败";
        
        PathExecutionFailedEventArgs? raisedEventArgs = null;
        _handler.PathExecutionFailed += (sender, args) => raisedEventArgs = args;

        // Act
        _handler.HandlePathFailure(parcelId, path, failureReason);

        // Assert
        Assert.NotNull(raisedEventArgs);
        Assert.Equal(parcelId, raisedEventArgs.Value.ParcelId);
        Assert.Equal(path, raisedEventArgs.Value.OriginalPath);
        Assert.Null(raisedEventArgs.Value.FailedSegment);
        Assert.Equal(failureReason, raisedEventArgs.Value.FailureReason);
        Assert.Equal(path.FallbackChuteId, raisedEventArgs.Value.ActualChuteId);
    }

    [Fact]
    public void HandlePathFailure_WithFailedSegment_IncludesSegmentInEvent()
    {
        // Arrange
        var parcelId = 12345L;
        var path = CreateTestPath(101);
        var segment = path.Segments[0];
        var failureReason = "段执行失败";
        
        PathExecutionFailedEventArgs? raisedEventArgs = null;
        _handler.PathExecutionFailed += (sender, args) => raisedEventArgs = args;

        // Act
        _handler.HandlePathFailure(parcelId, path, failureReason, segment);

        // Assert
        Assert.NotNull(raisedEventArgs);
        Assert.Equal(segment, raisedEventArgs.Value.FailedSegment);
    }

    [Fact]
    public void HandlePathFailure_CalculatesBackupPathAndRaisesPathSwitchedEvent()
    {
        // Arrange
        var parcelId = 12345L;
        var path = CreateTestPath(101);
        var backupPath = CreateTestPath(999); // 异常格口路径
        var failureReason = "路径执行失败";
        
        _pathGeneratorMock
            .Setup(g => g.GeneratePath(path.FallbackChuteId))
            .Returns(backupPath);
        
        PathSwitchedEventArgs? raisedEventArgs = null;
        _handler.PathSwitched += (sender, args) => raisedEventArgs = args;

        // Act
        _handler.HandlePathFailure(parcelId, path, failureReason);

        // Assert
        Assert.NotNull(raisedEventArgs);
        Assert.Equal(parcelId, raisedEventArgs.Value.ParcelId);
        Assert.Equal(path, raisedEventArgs.Value.OriginalPath);
        Assert.Equal(backupPath, raisedEventArgs.Value.BackupPath);
        Assert.Equal(failureReason, raisedEventArgs.Value.SwitchReason);
    }

    [Fact]
    public void HandlePathFailure_WhenBackupPathCannotBeGenerated_DoesNotRaisePathSwitchedEvent()
    {
        // Arrange
        var parcelId = 12345L;
        var path = CreateTestPath(101);
        var failureReason = "路径执行失败";
        
        _pathGeneratorMock
            .Setup(g => g.GeneratePath(path.FallbackChuteId))
            .Returns((SwitchingPath?)null);
        
        var pathSwitchedEventRaised = false;
        _handler.PathSwitched += (sender, args) => pathSwitchedEventRaised = true;

        // Act
        _handler.HandlePathFailure(parcelId, path, failureReason);

        // Assert
        Assert.False(pathSwitchedEventRaised);
    }

    [Fact]
    public void CalculateBackupPath_CallsPathGeneratorWithFallbackChuteId()
    {
        // Arrange
        var path = CreateTestPath(101);
        var backupPath = CreateTestPath(999);
        
        _pathGeneratorMock
            .Setup(g => g.GeneratePath(path.FallbackChuteId))
            .Returns(backupPath);

        // Act
        var result = _handler.CalculateBackupPath(path);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(backupPath, result);
        _pathGeneratorMock.Verify(g => g.GeneratePath(path.FallbackChuteId), Times.Once);
    }

    [Fact]
    public void CalculateBackupPath_WhenPathGeneratorReturnsNull_ReturnsNull()
    {
        // Arrange
        var path = CreateTestPath(101);
        
        _pathGeneratorMock
            .Setup(g => g.GeneratePath(path.FallbackChuteId))
            .Returns((SwitchingPath?)null);

        // Act
        var result = _handler.CalculateBackupPath(path);

        // Assert
        Assert.Null(result);
    }

    private static SwitchingPath CreateTestPath(int targetChuteId)
    {
        return new SwitchingPath
        {
            TargetChuteId = targetChuteId,
            FallbackChuteId = 999, // 异常格口
            GeneratedAt = DateTimeOffset.Now,
            Segments = new List<SwitchingPathSegment>
            {
                new SwitchingPathSegment
                {
                    SequenceNumber = 1,
                    DiverterId = 1,
                    TargetDirection = DiverterDirection.Straight,
                    TtlMilliseconds = 5000
                },
                new SwitchingPathSegment
                {
                    SequenceNumber = 2,
                    DiverterId = 2,
                    TargetDirection = DiverterDirection.Left,
                    TtlMilliseconds = 5000
                }
            }.AsReadOnly()
        };
    }
}
