using Xunit;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;

namespace ZakYip.WheelDiverterSorter.Core.Tests.LineModel;

/// <summary>
/// 测试 LineSegmentConfig 线体段配置功能
/// </summary>
public class LineSegmentConfigTests
{
    [Fact]
    public void LineSegmentConfig_ShouldCreateWithRequiredProperties()
    {
        // Arrange & Act
        var segment = new LineSegmentConfig
        {
            SegmentId = "SEG_001",
            FromNodeId = "ENTRY",
            ToNodeId = "WHEEL-1",
            LengthMm = 1000,
            NominalSpeedMmPerSec = 500.0,
            Description = "从入口到第一个摆轮"
        };

        // Assert
        Assert.NotNull(segment);
        Assert.Equal("SEG_001", segment.SegmentId);
        Assert.Equal("ENTRY", segment.FromNodeId);
        Assert.Equal("WHEEL-1", segment.ToNodeId);
        Assert.Equal(1000, segment.LengthMm);
        Assert.Equal(500.0, segment.NominalSpeedMmPerSec);
        Assert.Equal("从入口到第一个摆轮", segment.Description);
    }

    [Fact]
    public void CalculateTransitTimeMs_WithNominalSpeed_ReturnsCorrectTime()
    {
        // Arrange
        var segment = new LineSegmentConfig
        {
            SegmentId = "SEG_001",
            FromNodeId = "ENTRY",
            ToNodeId = "WHEEL-1",
            LengthMm = 1000,
            NominalSpeedMmPerSec = 500.0
        };

        // Act
        var transitTime = segment.CalculateTransitTimeMs();

        // Assert
        // 1000mm / 500mm/s = 2s = 2000ms
        Assert.Equal(2000.0, transitTime);
    }

    [Fact]
    public void CalculateTransitTimeMs_WithCustomSpeed_UsesCustomSpeed()
    {
        // Arrange
        var segment = new LineSegmentConfig
        {
            SegmentId = "SEG_001",
            FromNodeId = "ENTRY",
            ToNodeId = "WHEEL-1",
            LengthMm = 1000,
            NominalSpeedMmPerSec = 500.0
        };

        // Act - Use custom speed of 1000 mm/s
        var transitTime = segment.CalculateTransitTimeMs(1000.0);

        // Assert
        // 1000mm / 1000mm/s = 1s = 1000ms
        Assert.Equal(1000.0, transitTime);
    }

