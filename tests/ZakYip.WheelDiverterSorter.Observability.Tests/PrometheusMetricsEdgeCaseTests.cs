using Moq;
using Xunit;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Observability;

namespace ZakYip.WheelDiverterSorter.Observability.Tests;

/// <summary>
/// Tests for Prometheus metrics edge cases
/// 测试 Prometheus 指标边缘情况
/// </summary>
public class PrometheusMetricsEdgeCaseTests
{
    [Fact]
    public void RecordSortingSuccess_WithZeroDuration_ShouldNotThrow()
    {
        // Arrange
        var metrics = new PrometheusMetrics(Mock.Of<ISystemClock>());

        // Act & Assert - Should handle zero duration gracefully
        var exception = Record.Exception(() => metrics.RecordSortingSuccess(0.0));
        Assert.Null(exception);
    }

    [Fact]
    public void RecordSortingSuccess_WithNegativeDuration_ShouldNotThrow()
    {
        // Arrange
        var metrics = new PrometheusMetrics(Mock.Of<ISystemClock>());

        // Act & Assert - Should handle negative duration (even though it's invalid)
        var exception = Record.Exception(() => metrics.RecordSortingSuccess(-0.1));
        Assert.Null(exception);
    }

    [Fact]
    public void RecordSortingSuccess_WithExtremelyLargeDuration_ShouldNotThrow()
    {
        // Arrange
        var metrics = new PrometheusMetrics(Mock.Of<ISystemClock>());

        // Act & Assert - Should handle extremely large values
        var exception = Record.Exception(() => metrics.RecordSortingSuccess(double.MaxValue));
        Assert.Null(exception);
    }

    [Fact]
    public void RecordSortingFailure_WithVerySmallDuration_ShouldNotThrow()
    {
        // Arrange
        var metrics = new PrometheusMetrics(Mock.Of<ISystemClock>());

        // Act & Assert - Should handle very small durations (microseconds)
        var exception = Record.Exception(() => metrics.RecordSortingFailure(0.000001));
        Assert.Null(exception);
    }

    [Fact]
    public void RecordPathGeneration_WithNegativeDuration_ShouldNotThrow()
    {
        // Arrange
        var metrics = new PrometheusMetrics(Mock.Of<ISystemClock>());

        // Act & Assert
        var exception = Record.Exception(() => metrics.RecordPathGeneration(-1.0));
        Assert.Null(exception);
    }

    [Fact]
    public void RecordPathExecution_WithZeroDuration_ShouldNotThrow()
    {
        // Arrange
        var metrics = new PrometheusMetrics(Mock.Of<ISystemClock>());

        // Act & Assert
        var exception = Record.Exception(() => metrics.RecordPathExecution(0.0));
        Assert.Null(exception);
    }

    [Fact]
    public void SetQueueLength_WithNegativeValue_ShouldNotThrow()
    {
        // Arrange
        var metrics = new PrometheusMetrics(Mock.Of<ISystemClock>());

        // Act & Assert - Should handle negative queue length (invalid but should not crash)
        var exception = Record.Exception(() => metrics.SetQueueLength(-1));
        Assert.Null(exception);
    }

    [Fact]
    public void SetQueueLength_WithMaxIntValue_ShouldNotThrow()
    {
        // Arrange
        var metrics = new PrometheusMetrics(Mock.Of<ISystemClock>());

        // Act & Assert - Should handle extremely large queue lengths
        var exception = Record.Exception(() => metrics.SetQueueLength(int.MaxValue));
        Assert.Null(exception);
    }

    [Fact]
    public void SetQueueLength_WithZero_ShouldNotThrow()
    {
        // Arrange
        var metrics = new PrometheusMetrics(Mock.Of<ISystemClock>());

        // Act & Assert
        var exception = Record.Exception(() => metrics.SetQueueLength(0));
        Assert.Null(exception);
    }

    [Fact]
    public void RecordQueueWaitTime_WithZeroDuration_ShouldNotThrow()
    {
        // Arrange
        var metrics = new PrometheusMetrics(Mock.Of<ISystemClock>());

        // Act & Assert
        var exception = Record.Exception(() => metrics.RecordQueueWaitTime(0.0));
        Assert.Null(exception);
    }

    [Fact]
    public void RecordQueueWaitTime_WithExtremelyLongWait_ShouldNotThrow()
    {
        // Arrange
        var metrics = new PrometheusMetrics(Mock.Of<ISystemClock>());

        // Act & Assert - Should handle very long wait times (hours)
        var exception = Record.Exception(() => metrics.RecordQueueWaitTime(3600.0 * 24)); // 24 hours
        Assert.Null(exception);
    }

