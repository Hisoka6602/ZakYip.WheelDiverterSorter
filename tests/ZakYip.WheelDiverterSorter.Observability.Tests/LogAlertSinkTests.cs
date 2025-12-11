using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using ZakYip.WheelDiverterSorter.Core.Events.Monitoring;
using Microsoft.Extensions.Logging;
using Moq;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Events.Sorting;
using ZakYip.WheelDiverterSorter.Core.Events.Hardware;
using ZakYip.WheelDiverterSorter.Core.Events.Chute;
using Xunit;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Core.Enums.Monitoring;

namespace ZakYip.WheelDiverterSorter.Observability.Tests;

public class LogAlertSinkTests
{
    private readonly Mock<ILogger<LogAlertSink>> _mockLogger;

    public LogAlertSinkTests()
    {
        _mockLogger = new Mock<ILogger<LogAlertSink>>();
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new LogAlertSink(null!));
    }

    [Fact]
    public async Task WriteAlertAsync_WithValidInfoAlert_ShouldLogInformation()
    {
        // Arrange
        var sink = new LogAlertSink(_mockLogger.Object);
        var alertEvent = new AlertRaisedEventArgs
        {
            AlertCode = "TEST_INFO",
            Severity = AlertSeverity.Info,
            Message = "Test information alert",
            RaisedAt = DateTimeOffset.Now
        };

        // Act
        await sink.WriteAlertAsync(alertEvent);

        // Assert - Verify Info level was logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("ALERT-INFO")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task WriteAlertAsync_WithValidWarningAlert_ShouldLogWarning()
    {
        // Arrange
        var sink = new LogAlertSink(_mockLogger.Object);
        var alertEvent = new AlertRaisedEventArgs
        {
            AlertCode = "TEST_WARNING",
            Severity = AlertSeverity.Warning,
            Message = "Test warning alert",
            RaisedAt = DateTimeOffset.Now
        };

        // Act
        await sink.WriteAlertAsync(alertEvent);

        // Assert - Verify Warning level was logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("ALERT-WARNING")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task WriteAlertAsync_WithValidCriticalAlert_ShouldLogCritical()
    {
        // Arrange
        var sink = new LogAlertSink(_mockLogger.Object);
        var alertEvent = new AlertRaisedEventArgs
        {
            AlertCode = "TEST_CRITICAL",
            Severity = AlertSeverity.Critical,
            Message = "Test critical alert",
            RaisedAt = DateTimeOffset.Now
        };

        // Act
        await sink.WriteAlertAsync(alertEvent);

        // Assert - Verify Critical level was logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Critical,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("ALERT-CRITICAL")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task WriteAlertAsync_WithOptionalFields_ShouldIncludeInJson()
    {
        // Arrange
        var sink = new LogAlertSink(_mockLogger.Object);
        var details = new Dictionary<string, object>
        {
            { "exceptionRatio", 0.25 },
            { "threshold", 0.15 }
        };

        var alertEvent = new AlertRaisedEventArgs
        {
            AlertCode = "EXCEPTION_CHUTE_RATIO_HIGH",
            Severity = AlertSeverity.Warning,
            Message = "Exception chute ratio too high",
            RaisedAt = DateTimeOffset.Now,
            LineId = "LINE-001",
            ChuteId = "CHUTE-42",
            NodeId = 101,
            Details = details
        };

        // Act
        await sink.WriteAlertAsync(alertEvent);

        // Assert - Verify the alert was logged with all fields
        _mockLogger.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => 
                    v.ToString()!.Contains("EXCEPTION_CHUTE_RATIO_HIGH") &&
                    v.ToString()!.Contains("LINE-001") &&
                    v.ToString()!.Contains("CHUTE-42") &&
                    v.ToString()!.Contains("101")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task WriteAlertAsync_WithMetrics_ShouldNotThrow()
    {
        // Arrange - Use real metrics instance (cannot mock because methods aren't virtual)
        var realMetrics = new PrometheusMetrics(Mock.Of<ISystemClock>());
        var sink = new LogAlertSink(_mockLogger.Object, realMetrics);
        var alertEvent = new AlertRaisedEventArgs
        {
            AlertCode = "TEST_ALERT",
            Severity = AlertSeverity.Warning,
            Message = "Test alert",
            RaisedAt = DateTimeOffset.Now
        };

        // Act & Assert - Should not throw when metrics are provided
        var exception = await Record.ExceptionAsync(async () => 
            await sink.WriteAlertAsync(alertEvent));
        Assert.Null(exception);
    }

    [Fact]
    public async Task WriteAlertAsync_WithoutMetrics_ShouldNotThrow()
    {
        // Arrange
        var sink = new LogAlertSink(_mockLogger.Object, null); // No metrics
        var alertEvent = new AlertRaisedEventArgs
        {
            AlertCode = "TEST_ALERT",
            Severity = AlertSeverity.Info,
            Message = "Test alert",
            RaisedAt = DateTimeOffset.Now
        };

        // Act & Assert - Should not throw
        var exception = await Record.ExceptionAsync(async () => 
            await sink.WriteAlertAsync(alertEvent));
        Assert.Null(exception);
    }

    [Fact]
    public async Task WriteAlertAsync_WithCancellationToken_ShouldComplete()
    {
        // Arrange
        var sink = new LogAlertSink(_mockLogger.Object);
        var cts = new CancellationTokenSource();
        var alertEvent = new AlertRaisedEventArgs
        {
            AlertCode = "TEST_ALERT",
            Severity = AlertSeverity.Info,
            Message = "Test alert",
            RaisedAt = DateTimeOffset.Now
        };

        // Act
        var task = sink.WriteAlertAsync(alertEvent, cts.Token);

        // Assert - Should complete successfully
        await task;
        Assert.True(task.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task WriteAlertAsync_WithException_ShouldLogErrorAndNotThrow()
    {
        // Arrange - Make logger throw exception on specific log level
        _mockLogger
            .Setup(x => x.Log(
                LogLevel.Critical,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
            .Throws(new Exception("Test exception"));

        var sink = new LogAlertSink(_mockLogger.Object);
        var alertEvent = new AlertRaisedEventArgs
        {
            AlertCode = "TEST_CRITICAL",
            Severity = AlertSeverity.Critical,
            Message = "Test critical alert",
            RaisedAt = DateTimeOffset.Now
        };

        // Act & Assert - Should not throw even if logger throws
        var exception = await Record.ExceptionAsync(async () => 
            await sink.WriteAlertAsync(alertEvent));
        Assert.Null(exception);
    }

    [Fact]
    public async Task WriteAlertAsync_MultipleCalls_ShouldLogAll()
    {
        // Arrange
        var sink = new LogAlertSink(_mockLogger.Object);

        // Act - Write multiple alerts
        await sink.WriteAlertAsync(new AlertRaisedEventArgs
        {
            AlertCode = "ALERT_1",
            Severity = AlertSeverity.Info,
            Message = "Alert 1",
            RaisedAt = DateTimeOffset.Now
        });

        await sink.WriteAlertAsync(new AlertRaisedEventArgs
        {
            AlertCode = "ALERT_2",
            Severity = AlertSeverity.Warning,
            Message = "Alert 2",
            RaisedAt = DateTimeOffset.Now
        });

        await sink.WriteAlertAsync(new AlertRaisedEventArgs
        {
            AlertCode = "ALERT_3",
            Severity = AlertSeverity.Critical,
            Message = "Alert 3",
            RaisedAt = DateTimeOffset.Now
        });

        // Assert - All three should be logged
        _mockLogger.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("[ALERT]")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Exactly(3));
    }
}
