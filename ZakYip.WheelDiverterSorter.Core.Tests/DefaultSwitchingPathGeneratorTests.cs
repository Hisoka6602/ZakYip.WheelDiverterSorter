using Moq;
using Xunit;
using ZakYip.WheelDiverterSorter.Core;
using ZakYip.WheelDiverterSorter.Core.Configuration;

namespace ZakYip.WheelDiverterSorter.Core.Tests;

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
        var chuteId = "Chute01";
        var config = new ChuteRouteConfiguration
        {
            ChuteId = chuteId,
            DiverterConfigurations = new List<DiverterConfigurationEntry>
            {
                new DiverterConfigurationEntry
                {
                    DiverterId = "Diverter1",
                    TargetAngle = DiverterAngle.Angle0,
                    SequenceNumber = 1
                },
                new DiverterConfigurationEntry
                {
                    DiverterId = "Diverter2",
                    TargetAngle = DiverterAngle.Angle30,
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
        Assert.Equal("Diverter1", result.Segments[0].DiverterId);
        Assert.Equal(DiverterAngle.Angle0, result.Segments[0].TargetAngle);
        Assert.Equal(1, result.Segments[0].SequenceNumber);
        Assert.Equal("Diverter2", result.Segments[1].DiverterId);
        Assert.Equal(DiverterAngle.Angle30, result.Segments[1].TargetAngle);
        Assert.Equal(2, result.Segments[1].SequenceNumber);
        Assert.Equal(WellKnownChuteIds.DefaultException, result.FallbackChuteId);
    }

    [Fact]
    public void GeneratePath_WithNullChuteId_ReturnsNull()
    {
        // Act
        var result = _generator.GeneratePath(null!);

        // Assert
        Assert.Null(result);
        _mockRepository.Verify(r => r.GetByChuteId(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void GeneratePath_WithEmptyChuteId_ReturnsNull()
    {
        // Act
        var result = _generator.GeneratePath(string.Empty);

        // Assert
        Assert.Null(result);
        _mockRepository.Verify(r => r.GetByChuteId(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void GeneratePath_WithWhitespaceChuteId_ReturnsNull()
    {
        // Act
        var result = _generator.GeneratePath("   ");

        // Assert
        Assert.Null(result);
        _mockRepository.Verify(r => r.GetByChuteId(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void GeneratePath_WhenConfigNotFound_ReturnsNull()
    {
        // Arrange
        var chuteId = "NonExistentChute";
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
        var chuteId = "Chute01";
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
        var chuteId = "Chute01";
        var config = new ChuteRouteConfiguration
        {
            ChuteId = chuteId,
            DiverterConfigurations = new List<DiverterConfigurationEntry>
            {
                new DiverterConfigurationEntry
                {
                    DiverterId = "Diverter3",
                    TargetAngle = DiverterAngle.Angle90,
                    SequenceNumber = 3
                },
                new DiverterConfigurationEntry
                {
                    DiverterId = "Diverter1",
                    TargetAngle = DiverterAngle.Angle0,
                    SequenceNumber = 1
                },
                new DiverterConfigurationEntry
                {
                    DiverterId = "Diverter2",
                    TargetAngle = DiverterAngle.Angle30,
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
        Assert.Equal("Diverter1", result.Segments[0].DiverterId);
        Assert.Equal("Diverter2", result.Segments[1].DiverterId);
        Assert.Equal("Diverter3", result.Segments[2].DiverterId);
    }

    [Fact]
    public void GeneratePath_SetsDefaultTtlForAllSegments()
    {
        // Arrange
        var chuteId = "Chute01";
        var config = new ChuteRouteConfiguration
        {
            ChuteId = chuteId,
            DiverterConfigurations = new List<DiverterConfigurationEntry>
            {
                new DiverterConfigurationEntry
                {
                    DiverterId = "Diverter1",
                    TargetAngle = DiverterAngle.Angle0,
                    SequenceNumber = 1
                }
            }
        };

        _mockRepository.Setup(r => r.GetByChuteId(chuteId)).Returns(config);

        // Act
        var result = _generator.GeneratePath(chuteId);

        // Assert
        Assert.NotNull(result);
        Assert.All(result.Segments, segment => Assert.Equal(5000, segment.TtlMilliseconds));
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
        var chuteId = "Chute01";
        var config = new ChuteRouteConfiguration
        {
            ChuteId = chuteId,
            DiverterConfigurations = new List<DiverterConfigurationEntry>
            {
                new DiverterConfigurationEntry
                {
                    DiverterId = "Diverter1",
                    TargetAngle = DiverterAngle.Angle0,
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
    [InlineData(DiverterAngle.Angle0)]
    [InlineData(DiverterAngle.Angle30)]
    [InlineData(DiverterAngle.Angle90)]
    public void GeneratePath_PreservesTargetAngle(DiverterAngle angle)
    {
        // Arrange
        var chuteId = "Chute01";
        var config = new ChuteRouteConfiguration
        {
            ChuteId = chuteId,
            DiverterConfigurations = new List<DiverterConfigurationEntry>
            {
                new DiverterConfigurationEntry
                {
                    DiverterId = "Diverter1",
                    TargetAngle = angle,
                    SequenceNumber = 1
                }
            }
        };

        _mockRepository.Setup(r => r.GetByChuteId(chuteId)).Returns(config);

        // Act
        var result = _generator.GeneratePath(chuteId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(angle, result.Segments[0].TargetAngle);
    }
}
