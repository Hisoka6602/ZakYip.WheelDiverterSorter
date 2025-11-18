using Xunit;
using ZakYip.Sorting.Core.Runtime;
using ZakYip.Sorting.Core.Policies;

namespace ZakYip.WheelDiverterSorter.Core.Tests.ThrottleTests;

/// <summary>
/// 测试 SimpleCapacityEstimator：简单产能估算器
/// </summary>
public class SimpleCapacityEstimatorTests
{
    [Fact]
    public void Estimate_EmptyHistory_ReturnsZeroCapacity()
    {
        // Arrange
        var thresholds = new CapacityEstimationThresholds
        {
            MinSuccessRate = 0.95,
            MaxAcceptableLatencyMs = 3000,
            MaxExceptionRate = 0.05
        };
        var estimator = new SimpleCapacityEstimator(thresholds);
        
        var history = new CapacityHistory
        {
            TestResults = new List<CapacityTestResult>()
        };

        // Act
        var result = estimator.Estimate(in history);

        // Assert
        Assert.Equal(0, result.SafeMinParcelsPerMinute);
        Assert.Equal(0, result.SafeMaxParcelsPerMinute);
        Assert.Equal(0, result.DangerousThresholdParcelsPerMinute);
        Assert.Equal(0, result.Confidence);
        Assert.Equal(0, result.DataPointCount);
    }

    [Fact]
    public void Estimate_AllSafeResults_ReturnsCorrectRange()
    {
        // Arrange
        var thresholds = new CapacityEstimationThresholds
        {
            MinSuccessRate = 0.95,
            MaxAcceptableLatencyMs = 3000,
            MaxExceptionRate = 0.05
        };
        var estimator = new SimpleCapacityEstimator(thresholds);
        
        var testResults = new List<CapacityTestResult>
        {
            new CapacityTestResult 
            { 
                IntervalMs = 1000,  // 60 parcels/min
                SuccessRate = 0.98, 
                AverageLatencyMs = 1500, 
                ExceptionRate = 0.02 
            },
            new CapacityTestResult 
            { 
                IntervalMs = 800,   // 75 parcels/min
                SuccessRate = 0.97, 
                AverageLatencyMs = 2000, 
                ExceptionRate = 0.03 
            },
            new CapacityTestResult 
            { 
                IntervalMs = 600,   // 100 parcels/min
                SuccessRate = 0.96, 
                AverageLatencyMs = 2500, 
                ExceptionRate = 0.04 
            }
        };
        
        var history = new CapacityHistory
        {
            TestResults = testResults
        };

        // Act
        var result = estimator.Estimate(in history);

        // Assert
        Assert.True(result.SafeMinParcelsPerMinute > 0);
        Assert.True(result.SafeMaxParcelsPerMinute >= result.SafeMinParcelsPerMinute);
        Assert.Equal(3, result.DataPointCount);
        // 60 <= SafeMin <= SafeMax <= 100
        Assert.InRange(result.SafeMinParcelsPerMinute, 59, 61);
        Assert.InRange(result.SafeMaxParcelsPerMinute, 99, 101);
    }

    [Fact]
    public void Estimate_MixedResults_FiltersUnsafe()
    {
        // Arrange
        var thresholds = new CapacityEstimationThresholds
        {
            MinSuccessRate = 0.95,
            MaxAcceptableLatencyMs = 3000,
            MaxExceptionRate = 0.05
        };
        var estimator = new SimpleCapacityEstimator(thresholds);
        
        var testResults = new List<CapacityTestResult>
        {
            new CapacityTestResult 
            { 
                IntervalMs = 1000,  // 60 parcels/min - Safe
                SuccessRate = 0.98, 
                AverageLatencyMs = 1500, 
                ExceptionRate = 0.02 
            },
            new CapacityTestResult 
            { 
                IntervalMs = 500,   // 120 parcels/min - Unsafe (high latency)
                SuccessRate = 0.96, 
                AverageLatencyMs = 4000, 
                ExceptionRate = 0.04 
            },
            new CapacityTestResult 
            { 
                IntervalMs = 300,   // 200 parcels/min - Unsafe (low success rate)
                SuccessRate = 0.85, 
                AverageLatencyMs = 2500, 
                ExceptionRate = 0.15 
            }
        };
        
        var history = new CapacityHistory
        {
            TestResults = testResults
        };

        // Act
        var result = estimator.Estimate(in history);

        // Assert
        // Only the first result should be in safe range
        Assert.InRange(result.SafeMinParcelsPerMinute, 59, 61);
        Assert.InRange(result.SafeMaxParcelsPerMinute, 59, 61);
        Assert.Equal(3, result.DataPointCount);
    }

    [Fact]
    public void Estimate_LowSuccessRate_ExcludesFromSafeRange()
    {
        // Arrange
        var thresholds = new CapacityEstimationThresholds
        {
            MinSuccessRate = 0.95,
            MaxAcceptableLatencyMs = 3000,
            MaxExceptionRate = 0.05
        };
        var estimator = new SimpleCapacityEstimator(thresholds);
        
        var testResults = new List<CapacityTestResult>
        {
            new CapacityTestResult 
            { 
                IntervalMs = 1000,
                SuccessRate = 0.90,  // Below threshold
                AverageLatencyMs = 1500, 
                ExceptionRate = 0.02 
            }
        };
        
        var history = new CapacityHistory
        {
            TestResults = testResults
        };

        // Act
        var result = estimator.Estimate(in history);

        // Assert
        Assert.Equal(0, result.SafeMinParcelsPerMinute);
        Assert.Equal(0, result.SafeMaxParcelsPerMinute);
        // Dangerous threshold should be set to this failing point
        Assert.InRange(result.DangerousThresholdParcelsPerMinute, 59, 61);
    }