    [Fact]
    public void SetDiverterActive_WithEmptyId_ShouldNotThrow()
    {
        // Arrange
        var metrics = new PrometheusMetrics(Mock.Of<ISystemClock>());

        // Act & Assert - Should handle empty diverter ID
        var exception = Record.Exception(() => metrics.SetDiverterActive("", true));
        Assert.Null(exception);
    }

    [Fact]
    public void SetDiverterActive_WithVeryLongId_ShouldNotThrow()
    {
        // Arrange
        var metrics = new PrometheusMetrics(Mock.Of<ISystemClock>());
        var longId = new string('D', 1000);

        // Act & Assert - Should handle very long IDs
        var exception = Record.Exception(() => metrics.SetDiverterActive(longId, true));
        Assert.Null(exception);
    }

    [Fact]
    public void SetDiverterActive_WithSpecialCharacters_ShouldNotThrow()
    {
        // Arrange
        var metrics = new PrometheusMetrics(Mock.Of<ISystemClock>());

        // Act & Assert - Should handle special characters
        var exception = Record.Exception(() => metrics.SetDiverterActive("D-1_test@#", true));
        Assert.Null(exception);
    }

    [Fact]
    public void RecordDiverterOperation_WithEmptyId_ShouldNotThrow()
    {
        // Arrange
        var metrics = new PrometheusMetrics(Mock.Of<ISystemClock>());

        // Act & Assert
        var exception = Record.Exception(() => metrics.RecordDiverterOperation("", "left"));
        Assert.Null(exception);
    }

    [Fact]
    public void RecordDiverterOperation_WithEmptyDirection_ShouldNotThrow()
    {
        // Arrange
        var metrics = new PrometheusMetrics(Mock.Of<ISystemClock>());

        // Act & Assert
        var exception = Record.Exception(() => metrics.RecordDiverterOperation("D1", ""));
        Assert.Null(exception);
    }

    [Fact]
    public void RecordDiverterOperation_WithInvalidDirection_ShouldNotThrow()
    {
        // Arrange
        var metrics = new PrometheusMetrics(Mock.Of<ISystemClock>());

        // Act & Assert - Should handle invalid direction values
        var exception = Record.Exception(() => metrics.RecordDiverterOperation("D1", "invalid"));
        Assert.Null(exception);
    }

    [Fact]
    public void SetDiverterUtilization_WithNegativeValue_ShouldNotThrow()
    {
        // Arrange
        var metrics = new PrometheusMetrics(Mock.Of<ISystemClock>());

        // Act & Assert - Should handle negative utilization (invalid)
        var exception = Record.Exception(() => metrics.SetDiverterUtilization("D1", -0.5));
        Assert.Null(exception);
    }

    [Fact]
    public void SetDiverterUtilization_WithValueGreaterThanOne_ShouldNotThrow()
    {
        // Arrange
        var metrics = new PrometheusMetrics(Mock.Of<ISystemClock>());

        // Act & Assert - Should handle utilization > 1 (invalid but possible in edge cases)
        var exception = Record.Exception(() => metrics.SetDiverterUtilization("D1", 1.5));
        Assert.Null(exception);
    }

    [Fact]
    public void SetDiverterUtilization_WithExactlyZero_ShouldNotThrow()
    {
        // Arrange
        var metrics = new PrometheusMetrics(Mock.Of<ISystemClock>());

        // Act & Assert
        var exception = Record.Exception(() => metrics.SetDiverterUtilization("D1", 0.0));
        Assert.Null(exception);
    }

    [Fact]
    public void SetDiverterUtilization_WithExactlyOne_ShouldNotThrow()
    {
        // Arrange
        var metrics = new PrometheusMetrics(Mock.Of<ISystemClock>());

        // Act & Assert
        var exception = Record.Exception(() => metrics.SetDiverterUtilization("D1", 1.0));
        Assert.Null(exception);
    }

    [Fact]
    public void SetRuleEngineConnectionStatus_WithEmptyConnectionType_ShouldNotThrow()
    {
        // Arrange
        var metrics = new PrometheusMetrics(Mock.Of<ISystemClock>());

        // Act & Assert
        var exception = Record.Exception(() => metrics.SetRuleEngineConnectionStatus("", true));
        Assert.Null(exception);
    }