    [Fact]
    public void CalculateTransitTimeMs_WithZeroSpeed_ThrowsArgumentException()
    {
        // Arrange
        var segment = new LineSegmentConfig
        {
            SegmentId = "SEG_001",
            FromNodeId = "ENTRY",
            ToNodeId = "WHEEL-1",
            LengthMm = 1000,
            NominalSpeedMmPerSec = 500.0
        };

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => 
            segment.CalculateTransitTimeMs(0));
        Assert.Contains("速度必须大于0", exception.Message);
    }

    [Fact]
    public void CalculateTransitTimeMs_WithNegativeSpeed_ThrowsArgumentException()
    {
        // Arrange
        var segment = new LineSegmentConfig
        {
            SegmentId = "SEG_001",
            FromNodeId = "ENTRY",
            ToNodeId = "WHEEL-1",
            LengthMm = 1000,
            NominalSpeedMmPerSec = 500.0
        };

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => 
            segment.CalculateTransitTimeMs(-100));
        Assert.Contains("速度必须大于0", exception.Message);
    }

    [Fact]
    public void CalculateTransitTimeMs_WithZeroNominalSpeedAndNoCustomSpeed_ThrowsArgumentException()
    {
        // Arrange
        var segment = new LineSegmentConfig
        {
            SegmentId = "SEG_001",
            FromNodeId = "ENTRY",
            ToNodeId = "WHEEL-1",
            LengthMm = 1000,
            NominalSpeedMmPerSec = 0 // Invalid but allowed in construction
        };

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => 
            segment.CalculateTransitTimeMs());
        Assert.Contains("速度必须大于0", exception.Message);
    }

    [Fact]
    public void CalculateTransitTimeMs_WithVeryShortSegment_ReturnsSmallTime()
    {
        // Arrange - 10mm segment at 500mm/s
        var segment = new LineSegmentConfig
        {
            SegmentId = "SEG_SHORT",
            FromNodeId = "NODE_A",
            ToNodeId = "NODE_B",
            LengthMm = 10,
            NominalSpeedMmPerSec = 500.0
        };

        // Act
        var transitTime = segment.CalculateTransitTimeMs();

        // Assert
        // 10mm / 500mm/s = 0.02s = 20ms
        Assert.Equal(20.0, transitTime);
    }

    [Fact]
    public void CalculateTransitTimeMs_WithVeryLongSegment_ReturnsLargeTime()
    {
        // Arrange - 10000mm (10m) segment at 500mm/s
        var segment = new LineSegmentConfig
        {
            SegmentId = "SEG_LONG",
            FromNodeId = "NODE_A",
            ToNodeId = "NODE_B",
            LengthMm = 10000,
            NominalSpeedMmPerSec = 500.0
        };

        // Act
        var transitTime = segment.CalculateTransitTimeMs();

        // Assert
        // 10000mm / 500mm/s = 20s = 20000ms
        Assert.Equal(20000.0, transitTime);
    }

    [Fact]
    public void CalculateTransitTimeMs_WithVerySlowSpeed_ReturnsLargeTime()
    {
        // Arrange
        var segment = new LineSegmentConfig
        {
            SegmentId = "SEG_001",
            FromNodeId = "ENTRY",
            ToNodeId = "WHEEL-1",
            LengthMm = 1000,
            NominalSpeedMmPerSec = 50.0 // Very slow: 50mm/s
        };

        // Act
        var transitTime = segment.CalculateTransitTimeMs();

        // Assert
        // 1000mm / 50mm/s = 20s = 20000ms
        Assert.Equal(20000.0, transitTime);
    }

    [Fact]
    public void CalculateTransitTimeMs_WithVeryFastSpeed_ReturnsSmallTime()
    {
        // Arrange
        var segment = new LineSegmentConfig
        {
            SegmentId = "SEG_001",
            FromNodeId = "ENTRY",
            ToNodeId = "WHEEL-1",
            LengthMm = 1000,
            NominalSpeedMmPerSec = 5000.0 // Very fast: 5000mm/s
        };

        // Act
        var transitTime = segment.CalculateTransitTimeMs();

        // Assert
        // 1000mm / 5000mm/s = 0.2s = 200ms
        Assert.Equal(200.0, transitTime);
    }

    [Fact]
    public void LineSegmentConfig_RecordEquality_WorksCorrectly()
    {
        // Arrange
        var segment1 = new LineSegmentConfig
        {
            SegmentId = "SEG_001",
            FromNodeId = "ENTRY",
            ToNodeId = "WHEEL-1",
            LengthMm = 1000,
            NominalSpeedMmPerSec = 500.0
        };

        var segment2 = new LineSegmentConfig
        {
            SegmentId = "SEG_001",
            FromNodeId = "ENTRY",
            ToNodeId = "WHEEL-1",
            LengthMm = 1000,
            NominalSpeedMmPerSec = 500.0
        };

        // Act & Assert
        Assert.Equal(segment1, segment2);
        Assert.True(segment1 == segment2);
    }

    [Fact]
    public void LineSegmentConfig_RecordInequality_WorksCorrectly()
    {
        // Arrange
        var segment1 = new LineSegmentConfig
        {
            SegmentId = "SEG_001",
            FromNodeId = "ENTRY",
            ToNodeId = "WHEEL-1",
            LengthMm = 1000,
            NominalSpeedMmPerSec = 500.0
        };

        var segment2 = new LineSegmentConfig
        {
            SegmentId = "SEG_002",
            FromNodeId = "ENTRY",
            ToNodeId = "WHEEL-2",
            LengthMm = 1500,
            NominalSpeedMmPerSec = 600.0
        };

        // Act & Assert
        Assert.NotEqual(segment1, segment2);
        Assert.False(segment1 == segment2);
    }
}
