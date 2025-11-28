using Xunit;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.LiteDb;

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
            SegmentId = 1,
            StartIoId = 1,
            EndIoId = 2,
            LengthMm = 1000,
            SpeedMmPerSec = 500.0,
            Description = "从入口到第一个摆轮"
        };

        // Assert
        Assert.NotNull(segment);
        Assert.Equal(1, segment.SegmentId);
        Assert.Equal(1, segment.StartIoId);
        Assert.Equal(2, segment.EndIoId);
        Assert.Equal(1000, segment.LengthMm);
        Assert.Equal(500.0, segment.SpeedMmPerSec);
        Assert.Equal("从入口到第一个摆轮", segment.Description);
    }

    [Fact]
    public void CalculateTransitTimeMs_WithNominalSpeed_ReturnsCorrectTime()
    {
        // Arrange
        var segment = new LineSegmentConfig
        {
            SegmentId = 1,
            StartIoId = 1,
            EndIoId = 2,
            LengthMm = 1000,
            SpeedMmPerSec = 500.0
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
            SegmentId = 1,
            StartIoId = 1,
            EndIoId = 2,
            LengthMm = 1000,
            SpeedMmPerSec = 500.0
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
            SegmentId = 1,
            StartIoId = 1,
            EndIoId = 2,
            LengthMm = 1000,
            SpeedMmPerSec = 500.0
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
            SegmentId = 1,
            StartIoId = 1,
            EndIoId = 2,
            LengthMm = 1000,
            SpeedMmPerSec = 500.0
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
            SegmentId = 1,
            StartIoId = 1,
            EndIoId = 2,
            LengthMm = 1000,
            SpeedMmPerSec = 0 // Invalid but allowed in construction
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
            SegmentId = 100,
            StartIoId = 1,
            EndIoId = 2,
            LengthMm = 10,
            SpeedMmPerSec = 500.0
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
            SegmentId = 200,
            StartIoId = 1,
            EndIoId = 2,
            LengthMm = 10000,
            SpeedMmPerSec = 500.0
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
            SegmentId = 1,
            StartIoId = 1,
            EndIoId = 2,
            LengthMm = 1000,
            SpeedMmPerSec = 50.0 // Very slow: 50mm/s
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
            SegmentId = 1,
            StartIoId = 1,
            EndIoId = 2,
            LengthMm = 1000,
            SpeedMmPerSec = 5000.0 // Very fast: 5000mm/s
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
            SegmentId = 1,
            StartIoId = 1,
            EndIoId = 2,
            LengthMm = 1000,
            SpeedMmPerSec = 500.0
        };

        var segment2 = new LineSegmentConfig
        {
            SegmentId = 1,
            StartIoId = 1,
            EndIoId = 2,
            LengthMm = 1000,
            SpeedMmPerSec = 500.0
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
            SegmentId = 1,
            StartIoId = 1,
            EndIoId = 2,
            LengthMm = 1000,
            SpeedMmPerSec = 500.0
        };

        var segment2 = new LineSegmentConfig
        {
            SegmentId = 2,
            StartIoId = 1,
            EndIoId = 3,
            LengthMm = 1500,
            SpeedMmPerSec = 600.0
        };

        // Act & Assert
        Assert.NotEqual(segment1, segment2);
        Assert.False(segment1 == segment2);
    }

    [Fact]
    public void IsEndSegment_WithZeroEndIoId_ReturnsTrue()
    {
        // Arrange
        var segment = new LineSegmentConfig
        {
            SegmentId = 999,
            StartIoId = 3,
            EndIoId = 0,
            LengthMm = 500,
            SpeedMmPerSec = 500.0
        };

        // Act & Assert
        Assert.True(segment.IsEndSegment);
    }

    [Fact]
    public void IsEndSegment_WithNonZeroEndIoId_ReturnsFalse()
    {
        // Arrange
        var segment = new LineSegmentConfig
        {
            SegmentId = 50,
            StartIoId = 1,
            EndIoId = 2,
            LengthMm = 500,
            SpeedMmPerSec = 500.0
        };

        // Act & Assert
        Assert.False(segment.IsEndSegment);
    }
}