    [Fact]
    public void RecordRuleEngineSend_WithEmptyLabels_ShouldNotThrow()
    {
        // Arrange
        var metrics = new PrometheusMetrics(Mock.Of<ISystemClock>());

        // Act & Assert
        var exception = Record.Exception(() => metrics.RecordRuleEngineSend("", ""));
        Assert.Null(exception);
    }

    [Fact]
    public void RecordRuleEngineReceive_WithSpecialCharacters_ShouldNotThrow()
    {
        // Arrange
        var metrics = new PrometheusMetrics(Mock.Of<ISystemClock>());

        // Act & Assert
        var exception = Record.Exception(() => metrics.RecordRuleEngineReceive("tcp@#$%", "type!@#"));
        Assert.Null(exception);
    }

    [Fact]
    public void SetSensorHealthStatus_WithEmptySensorId_ShouldNotThrow()
    {
        // Arrange
        var metrics = new PrometheusMetrics(Mock.Of<ISystemClock>());

        // Act & Assert
        var exception = Record.Exception(() => metrics.SetSensorHealthStatus("", "mock", true));
        Assert.Null(exception);
    }

    [Fact]
    public void SetSensorHealthStatus_WithEmptySensorType_ShouldNotThrow()
    {
        // Arrange
        var metrics = new PrometheusMetrics(Mock.Of<ISystemClock>());

        // Act & Assert
        var exception = Record.Exception(() => metrics.SetSensorHealthStatus("S1", "", true));
        Assert.Null(exception);
    }

    [Fact]
    public void RecordSensorError_WithEmptyLabels_ShouldNotThrow()
    {
        // Arrange
        var metrics = new PrometheusMetrics(Mock.Of<ISystemClock>());

        // Act & Assert
        var exception = Record.Exception(() => metrics.RecordSensorError("", ""));
        Assert.Null(exception);
    }

    [Fact]
    public void RecordSensorDetection_WithVeryLongLabels_ShouldNotThrow()
    {
        // Arrange
        var metrics = new PrometheusMetrics(Mock.Of<ISystemClock>());
        var longLabel = new string('S', 1000);

        // Act & Assert
        var exception = Record.Exception(() => metrics.RecordSensorDetection(longLabel, longLabel));
        Assert.Null(exception);
    }

    [Fact]
    public void ActiveRequests_IncrementAndDecrementMultipleTimes_ShouldNotThrow()
    {
        // Arrange
        var metrics = new PrometheusMetrics(Mock.Of<ISystemClock>());

        // Act & Assert - Should handle multiple increments and decrements
        var exception = Record.Exception(() =>
        {
            for (int i = 0; i < 1000; i++)
            {
                metrics.IncrementActiveRequests();
            }
            for (int i = 0; i < 1000; i++)
            {
                metrics.DecrementActiveRequests();
            }
        });
        Assert.Null(exception);
    }

    [Fact]
    public void ActiveRequests_DecrementBelowZero_ShouldNotThrow()
    {
        // Arrange
        var metrics = new PrometheusMetrics(Mock.Of<ISystemClock>());

        // Act & Assert - Should handle decrementing below zero (edge case)
        var exception = Record.Exception(() =>
        {
            metrics.DecrementActiveRequests();
            metrics.DecrementActiveRequests();
            metrics.DecrementActiveRequests();
        });
        Assert.Null(exception);
    }

    [Fact]
    public void ConcurrentMetricUpdates_ShouldNotThrow()
    {
        // Arrange
        var metrics = new PrometheusMetrics(Mock.Of<ISystemClock>());
        const int threadCount = 10;
        const int operationsPerThread = 100;
        var tasks = new Task[threadCount];

        // Act - Multiple threads updating metrics concurrently
        for (int i = 0; i < threadCount; i++)
        {
            int threadIndex = i;
            tasks[i] = Task.Run(() =>
            {
                for (int j = 0; j < operationsPerThread; j++)
                {
                    metrics.RecordSortingSuccess(0.1);
                    metrics.RecordSortingFailure(0.1);
                    metrics.SetQueueLength(j);
                    metrics.RecordDiverterOperation($"D{threadIndex}", "left");
                    metrics.IncrementActiveRequests();
                    metrics.DecrementActiveRequests();
                }
            });
        }

        // Assert - Should complete without exceptions
        var aggregateException = Record.Exception(() => Task.WaitAll(tasks));
        Assert.Null(aggregateException);
    }

