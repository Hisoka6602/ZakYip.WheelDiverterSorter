using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ZakYip.WheelDiverterSorter.Core;
using ZakYip.WheelDiverterSorter.Drivers;
using ZakYip.WheelDiverterSorter.Drivers.Abstractions;

namespace ZakYip.WheelDiverterSorter.Drivers.Tests;

public class HardwareSwitchingPathExecutorTests
{
    private readonly Mock<ILogger<HardwareSwitchingPathExecutor>> _mockLogger;

    public HardwareSwitchingPathExecutorTests()
    {
        _mockLogger = new Mock<ILogger<HardwareSwitchingPathExecutor>>();
    }

    [Fact]
    public async Task ExecuteAsync_WithValidPath_ReturnsSuccess()
    {
        // Arrange
        var mockDiverter1 = new Mock<IDiverterController>();
        mockDiverter1.Setup(d => d.DiverterId).Returns("Diverter1");
        mockDiverter1.Setup(d => d.SetAngleAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var mockDiverter2 = new Mock<IDiverterController>();
        mockDiverter2.Setup(d => d.DiverterId).Returns("Diverter2");
        mockDiverter2.Setup(d => d.SetAngleAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var executor = new HardwareSwitchingPathExecutor(
            _mockLogger.Object,
            new[] { mockDiverter1.Object, mockDiverter2.Object });

        var path = new SwitchingPath
        {
            TargetChuteId = "Chute01",
            FallbackChuteId = "Exception",
            GeneratedAt = DateTimeOffset.UtcNow,
            Segments = new List<SwitchingPathSegment>
            {
                new SwitchingPathSegment
                {
                    SequenceNumber = 1,
                    DiverterId = "Diverter1",
                    TargetDirection = DiverterDirection.Straight,
                    TtlMilliseconds = 5000
                },
                new SwitchingPathSegment
                {
                    SequenceNumber = 2,
                    DiverterId = "Diverter2",
                    TargetDirection = DiverterDirection.Left,
                    TtlMilliseconds = 5000
                }
            }.AsReadOnly()
        };

        // Act
        var result = await executor.ExecuteAsync(path);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Chute01", result.ActualChuteId);
        Assert.Null(result.FailureReason);

        mockDiverter1.Verify(d => d.SetAngleAsync(0, It.IsAny<CancellationToken>()), Times.Once);
        mockDiverter2.Verify(d => d.SetAngleAsync(45, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullPath_ThrowsArgumentNullException()
    {
        // Arrange
        var executor = new HardwareSwitchingPathExecutor(_mockLogger.Object, Array.Empty<IDiverterController>());

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => executor.ExecuteAsync(null!));
    }

    [Fact]
    public async Task ExecuteAsync_WhenDiverterNotFound_ReturnsFailure()
    {
        // Arrange
        var mockDiverter1 = new Mock<IDiverterController>();
        mockDiverter1.Setup(d => d.DiverterId).Returns("Diverter1");

        var executor = new HardwareSwitchingPathExecutor(
            _mockLogger.Object,
            new[] { mockDiverter1.Object });

        var path = new SwitchingPath
        {
            TargetChuteId = "Chute01",
            FallbackChuteId = "Exception",
            GeneratedAt = DateTimeOffset.UtcNow,
            Segments = new List<SwitchingPathSegment>
            {
                new SwitchingPathSegment
                {
                    SequenceNumber = 1,
                    DiverterId = "NonExistentDiverter",
                    TargetDirection = DiverterDirection.Straight,
                    TtlMilliseconds = 5000
                }
            }.AsReadOnly()
        };

        // Act
        var result = await executor.ExecuteAsync(path);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("Exception", result.ActualChuteId);
        Assert.Contains("找不到摆轮控制器", result.FailureReason);
    }

    [Fact]
    public async Task ExecuteAsync_WhenDiverterFails_ReturnsFailure()
    {
        // Arrange
        var mockDiverter = new Mock<IDiverterController>();
        mockDiverter.Setup(d => d.DiverterId).Returns("Diverter1");
        mockDiverter.Setup(d => d.SetAngleAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var executor = new HardwareSwitchingPathExecutor(
            _mockLogger.Object,
            new[] { mockDiverter.Object });

        var path = new SwitchingPath
        {
            TargetChuteId = "Chute01",
            FallbackChuteId = "Exception",
            GeneratedAt = DateTimeOffset.UtcNow,
            Segments = new List<SwitchingPathSegment>
            {
                new SwitchingPathSegment
                {
                    SequenceNumber = 1,
                    DiverterId = "Diverter1",
                    TargetDirection = DiverterDirection.Straight,
                    TtlMilliseconds = 5000
                }
            }.AsReadOnly()
        };

        // Act
        var result = await executor.ExecuteAsync(path);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("Exception", result.ActualChuteId);
        Assert.Contains("执行失败", result.FailureReason);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancelled_ReturnsFailure()
    {
        // Arrange
        var mockDiverter = new Mock<IDiverterController>();
        mockDiverter.Setup(d => d.DiverterId).Returns("Diverter1");
        mockDiverter.Setup(d => d.SetAngleAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var executor = new HardwareSwitchingPathExecutor(
            _mockLogger.Object,
            new[] { mockDiverter.Object });

        var path = new SwitchingPath
        {
            TargetChuteId = "Chute01",
            FallbackChuteId = "Exception",
            GeneratedAt = DateTimeOffset.UtcNow,
            Segments = new List<SwitchingPathSegment>
            {
                new SwitchingPathSegment
                {
                    SequenceNumber = 1,
                    DiverterId = "Diverter1",
                    TargetDirection = DiverterDirection.Straight,
                    TtlMilliseconds = 5000
                }
            }.AsReadOnly()
        };

        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await executor.ExecuteAsync(path, cts.Token);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("Exception", result.ActualChuteId);
        Assert.Contains("取消", result.FailureReason);
    }

    [Fact]
    public async Task ExecuteAsync_WhenException_ReturnsFailure()
    {
        // Arrange
        var mockDiverter = new Mock<IDiverterController>();
        mockDiverter.Setup(d => d.DiverterId).Returns("Diverter1");
        mockDiverter.Setup(d => d.SetAngleAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Hardware error"));

        var executor = new HardwareSwitchingPathExecutor(
            _mockLogger.Object,
            new[] { mockDiverter.Object });

        var path = new SwitchingPath
        {
            TargetChuteId = "Chute01",
            FallbackChuteId = "Exception",
            GeneratedAt = DateTimeOffset.UtcNow,
            Segments = new List<SwitchingPathSegment>
            {
                new SwitchingPathSegment
                {
                    SequenceNumber = 1,
                    DiverterId = "Diverter1",
                    TargetDirection = DiverterDirection.Straight,
                    TtlMilliseconds = 5000
                }
            }.AsReadOnly()
        };

        // Act
        var result = await executor.ExecuteAsync(path);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("Exception", result.ActualChuteId);
        Assert.Contains("执行异常", result.FailureReason);
    }

    [Fact]
    public async Task ExecuteAsync_ExecutesSegmentsInOrder()
    {
        // Arrange
        var executionOrder = new List<string>();
        
        var mockDiverter1 = new Mock<IDiverterController>();
        mockDiverter1.Setup(d => d.DiverterId).Returns("Diverter1");
        mockDiverter1.Setup(d => d.SetAngleAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                executionOrder.Add("Diverter1");
                return true;
            });

        var mockDiverter2 = new Mock<IDiverterController>();
        mockDiverter2.Setup(d => d.DiverterId).Returns("Diverter2");
        mockDiverter2.Setup(d => d.SetAngleAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                executionOrder.Add("Diverter2");
                return true;
            });

        var mockDiverter3 = new Mock<IDiverterController>();
        mockDiverter3.Setup(d => d.DiverterId).Returns("Diverter3");
        mockDiverter3.Setup(d => d.SetAngleAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                executionOrder.Add("Diverter3");
                return true;
            });

        var executor = new HardwareSwitchingPathExecutor(
            _mockLogger.Object,
            new[] { mockDiverter1.Object, mockDiverter2.Object, mockDiverter3.Object });

        var path = new SwitchingPath
        {
            TargetChuteId = "Chute01",
            FallbackChuteId = "Exception",
            GeneratedAt = DateTimeOffset.UtcNow,
            Segments = new List<SwitchingPathSegment>
            {
                new SwitchingPathSegment
                {
                    SequenceNumber = 1,
                    DiverterId = "Diverter1",
                    TargetDirection = DiverterDirection.Straight,
                    TtlMilliseconds = 5000
                },
                new SwitchingPathSegment
                {
                    SequenceNumber = 2,
                    DiverterId = "Diverter2",
                    TargetDirection = DiverterDirection.Left,
                    TtlMilliseconds = 5000
                },
                new SwitchingPathSegment
                {
                    SequenceNumber = 3,
                    DiverterId = "Diverter3",
                    TargetDirection = DiverterDirection.Right,
                    TtlMilliseconds = 5000
                }
            }.AsReadOnly()
        };

        // Act
        var result = await executor.ExecuteAsync(path);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { "Diverter1", "Diverter2", "Diverter3" }, executionOrder);
    }

    [Fact]
    public async Task ExecuteAsync_StopsOnFirstFailure()
    {
        // Arrange
        var executionOrder = new List<string>();

        var mockDiverter1 = new Mock<IDiverterController>();
        mockDiverter1.Setup(d => d.DiverterId).Returns("Diverter1");
        mockDiverter1.Setup(d => d.SetAngleAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                executionOrder.Add("Diverter1");
                return true;
            });

        var mockDiverter2 = new Mock<IDiverterController>();
        mockDiverter2.Setup(d => d.DiverterId).Returns("Diverter2");
        mockDiverter2.Setup(d => d.SetAngleAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                executionOrder.Add("Diverter2");
                return false; // Failure
            });

        var mockDiverter3 = new Mock<IDiverterController>();
        mockDiverter3.Setup(d => d.DiverterId).Returns("Diverter3");
        mockDiverter3.Setup(d => d.SetAngleAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                executionOrder.Add("Diverter3");
                return true;
            });

        var executor = new HardwareSwitchingPathExecutor(
            _mockLogger.Object,
            new[] { mockDiverter1.Object, mockDiverter2.Object, mockDiverter3.Object });

        var path = new SwitchingPath
        {
            TargetChuteId = "Chute01",
            FallbackChuteId = "Exception",
            GeneratedAt = DateTimeOffset.UtcNow,
            Segments = new List<SwitchingPathSegment>
            {
                new SwitchingPathSegment
                {
                    SequenceNumber = 1,
                    DiverterId = "Diverter1",
                    TargetDirection = DiverterDirection.Straight,
                    TtlMilliseconds = 5000
                },
                new SwitchingPathSegment
                {
                    SequenceNumber = 2,
                    DiverterId = "Diverter2",
                    TargetDirection = DiverterDirection.Left,
                    TtlMilliseconds = 5000
                },
                new SwitchingPathSegment
                {
                    SequenceNumber = 3,
                    DiverterId = "Diverter3",
                    TargetDirection = DiverterDirection.Right,
                    TtlMilliseconds = 5000
                }
            }.AsReadOnly()
        };

        // Act
        var result = await executor.ExecuteAsync(path);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(new[] { "Diverter1", "Diverter2" }, executionOrder); // Diverter3 should not execute
        mockDiverter3.Verify(d => d.SetAngleAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
