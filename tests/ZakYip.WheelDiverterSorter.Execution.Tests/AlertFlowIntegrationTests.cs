using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using ZakYip.WheelDiverterSorter.Core.Events.Monitoring;
using Microsoft.Extensions.Logging;
using Moq;
using ZakYip.WheelDiverterSorter.Core.LineModel;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Events.Sorting;
using ZakYip.WheelDiverterSorter.Core.Events.Hardware;
using ZakYip.WheelDiverterSorter.Core.Events.Chute;
using ZakYip.WheelDiverterSorter.Observability;
using Xunit;
using ZakYip.WheelDiverterSorter.Core.Enums.Monitoring;
using ZakYip.WheelDiverterSorter.Execution.Diagnostics;

namespace ZakYip.WheelDiverterSorter.Execution.Tests;

/// <summary>
/// Integration tests for the complete alert flow from anomaly detection to alert sink
/// </summary>
public class AlertFlowIntegrationTests
{
    [Fact]
    public async Task CompleteAlertFlow_FromDetectionToSink_ShouldWorkEndToEnd()
    {
        // Arrange - Setup the complete pipeline
        var mockAlertSinkLogger = new Mock<ILogger<LogAlertSink>>();
        var mockClock = new Mock<Core.Utilities.ISystemClock>();
        var metrics = new PrometheusMetrics(mockClock.Object);
        var alertSink = new LogAlertSink(mockAlertSinkLogger.Object, metrics);

        var mockDetectorLogger = new Mock<ILogger<AnomalyDetector>>();
        var detector = new AnomalyDetector(alertSink, mockDetectorLogger.Object, new ZakYip.WheelDiverterSorter.Core.Utilities.LocalSystemClock());

        // Act - Simulate business scenario with high exception chute ratio
        for (int i = 0; i < 100; i++)
        {
            // 20% exception rate (above 15% threshold)
            bool isException = i < 20;
            detector.RecordSortingResult($"CHUTE-{i:D3}", isException);
        }

        // Trigger anomaly detection
        await detector.CheckAnomalyTrendsAsync();
        await Task.Delay(200); // Give async alert writing time to complete

        // Assert - Verify alert was logged
        mockAlertSinkLogger.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => 
                    v.ToString()!.Contains("EXCEPTION_CHUTE_RATIO_HIGH") &&
                    v.ToString()!.Contains("ALERT")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task AlertFlow_WithUpstreamTimeout_ShouldTriggerCriticalAlert()
    {
        // Arrange
        var mockAlertSinkLogger = new Mock<ILogger<LogAlertSink>>();
        var alertSink = new LogAlertSink(mockAlertSinkLogger.Object);

        var mockDetectorLogger = new Mock<ILogger<AnomalyDetector>>();
        var detector = new AnomalyDetector(alertSink, mockDetectorLogger.Object, new ZakYip.WheelDiverterSorter.Core.Utilities.LocalSystemClock());

        // Act - Simulate upstream timeout scenario (15% timeout rate, above 10% threshold)
        for (int i = 0; i < 100; i++)
        {
            detector.RecordSortingResult($"CHUTE-{i:D3}", false);
            
            if (i < 15)
            {
                detector.RecordUpstreamTimeout();
            }
        }

        await detector.CheckAnomalyTrendsAsync();
        await Task.Delay(200); // Give async alert writing time to complete

        // Assert - Verify critical alert was logged
        mockAlertSinkLogger.Verify(
            x => x.Log(
                LogLevel.Critical,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => 
                    v.ToString()!.Contains("UPSTREAM_TIMEOUT_HIGH")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task AlertFlow_WithMultipleAnomalies_ShouldTriggerMultipleAlerts()
    {
        // Arrange
        var mockAlertSinkLogger = new Mock<ILogger<LogAlertSink>>();
        var alertSink = new LogAlertSink(mockAlertSinkLogger.Object);

        var mockDetectorLogger = new Mock<ILogger<AnomalyDetector>>();
        var detector = new AnomalyDetector(alertSink, mockDetectorLogger.Object, new ZakYip.WheelDiverterSorter.Core.Utilities.LocalSystemClock());

        // Act - Simulate multiple anomaly scenarios
        for (int i = 0; i < 100; i++)
        {
            // High exception rate
            detector.RecordSortingResult($"CHUTE-{i:D3}", i < 20);
            
            // High upstream timeout rate
            if (i < 15)
            {
                detector.RecordUpstreamTimeout();
            }
        }

        await detector.CheckAnomalyTrendsAsync();
        await Task.Delay(200); // Give async alert writing time to complete

        // Assert - Verify both alerts were logged
        mockAlertSinkLogger.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => 
                    v.ToString()!.Contains("EXCEPTION_CHUTE_RATIO_HIGH")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce,
            "Exception chute ratio alert should be triggered");

        mockAlertSinkLogger.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => 
                    v.ToString()!.Contains("UPSTREAM_TIMEOUT_HIGH")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce,
            "Upstream timeout alert should be triggered");
    }

    [Fact]
    public async Task AlertFlow_WithNormalMetrics_ShouldNotTriggerAlerts()
    {
        // Arrange
        var mockAlertSinkLogger = new Mock<ILogger<LogAlertSink>>();
        var alertSink = new LogAlertSink(mockAlertSinkLogger.Object);

        var mockDetectorLogger = new Mock<ILogger<AnomalyDetector>>();
        var detector = new AnomalyDetector(alertSink, mockDetectorLogger.Object, new ZakYip.WheelDiverterSorter.Core.Utilities.LocalSystemClock());

        // Act - Simulate normal operation (5% exception rate, 3% timeout rate)
        for (int i = 0; i < 100; i++)
        {
            detector.RecordSortingResult($"CHUTE-{i:D3}", i < 5);
            
            if (i < 3)
            {
                detector.RecordUpstreamTimeout();
            }
        }

        await detector.CheckAnomalyTrendsAsync();
        await Task.Delay(200); // Give async alert writing time to complete

        // Assert - No alerts should be triggered
        mockAlertSinkLogger.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("[ALERT]")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Fact]
    public async Task AlertFlow_CustomAlertSink_ShouldReceiveAlerts()
    {
        // Arrange - Use a custom alert sink to verify alert data
        var receivedAlerts = new List<AlertRaisedEventArgs>();
        var customSink = new TestAlertSink(receivedAlerts);

        var mockDetectorLogger = new Mock<ILogger<AnomalyDetector>>();
        var detector = new AnomalyDetector(customSink, mockDetectorLogger.Object, new ZakYip.WheelDiverterSorter.Core.Utilities.LocalSystemClock());

        // Act - Trigger an anomaly
        for (int i = 0; i < 100; i++)
        {
            detector.RecordSortingResult($"CHUTE-{i:D3}", i < 25); // 25% exception rate
        }

        await detector.CheckAnomalyTrendsAsync();
        await Task.Delay(200); // Give async alert writing time to complete

        // Assert - Custom sink should have received the alert
        Assert.NotEmpty(receivedAlerts);
        var alert = receivedAlerts[0];
        Assert.Equal("EXCEPTION_CHUTE_RATIO_HIGH", alert.AlertCode);
        Assert.Equal(AlertSeverity.Warning, alert.Severity);
        Assert.Contains("异常格口比例过高", alert.Message);
        Assert.NotNull(alert.Details);
        Assert.True(alert.Details.ContainsKey("exceptionRatio"));
    }

    /// <summary>
    /// Test implementation of IAlertSink for testing purposes
    /// </summary>
    private class TestAlertSink : IAlertSink
    {
        private readonly List<AlertRaisedEventArgs> _receivedAlerts;

        public TestAlertSink(List<AlertRaisedEventArgs> receivedAlerts)
        {
            _receivedAlerts = receivedAlerts;
        }

        public Task WriteAlertAsync(AlertRaisedEventArgs alertEvent, CancellationToken cancellationToken = default)
        {
            _receivedAlerts.Add(alertEvent);
            return Task.CompletedTask;
        }
    }
}
