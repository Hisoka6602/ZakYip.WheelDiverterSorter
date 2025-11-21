using Xunit;
using ZakYip.WheelDiverterSorter.Core.Sorting.Runtime;
using ZakYip.WheelDiverterSorter.Core.Enums.Sorting;
using ZakYip.WheelDiverterSorter.Core.Sorting.Policies;

namespace ZakYip.WheelDiverterSorter.Core.Tests.ThrottleTests;

/// <summary>
/// 测试 ThresholdBasedCongestionDetector：基于阈值的拥堵检测器
/// </summary>
public class ThresholdBasedCongestionDetectorTests
{
    [Fact]
    public void Detect_AllNormal_ReturnsNormal()
    {
        // Arrange
        var thresholds = new CongestionThresholds
        {
            WarningInFlightParcels = 50,
            SevereInFlightParcels = 100,
            WarningAverageLatencyMs = 3000,
            SevereAverageLatencyMs = 5000,
            WarningFailureRatio = 0.1,
            SevereFailureRatio = 0.3
        };
        var detector = new ThresholdBasedCongestionDetector(thresholds);
        
        var snapshot = new CongestionSnapshot
        {
            InFlightParcels = 30,
            AverageLatencyMs = 2000,
            FailureRatio = 0.05
        };

        // Act
        var level = detector.Detect(in snapshot);

        // Assert
        Assert.Equal(CongestionLevel.Normal, level);
    }

    [Fact]
    public void Detect_InFlightParcelsAtWarning_ReturnsWarning()
    {
        // Arrange
        var thresholds = new CongestionThresholds
        {
            WarningInFlightParcels = 50,
            SevereInFlightParcels = 100
        };
        var detector = new ThresholdBasedCongestionDetector(thresholds);
        
        var snapshot = new CongestionSnapshot
        {
            InFlightParcels = 50,
            AverageLatencyMs = 2000,
            FailureRatio = 0.05
        };

        // Act
        var level = detector.Detect(in snapshot);

        // Assert
        Assert.Equal(CongestionLevel.Warning, level);
    }

    [Fact]
    public void Detect_InFlightParcelsAtSevere_ReturnsSevere()
    {
        // Arrange
        var thresholds = new CongestionThresholds
        {
            WarningInFlightParcels = 50,
            SevereInFlightParcels = 100
        };
        var detector = new ThresholdBasedCongestionDetector(thresholds);
        
        var snapshot = new CongestionSnapshot
        {
            InFlightParcels = 100,
            AverageLatencyMs = 2000,
            FailureRatio = 0.05
        };

        // Act
        var level = detector.Detect(in snapshot);

        // Assert
        Assert.Equal(CongestionLevel.Severe, level);
    }

    [Fact]
    public void Detect_AverageLatencyAtWarning_ReturnsWarning()
    {
        // Arrange
        var thresholds = new CongestionThresholds
        {
            WarningInFlightParcels = 50,
            SevereInFlightParcels = 100,
            WarningAverageLatencyMs = 3000,
            SevereAverageLatencyMs = 5000
        };
        var detector = new ThresholdBasedCongestionDetector(thresholds);
        
        var snapshot = new CongestionSnapshot
        {
            InFlightParcels = 30,
            AverageLatencyMs = 3500,
            FailureRatio = 0.05
        };

        // Act
        var level = detector.Detect(in snapshot);

        // Assert
        Assert.Equal(CongestionLevel.Warning, level);
    }

    [Fact]
    public void Detect_AverageLatencyAtSevere_ReturnsSevere()
    {
        // Arrange
        var thresholds = new CongestionThresholds
        {
            WarningAverageLatencyMs = 3000,
            SevereAverageLatencyMs = 5000
        };
        var detector = new ThresholdBasedCongestionDetector(thresholds);
        
        var snapshot = new CongestionSnapshot
        {
            InFlightParcels = 30,
            AverageLatencyMs = 6000,
            FailureRatio = 0.05
        };

        // Act
        var level = detector.Detect(in snapshot);

        // Assert
        Assert.Equal(CongestionLevel.Severe, level);
    }

    [Fact]
    public void Detect_FailureRatioAtWarning_ReturnsWarning()
    {
        // Arrange
        var thresholds = new CongestionThresholds
        {
            WarningFailureRatio = 0.1,
            SevereFailureRatio = 0.3
        };
        var detector = new ThresholdBasedCongestionDetector(thresholds);
        
        var snapshot = new CongestionSnapshot
        {
            InFlightParcels = 30,
            AverageLatencyMs = 2000,
            FailureRatio = 0.15
        };

        // Act
        var level = detector.Detect(in snapshot);

        // Assert
        Assert.Equal(CongestionLevel.Warning, level);
    }

    [Fact]
    public void Detect_FailureRatioAtSevere_ReturnsSevere()
    {
        // Arrange
        var thresholds = new CongestionThresholds
        {
            WarningFailureRatio = 0.1,
            SevereFailureRatio = 0.3
        };
        var detector = new ThresholdBasedCongestionDetector(thresholds);
        
        var snapshot = new CongestionSnapshot
        {
            InFlightParcels = 30,
            AverageLatencyMs = 2000,
            FailureRatio = 0.4
        };

        // Act
        var level = detector.Detect(in snapshot);

        // Assert
        Assert.Equal(CongestionLevel.Severe, level);
    }

    [Fact]
    public void Detect_MultipleWarningIndicators_ReturnsWarning()
    {
        // Arrange
        var thresholds = new CongestionThresholds
        {
            WarningInFlightParcels = 50,
            WarningAverageLatencyMs = 3000,
            WarningFailureRatio = 0.1
        };
        var detector = new ThresholdBasedCongestionDetector(thresholds);
        
        var snapshot = new CongestionSnapshot
        {
            InFlightParcels = 55,
            AverageLatencyMs = 3200,
            FailureRatio = 0.12
        };

        // Act
        var level = detector.Detect(in snapshot);

        // Assert
        Assert.Equal(CongestionLevel.Warning, level);
    }

    [Fact]
    public void Detect_OneSevereIndicator_ReturnsSevere()
    {
        // Arrange
        var thresholds = new CongestionThresholds
        {
            WarningInFlightParcels = 50,
            SevereInFlightParcels = 100,
            SevereFailureRatio = 0.3
        };
        var detector = new ThresholdBasedCongestionDetector(thresholds);
        
        var snapshot = new CongestionSnapshot
        {
            InFlightParcels = 30,  // Normal
            AverageLatencyMs = 2000,  // Normal
            FailureRatio = 0.35  // Severe
        };

        // Act
        var level = detector.Detect(in snapshot);

        // Assert
        Assert.Equal(CongestionLevel.Severe, level);
    }
}