    [Fact]
    public void SameDiverterConcurrentOperations_ShouldNotThrow()
    {
        // Arrange
        var metrics = new PrometheusMetrics(Mock.Of<ISystemClock>());
        const int threadCount = 50;
        var tasks = new Task[threadCount];

        // Act - Multiple threads operating on the same diverter
        for (int i = 0; i < threadCount; i++)
        {
            tasks[i] = Task.Run(() =>
            {
                for (int j = 0; j < 10; j++)
                {
                    metrics.SetDiverterActive("D1", j % 2 == 0);
                    metrics.RecordDiverterOperation("D1", "left");
                    metrics.SetDiverterUtilization("D1", 0.5);
                }
            });
        }

        // Assert
        var aggregateException = Record.Exception(() => Task.WaitAll(tasks));
        Assert.Null(aggregateException);
    }

    [Fact]
    public void MultipleInstances_ConcurrentOperations_ShouldNotThrow()
    {
        // Arrange
        var metrics1 = new PrometheusMetrics(Mock.Of<ISystemClock>());
        var metrics2 = new PrometheusMetrics(Mock.Of<ISystemClock>());
        var metrics3 = new PrometheusMetrics(Mock.Of<ISystemClock>());

        // Act - Multiple instances used concurrently
        var task1 = Task.Run(() =>
        {
            for (int i = 0; i < 100; i++)
            {
                metrics1.RecordSortingSuccess(0.1);
            }
        });

        var task2 = Task.Run(() =>
        {
            for (int i = 0; i < 100; i++)
            {
                metrics2.RecordSortingFailure(0.1);
            }
        });

        var task3 = Task.Run(() =>
        {
            for (int i = 0; i < 100; i++)
            {
                metrics3.SetQueueLength(i);
            }
        });

        // Assert
        var aggregateException = Record.Exception(() => Task.WaitAll(task1, task2, task3));
        Assert.Null(aggregateException);
    }

    [Fact]
    public void RapidMetricUpdates_ShouldMaintainStability()
    {
        // Arrange
        var metrics = new PrometheusMetrics(Mock.Of<ISystemClock>());

        // Act & Assert - Very rapid updates should not cause issues
        var exception = Record.Exception(() =>
        {
            for (int i = 0; i < 10000; i++)
            {
                metrics.RecordSortingSuccess(0.001);
                if (i % 100 == 0)
                {
                    metrics.SetQueueLength(i / 100);
                }
            }
        });
        Assert.Null(exception);
    }

    [Fact]
    public void MixedLabelSizes_ShouldNotThrow()
    {
        // Arrange
        var metrics = new PrometheusMetrics(Mock.Of<ISystemClock>());

        // Act & Assert - Should handle labels of various sizes
        var exception = Record.Exception(() =>
        {
            metrics.RecordDiverterOperation("", "left"); // Empty
            metrics.RecordDiverterOperation("D", "right"); // Short
            metrics.RecordDiverterOperation("D123", "straight"); // Normal
            metrics.RecordDiverterOperation(new string('D', 100), "left"); // Long
            metrics.RecordDiverterOperation("D-_./", "right"); // Special chars
        });
        Assert.Null(exception);
    }

    [Fact]
    public void UnicodeLabels_ShouldNotThrow()
    {
        // Arrange
        var metrics = new PrometheusMetrics(Mock.Of<ISystemClock>());

        // Act & Assert - Should handle Unicode characters in labels
        var exception = Record.Exception(() =>
        {
            metrics.RecordDiverterOperation("摆轮1", "left");
            metrics.SetSensorHealthStatus("传感器1", "类型", true);
            metrics.RecordRuleEngineSend("连接", "消息");
        });
        Assert.Null(exception);
    }

    [Fact]
    public void ExtremeHistogramValues_ShouldNotThrow()
    {
        // Arrange
        var metrics = new PrometheusMetrics(Mock.Of<ISystemClock>());

        // Act & Assert - Test histogram bucket edge cases
        var exception = Record.Exception(() =>
        {
            metrics.RecordPathGeneration(0.0); // Minimum
            metrics.RecordPathGeneration(0.001); // Lower bucket
            metrics.RecordPathGeneration(0.01); // Mid bucket
            metrics.RecordPathGeneration(1.0); // High bucket
            metrics.RecordPathGeneration(10.0); // Very high
            metrics.RecordPathGeneration(100.0); // Extremely high
        });
        Assert.Null(exception);
    }
}
