using Moq;
using Xunit;
using ZakYip.WheelDiverterSorter.Core.LineModel;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;
using ZakYip.WheelDiverterSorter.Core.Enums;


using ZakYip.WheelDiverterSorter.Core.LineModel.Chutes;
using ZakYip.WheelDiverterSorter.Core.LineModel.Topology;namespace ZakYip.WheelDiverterSorter.Core.Tests;

public class DefaultSwitchingPathGeneratorTests
{
    private readonly Mock<IRouteConfigurationRepository> _mockRepository;
    private readonly DefaultSwitchingPathGenerator _generator;

    public DefaultSwitchingPathGeneratorTests()
    {
        _mockRepository = new Mock<IRouteConfigurationRepository>();
        _generator = new DefaultSwitchingPathGenerator(_mockRepository.Object);
    }

    [Fact]
    public void GeneratePath_WithValidChuteId_ReturnsValidPath()
    {
        // Arrange
        var chuteId = 1;
        var config = new ChuteRouteConfiguration
        {
            ChuteId = chuteId,
            DiverterConfigurations = new List<DiverterConfigurationEntry>
            {
                new DiverterConfigurationEntry
                {
                    DiverterId = 1,
                    TargetDirection = DiverterDirection.Straight,
                    SequenceNumber = 1
                },
                new DiverterConfigurationEntry
                {
                    DiverterId = 2,
                    TargetDirection = DiverterDirection.Left,
                    SequenceNumber = 2
                }
            }
        };

        _mockRepository.Setup(r => r.GetByChuteId(chuteId)).Returns(config);

        // Act
        var result = _generator.GeneratePath(chuteId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(chuteId, result.TargetChuteId);
        Assert.Equal(2, result.Segments.Count);
        Assert.Equal(1, result.Segments[0].DiverterId);
        Assert.Equal(DiverterDirection.Straight, result.Segments[0].TargetDirection);
        Assert.Equal(1, result.Segments[0].SequenceNumber);
        Assert.Equal(2, result.Segments[1].DiverterId);
        Assert.Equal(DiverterDirection.Left, result.Segments[1].TargetDirection);
        Assert.Equal(2, result.Segments[1].SequenceNumber);
        Assert.Equal(WellKnownChuteIds.DefaultException, result.FallbackChuteId);
    }

    [Fact]
    public void GeneratePath_WithNullChuteId_ReturnsNull()
    {
        // Act
        var result = _generator.GeneratePath(0);

        // Assert
        Assert.Null(result);
        _mockRepository.Verify(r => r.GetByChuteId(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public void GeneratePath_WithEmptyChuteId_ReturnsNull()
    {
        // Act
        var result = _generator.GeneratePath(0);

        // Assert
        Assert.Null(result);
        _mockRepository.Verify(r => r.GetByChuteId(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public void GeneratePath_WithWhitespaceChuteId_ReturnsNull()
    {
        // Act
        var result = _generator.GeneratePath(0);

        // Assert
        Assert.Null(result);
        _mockRepository.Verify(r => r.GetByChuteId(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public void GeneratePath_WhenConfigNotFound_ReturnsNull()
    {
        // Arrange
        var chuteId = 999;
        _mockRepository.Setup(r => r.GetByChuteId(chuteId)).Returns((ChuteRouteConfiguration?)null);

        // Act
        var result = _generator.GeneratePath(chuteId);

        // Assert
        Assert.Null(result);
        _mockRepository.Verify(r => r.GetByChuteId(chuteId), Times.Once);
    }

    [Fact]
    public void GeneratePath_WhenConfigHasEmptyDiverters_ReturnsNull()
    {
        // Arrange
        var chuteId = 1;
        var config = new ChuteRouteConfiguration
        {
            ChuteId = chuteId,
            DiverterConfigurations = new List<DiverterConfigurationEntry>()
        };

        _mockRepository.Setup(r => r.GetByChuteId(chuteId)).Returns(config);

        // Act
        var result = _generator.GeneratePath(chuteId);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GeneratePath_OrdersSegmentsBySequenceNumber()
    {
        // Arrange
        var chuteId = 1;
        var config = new ChuteRouteConfiguration
        {
            ChuteId = chuteId,
            DiverterConfigurations = new List<DiverterConfigurationEntry>
            {
                new DiverterConfigurationEntry
                {
                    DiverterId = 3,
                    TargetDirection = DiverterDirection.Right,
                    SequenceNumber = 3
                },
                new DiverterConfigurationEntry
                {
                    DiverterId = 1,
                    TargetDirection = DiverterDirection.Straight,
                    SequenceNumber = 1
                },
                new DiverterConfigurationEntry
                {
                    DiverterId = 2,
                    TargetDirection = DiverterDirection.Left,
                    SequenceNumber = 2
                }
            }
        };

        _mockRepository.Setup(r => r.GetByChuteId(chuteId)).Returns(config);

        // Act
        var result = _generator.GeneratePath(chuteId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Segments.Count);
        Assert.Equal(1, result.Segments[0].DiverterId);
        Assert.Equal(2, result.Segments[1].DiverterId);
        Assert.Equal(3, result.Segments[2].DiverterId);
    }

    [Fact]
    public void GeneratePath_CalculatesTtlDynamicallyBasedOnSegmentConfiguration()
    {
        // Arrange
        var chuteId = 1;
        var config = new ChuteRouteConfiguration
        {
            ChuteId = chuteId,
            DiverterConfigurations = new List<DiverterConfigurationEntry>
            {
                new DiverterConfigurationEntry
                {
                    DiverterId = 1,
                    TargetDirection = DiverterDirection.Straight,
                    SequenceNumber = 1,
                    SegmentLengthMm = 5000.0,
                    SegmentSpeedMmPerSecond = 1000.0,
                    SegmentToleranceTimeMs = 2000
                }
            }
        };

        _mockRepository.Setup(r => r.GetByChuteId(chuteId)).Returns(config);

        // Act
        var result = _generator.GeneratePath(chuteId);

        // Assert
        Assert.NotNull(result);
        // TTL = (5.0 / 1.0) * 1000 + 2000 = 7000ms
        Assert.All(result.Segments, segment => Assert.Equal(7000, segment.TtlMilliseconds));
    }

    [Fact]
    public void Constructor_WithNullRepository_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new DefaultSwitchingPathGenerator(null!));
    }

    [Fact]
    public void GeneratePath_SetsGeneratedAtToCurrentTime()
    {
        // Arrange
        var chuteId = 1;
        var config = new ChuteRouteConfiguration
        {
            ChuteId = chuteId,
            DiverterConfigurations = new List<DiverterConfigurationEntry>
            {
                new DiverterConfigurationEntry
                {
                    DiverterId = 1,
                    TargetDirection = DiverterDirection.Straight,
                    SequenceNumber = 1
                }
            }
        };

        _mockRepository.Setup(r => r.GetByChuteId(chuteId)).Returns(config);
        var before = DateTimeOffset.UtcNow;

        // Act
        var result = _generator.GeneratePath(chuteId);
        var after = DateTimeOffset.UtcNow;

        // Assert
        Assert.NotNull(result);
        Assert.True(result.GeneratedAt >= before);
        Assert.True(result.GeneratedAt <= after);
    }

    [Theory]
    [InlineData(DiverterDirection.Straight)]
    [InlineData(DiverterDirection.Left)]
    [InlineData(DiverterDirection.Right)]
    public void GeneratePath_PreservesTargetDirection(DiverterDirection direction)
    {
        // Arrange
        var chuteId = 1;
        var config = new ChuteRouteConfiguration
        {
            ChuteId = chuteId,
            DiverterConfigurations = new List<DiverterConfigurationEntry>
            {
                new DiverterConfigurationEntry
                {
                    DiverterId = 1,
                    TargetDirection = direction,
                    SequenceNumber = 1
                }
            }
        };

        _mockRepository.Setup(r => r.GetByChuteId(chuteId)).Returns(config);

        // Act
        var result = _generator.GeneratePath(chuteId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(direction, result.Segments[0].TargetDirection);
    }

    [Fact]
    public void GeneratePath_CalculatesTtlWithDifferentSegmentSpeeds()
    {
        // Arrange
        var chuteId = 1;
        var config = new ChuteRouteConfiguration
        {
            ChuteId = chuteId,
            DiverterConfigurations = new List<DiverterConfigurationEntry>
            {
                new DiverterConfigurationEntry
                {
                    DiverterId = 1,
                    TargetDirection = DiverterDirection.Straight,
                    SequenceNumber = 1,
                    SegmentLengthMm = 10000.0,
                    SegmentSpeedMmPerSecond = 2000.0,
                    SegmentToleranceTimeMs = 1000
                },
                new DiverterConfigurationEntry
                {
                    DiverterId = 2,
                    TargetDirection = DiverterDirection.Right,
                    SequenceNumber = 2,
                    SegmentLengthMm = 5000.0,
                    SegmentSpeedMmPerSecond = 1500.0,
                    SegmentToleranceTimeMs = 1500
                }
            }
        };

        _mockRepository.Setup(r => r.GetByChuteId(chuteId)).Returns(config);

        // Act
        var result = _generator.GeneratePath(chuteId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Segments.Count);
        // Segment 1: (10.0 / 2.0) * 1000 + 1000 = 6000ms
        Assert.Equal(6000, result.Segments[0].TtlMilliseconds);
        // Segment 2: (5.0 / 1.5) * 1000 + 1500 = 3333 + 1500 = 4834ms (rounded up)
        Assert.Equal(4834, result.Segments[1].TtlMilliseconds);
    }

    [Fact]
    public void GeneratePath_EnforcesMinimumTtl()
    {
        // Arrange
        var chuteId = 1;
        var config = new ChuteRouteConfiguration
        {
            ChuteId = chuteId,
            DiverterConfigurations = new List<DiverterConfigurationEntry>
            {
                new DiverterConfigurationEntry
                {
                    DiverterId = 1,
                    TargetDirection = DiverterDirection.Straight,
                    SequenceNumber = 1,
                    SegmentLengthMm = 100.0,  // Very short segment
                    SegmentSpeedMmPerSecond = 10000.0,  // Very fast
                    SegmentToleranceTimeMs = 0  // No tolerance
                }
            }
        };

        _mockRepository.Setup(r => r.GetByChuteId(chuteId)).Returns(config);

        // Act
        var result = _generator.GeneratePath(chuteId);

        // Assert
        Assert.NotNull(result);
        // Should enforce minimum TTL of 1000ms even though calculated value is much smaller
        Assert.All(result.Segments, segment => Assert.True(segment.TtlMilliseconds >= 1000));
    }

    [Fact]
    public void GeneratePath_CalculatesTtlForMultipleSegmentsIndependently()
    {
        // Arrange
        var chuteId = 1;
        var config = new ChuteRouteConfiguration
        {
            ChuteId = chuteId,
            DiverterConfigurations = new List<DiverterConfigurationEntry>
            {
                new DiverterConfigurationEntry
                {
                    DiverterId = 1,
                    TargetDirection = DiverterDirection.Straight,
                    SequenceNumber = 1,
                    SegmentLengthMm = 5000.0,
                    SegmentSpeedMmPerSecond = 1000.0,
                    SegmentToleranceTimeMs = 2000
                },
                new DiverterConfigurationEntry
                {
                    DiverterId = 2,
                    TargetDirection = DiverterDirection.Straight,
                    SequenceNumber = 2,
                    SegmentLengthMm = 8000.0,
                    SegmentSpeedMmPerSecond = 1500.0,
                    SegmentToleranceTimeMs = 2500
                },
                new DiverterConfigurationEntry
                {
                    DiverterId = 3,
                    TargetDirection = DiverterDirection.Right,
                    SequenceNumber = 3,
                    SegmentLengthMm = 6000.0,
                    SegmentSpeedMmPerSecond = 2000.0,
                    SegmentToleranceTimeMs = 1500
                }
            }
        };

        _mockRepository.Setup(r => r.GetByChuteId(chuteId)).Returns(config);

        // Act
        var result = _generator.GeneratePath(chuteId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Segments.Count);
        // Segment 1: (5.0 / 1.0) * 1000 + 2000 = 7000ms
        Assert.Equal(7000, result.Segments[0].TtlMilliseconds);
        // Segment 2: (8.0 / 1.5) * 1000 + 2500 = 5333 + 2500 = 7834ms (rounded up)
        Assert.Equal(7834, result.Segments[1].TtlMilliseconds);
        // Segment 3: (6.0 / 2.0) * 1000 + 1500 = 4500ms
        Assert.Equal(4500, result.Segments[2].TtlMilliseconds);
    }

    [Theory]
    [InlineData(1000, 400, true)]  // Tolerance 400ms < 500ms (1000/2), should be valid
    [InlineData(1000, 500, false)] // Tolerance 500ms = 500ms (1000/2), should be invalid (not strictly less than)
    [InlineData(1000, 600, false)] // Tolerance 600ms > 500ms (1000/2), should be invalid
    [InlineData(2000, 999, true)]  // Tolerance 999ms < 1000ms (2000/2), should be valid
    [InlineData(2000, 1000, false)] // Tolerance 1000ms = 1000ms (2000/2), should be invalid
    [InlineData(500, 200, true)]   // Tolerance 200ms < 250ms (500/2), should be valid
    [InlineData(500, 250, false)]  // Tolerance 250ms = 250ms (500/2), should be invalid
    public void ValidateToleranceTime_WithVariousIntervals_ReturnsExpectedResult(
        int parcelIntervalMs, int toleranceMs, bool expectedValid)
    {
        // Arrange
        var segmentConfig = new DiverterConfigurationEntry
        {
            DiverterId = 1,
            TargetDirection = DiverterDirection.Straight,
            SequenceNumber = 1,
            SegmentLengthMm = 5000.0,
            SegmentSpeedMmPerSecond = 1000.0,
            SegmentToleranceTimeMs = toleranceMs
        };

        // Act
        var result = DefaultSwitchingPathGenerator.ValidateToleranceTime(segmentConfig, parcelIntervalMs);

        // Assert
        Assert.Equal(expectedValid, result);
    }

    [Fact]
    public void ValidateToleranceTime_WithZeroInterval_ThrowsArgumentException()
    {
        // Arrange
        var segmentConfig = new DiverterConfigurationEntry
        {
            DiverterId = 1,
            TargetDirection = DiverterDirection.Straight,
            SequenceNumber = 1,
            SegmentToleranceTimeMs = 1000
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            DefaultSwitchingPathGenerator.ValidateToleranceTime(segmentConfig, 0));
    }

    [Fact]
    public void ValidateToleranceTime_WithNegativeInterval_ThrowsArgumentException()
    {
        // Arrange
        var segmentConfig = new DiverterConfigurationEntry
        {
            DiverterId = 1,
            TargetDirection = DiverterDirection.Straight,
            SequenceNumber = 1,
            SegmentToleranceTimeMs = 1000
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            DefaultSwitchingPathGenerator.ValidateToleranceTime(segmentConfig, -100));
    }
}
