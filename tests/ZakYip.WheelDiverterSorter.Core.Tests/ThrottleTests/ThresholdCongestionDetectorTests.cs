using Xunit;
using ZakYip.WheelDiverterSorter.Core.Sorting.Models;
using ZakYip.WheelDiverterSorter.Core.Enums.Sorting;
using ZakYip.WheelDiverterSorter.Core.Sorting.Policies;

namespace ZakYip.WheelDiverterSorter.Core.Tests.ThrottleTests;

public class ThresholdCongestionDetectorTests
{
    private readonly ReleaseThrottleConfiguration _config;
    private readonly ThresholdCongestionDetector _detector;

    public ThresholdCongestionDetectorTests()
    {
        _config = new ReleaseThrottleConfiguration
        {
            WarningThresholdLatencyMs = 5000,
            SevereThresholdLatencyMs = 10000,
            WarningThresholdSuccessRate = 0.9,
            SevereThresholdSuccessRate = 0.7,
            WarningThresholdInFlightParcels = 50,
            SevereThresholdInFlightParcels = 100
        };
        _detector = new ThresholdCongestionDetector(_config);
    }

    [Fact]
    public void DetectCongestionLevel_NullMetrics_ReturnsNormal()
    {
        var level = _detector.DetectCongestionLevel(null!);
        Assert.Equal(CongestionLevel.Normal, level);
    }

    [Fact]
    public void DetectCongestionLevel_AllMetricsGood_ReturnsNormal()
    {
        var metrics = new CongestionMetrics
        {
            InFlightParcels = 10,
            SuccessRate = 0.95,
            AverageLatencyMs = 2000,
            TotalParcels = 100
        };

        var level = _detector.DetectCongestionLevel(metrics);
        Assert.Equal(CongestionLevel.Normal, level);
    }

    [Fact]
    public void DetectCongestionLevel_HighLatency_ReturnsWarning()
    {
        var metrics = new CongestionMetrics
        {
            InFlightParcels = 10,
            SuccessRate = 0.95,
            AverageLatencyMs = 5500,
            TotalParcels = 100
        };

        var level = _detector.DetectCongestionLevel(metrics);
        Assert.Equal(CongestionLevel.Warning, level);
    }

    [Fact]
    public void DetectCongestionLevel_VeryHighLatency_ReturnsSevere()
    {
        var metrics = new CongestionMetrics
        {
            InFlightParcels = 10,
            SuccessRate = 0.95,
            AverageLatencyMs = 11000,
            TotalParcels = 100
        };

        var level = _detector.DetectCongestionLevel(metrics);
        Assert.Equal(CongestionLevel.Severe, level);
    }

    [Fact]
    public void DetectCongestionLevel_LowSuccessRate_ReturnsWarning()
    {
        var metrics = new CongestionMetrics
        {
            InFlightParcels = 10,
            SuccessRate = 0.85,
            AverageLatencyMs = 2000,
            TotalParcels = 100
        };

        var level = _detector.DetectCongestionLevel(metrics);
        Assert.Equal(CongestionLevel.Warning, level);
    }

    [Fact]
    public void DetectCongestionLevel_HighInFlightParcels_ReturnsWarning()
    {
        var metrics = new CongestionMetrics
        {
            InFlightParcels = 55,
            SuccessRate = 0.95,
            AverageLatencyMs = 2000,
            TotalParcels = 100
        };

        var level = _detector.DetectCongestionLevel(metrics);
        Assert.Equal(CongestionLevel.Warning, level);
    }

    [Fact]
    public void DetectCongestionLevel_VeryHighInFlightParcels_ReturnsSevere()
    {
        var metrics = new CongestionMetrics
        {
            InFlightParcels = 105,
            SuccessRate = 0.95,
            AverageLatencyMs = 2000,
            TotalParcels = 100
        };

        var level = _detector.DetectCongestionLevel(metrics);
        Assert.Equal(CongestionLevel.Severe, level);
    }
}
