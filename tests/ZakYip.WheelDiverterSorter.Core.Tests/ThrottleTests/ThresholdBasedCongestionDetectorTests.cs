using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using Xunit;
using ZakYip.WheelDiverterSorter.Core.Sorting.Runtime;
using ZakYip.WheelDiverterSorter.Core.Sorting.Models;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Sorting.Policies;
using ZakYip.WheelDiverterSorter.Core.Enums.Monitoring;

namespace ZakYip.WheelDiverterSorter.Core.Tests.ThrottleTests;

/// <summary>
/// 测试 ThresholdCongestionDetector：基于阈值的拥堵检测器
/// PR-S1: 使用统一的 ThresholdCongestionDetector 测试 CongestionSnapshot 输入
/// </summary>
public class ThresholdBasedCongestionDetectorTests
{
    [Fact]
    public void Detect_AllNormal_ReturnsNormal()
    {
        // Arrange - 使用 ReleaseThrottleConfiguration 配置
        var config = new ReleaseThrottleConfiguration
        {
            WarningThresholdInFlightParcels = 50,
            SevereThresholdInFlightParcels = 100,
            WarningThresholdLatencyMs = 3000,
            SevereThresholdLatencyMs = 5000,
            WarningThresholdSuccessRate = 0.9,  // 1 - 0.1 = 0.9 成功率
            SevereThresholdSuccessRate = 0.7    // 1 - 0.3 = 0.7 成功率
        };
        var detector = new ThresholdCongestionDetector(config);
        
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
        var config = new ReleaseThrottleConfiguration
        {
            WarningThresholdInFlightParcels = 50,
            SevereThresholdInFlightParcels = 100
        };
        var detector = new ThresholdCongestionDetector(config);
        
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
        var config = new ReleaseThrottleConfiguration
        {
            WarningThresholdInFlightParcels = 50,
            SevereThresholdInFlightParcels = 100
        };
        var detector = new ThresholdCongestionDetector(config);
        
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
        var config = new ReleaseThrottleConfiguration
        {
            WarningThresholdInFlightParcels = 50,
            SevereThresholdInFlightParcels = 100,
            WarningThresholdLatencyMs = 3000,
            SevereThresholdLatencyMs = 5000
        };
        var detector = new ThresholdCongestionDetector(config);
        
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
        var config = new ReleaseThrottleConfiguration
        {
            WarningThresholdLatencyMs = 3000,
            SevereThresholdLatencyMs = 5000
        };
        var detector = new ThresholdCongestionDetector(config);
        
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
        // Arrange - 成功率阈值转换为失败率: 1 - 0.9 = 0.1, 1 - 0.7 = 0.3
        var config = new ReleaseThrottleConfiguration
        {
            WarningThresholdSuccessRate = 0.9,  // 对应 0.1 失败率
            SevereThresholdSuccessRate = 0.7    // 对应 0.3 失败率
        };
        var detector = new ThresholdCongestionDetector(config);
        
        var snapshot = new CongestionSnapshot
        {
            InFlightParcels = 30,
            AverageLatencyMs = 2000,
            FailureRatio = 0.15  // 超过 0.1 (warning) 但低于 0.3 (severe)
        };

        // Act
        var level = detector.Detect(in snapshot);

        // Assert
        Assert.Equal(CongestionLevel.Warning, level);
    }

    [Fact]
    public void Detect_FailureRatioAtSevere_ReturnsSevere()
    {
        // Arrange - 成功率阈值转换为失败率: 1 - 0.9 = 0.1, 1 - 0.7 = 0.3
        var config = new ReleaseThrottleConfiguration
        {
            WarningThresholdSuccessRate = 0.9,  // 对应 0.1 失败率
            SevereThresholdSuccessRate = 0.7    // 对应 0.3 失败率
        };
        var detector = new ThresholdCongestionDetector(config);
        
        var snapshot = new CongestionSnapshot
        {
            InFlightParcels = 30,
            AverageLatencyMs = 2000,
            FailureRatio = 0.4  // 超过 0.3 (severe)
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
        var config = new ReleaseThrottleConfiguration
        {
            WarningThresholdInFlightParcels = 50,
            WarningThresholdLatencyMs = 3000,
            WarningThresholdSuccessRate = 0.9  // 对应 0.1 失败率
        };
        var detector = new ThresholdCongestionDetector(config);
        
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
        var config = new ReleaseThrottleConfiguration
        {
            WarningThresholdInFlightParcels = 50,
            SevereThresholdInFlightParcels = 100,
            SevereThresholdSuccessRate = 0.7  // 对应 0.3 失败率
        };
        var detector = new ThresholdCongestionDetector(config);
        
        var snapshot = new CongestionSnapshot
        {
            InFlightParcels = 30,  // Normal
            AverageLatencyMs = 2000,  // Normal
            FailureRatio = 0.35  // Severe (> 0.3)
        };

        // Act
        var level = detector.Detect(in snapshot);

        // Assert
        Assert.Equal(CongestionLevel.Severe, level);
    }
}