    [Fact]
    public void Estimate_HighExceptionRate_ExcludesFromSafeRange()
    {
        // Arrange
        var thresholds = new CapacityEstimationThresholds
        {
            MinSuccessRate = 0.95,
            MaxAcceptableLatencyMs = 3000,
            MaxExceptionRate = 0.05
        };
        var estimator = new SimpleCapacityEstimator(thresholds);
        
        var testResults = new List<CapacityTestResult>
        {
            new CapacityTestResult 
            { 
                IntervalMs = 1000,
                SuccessRate = 0.98,
                AverageLatencyMs = 1500, 
                ExceptionRate = 0.08  // Above threshold
            }
        };
        
        var history = new CapacityHistory
        {
            TestResults = testResults
        };

        // Act
        var result = estimator.Estimate(in history);

        // Assert
        Assert.Equal(0, result.SafeMinParcelsPerMinute);
        Assert.Equal(0, result.SafeMaxParcelsPerMinute);
    }

    [Fact]
    public void Estimate_HighLatency_ExcludesFromSafeRange()
    {
        // Arrange
        var thresholds = new CapacityEstimationThresholds
        {
            MinSuccessRate = 0.95,
            MaxAcceptableLatencyMs = 3000,
            MaxExceptionRate = 0.05
        };
        var estimator = new SimpleCapacityEstimator(thresholds);
        
        var testResults = new List<CapacityTestResult>
        {
            new CapacityTestResult 
            { 
                IntervalMs = 1000,
                SuccessRate = 0.98,
                AverageLatencyMs = 3500,  // Above threshold
                ExceptionRate = 0.02 
            }
        };
        
        var history = new CapacityHistory
        {
            TestResults = testResults
        };

        // Act
        var result = estimator.Estimate(in history);

        // Assert
        Assert.Equal(0, result.SafeMinParcelsPerMinute);
        Assert.Equal(0, result.SafeMaxParcelsPerMinute);
    }

    [Fact]
    public void Estimate_TenDataPoints_ReturnsFullConfidence()
    {
        // Arrange
        var thresholds = new CapacityEstimationThresholds();
        var estimator = new SimpleCapacityEstimator(thresholds);
        
        var testResults = new List<CapacityTestResult>();
        for (int i = 0; i < 10; i++)
        {
            testResults.Add(new CapacityTestResult 
            { 
                IntervalMs = 1000 - (i * 50),
                SuccessRate = 0.98,
                AverageLatencyMs = 1500, 
                ExceptionRate = 0.02 
            });
        }
        
        var history = new CapacityHistory
        {
            TestResults = testResults
        };

        // Act
        var result = estimator.Estimate(in history);

        // Assert
        Assert.Equal(1.0, result.Confidence);
        Assert.Equal(10, result.DataPointCount);
    }

    [Fact]
    public void Estimate_FiveDataPoints_ReturnsHalfConfidence()
    {
        // Arrange
        var thresholds = new CapacityEstimationThresholds();
        var estimator = new SimpleCapacityEstimator(thresholds);
        
        var testResults = new List<CapacityTestResult>();
        for (int i = 0; i < 5; i++)
        {
            testResults.Add(new CapacityTestResult 
            { 
                IntervalMs = 1000 - (i * 100),
                SuccessRate = 0.98,
                AverageLatencyMs = 1500, 
                ExceptionRate = 0.02 
            });
        }
        
        var history = new CapacityHistory
        {
            TestResults = testResults
        };

        // Act
        var result = estimator.Estimate(in history);

        // Assert
        Assert.Equal(0.5, result.Confidence);
        Assert.Equal(5, result.DataPointCount);
    }

    [Fact]
    public void Estimate_NoFailingPoints_DangerousThresholdIsAboveSafeMax()
    {
        // Arrange
        var thresholds = new CapacityEstimationThresholds();
        var estimator = new SimpleCapacityEstimator(thresholds);
        
        var testResults = new List<CapacityTestResult>
        {
            new CapacityTestResult 
            { 
                IntervalMs = 1000,
                SuccessRate = 0.98,
                AverageLatencyMs = 1500, 
                ExceptionRate = 0.02 
            }
        };
        
        var history = new CapacityHistory
        {
            TestResults = testResults
        };

        // Act
        var result = estimator.Estimate(in history);

        // Assert
        // When no failing points, dangerous threshold should be 120% of safe max
        Assert.True(result.DangerousThresholdParcelsPerMinute > result.SafeMaxParcelsPerMinute);
        Assert.InRange(result.DangerousThresholdParcelsPerMinute, 
            result.SafeMaxParcelsPerMinute * 1.1, 
            result.SafeMaxParcelsPerMinute * 1.3);
    }
}
