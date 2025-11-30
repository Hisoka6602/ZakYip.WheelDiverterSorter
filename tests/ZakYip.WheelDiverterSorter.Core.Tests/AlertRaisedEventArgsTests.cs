using ZakYip.WheelDiverterSorter.Core.Events.Monitoring;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Events.Sorting;
using ZakYip.WheelDiverterSorter.Core.Events.Hardware;
using ZakYip.WheelDiverterSorter.Core.Events.Chute;
using Xunit;
using ZakYip.WheelDiverterSorter.Core.Enums.Monitoring;

namespace ZakYip.WheelDiverterSorter.Core.Tests;

public class AlertRaisedEventArgsTests
{
    [Fact]
    public void AlertRaisedEventArgs_RequiredFields_ShouldBeSet()
    {
        // Arrange
        var alertCode = "TEST_ALERT";
        var severity = AlertSeverity.Warning;
        var message = "Test alert message";
        var raisedAt = DateTimeOffset.Now;

        // Act
        var alertEvent = new AlertRaisedEventArgs
        {
            AlertCode = alertCode,
            Severity = severity,
            Message = message,
            RaisedAt = raisedAt
        };

        // Assert
        Assert.Equal(alertCode, alertEvent.AlertCode);
        Assert.Equal(severity, alertEvent.Severity);
        Assert.Equal(message, alertEvent.Message);
        Assert.Equal(raisedAt, alertEvent.RaisedAt);
    }

    [Fact]
    public void AlertRaisedEventArgs_OptionalFields_ShouldBeNullByDefault()
    {
        // Arrange & Act
        var alertEvent = new AlertRaisedEventArgs
        {
            AlertCode = "TEST",
            Severity = AlertSeverity.Info,
            Message = "Test",
            RaisedAt = DateTimeOffset.Now
        };

        // Assert
        Assert.Null(alertEvent.LineId);
        Assert.Null(alertEvent.ChuteId);
        Assert.Null(alertEvent.NodeId);
        Assert.Null(alertEvent.Details);
    }

    [Fact]
    public void AlertRaisedEventArgs_WithOptionalFields_ShouldBeSet()
    {
        // Arrange
        var lineId = "LINE-001";
        var chuteId = "CHUTE-42";
        var nodeId = 101;
        var details = new Dictionary<string, object>
        {
            { "additionalInfo", "test info" },
            { "count", 5 }
        };

        // Act
        var alertEvent = new AlertRaisedEventArgs
        {
            AlertCode = "TEST",
            Severity = AlertSeverity.Critical,
            Message = "Test",
            RaisedAt = DateTimeOffset.Now,
            LineId = lineId,
            ChuteId = chuteId,
            NodeId = nodeId,
            Details = details
        };

        // Assert
        Assert.Equal(lineId, alertEvent.LineId);
        Assert.Equal(chuteId, alertEvent.ChuteId);
        Assert.Equal(nodeId, alertEvent.NodeId);
        Assert.NotNull(alertEvent.Details);
        Assert.Equal(2, alertEvent.Details.Count);
        Assert.Equal("test info", alertEvent.Details["additionalInfo"]);
        Assert.Equal(5, alertEvent.Details["count"]);
    }

    [Fact]
    public void AlertRaisedEventArgs_RecordEquality_ShouldWorkCorrectly()
    {
        // Arrange
        var raisedAt = DateTimeOffset.Now;
        var alert1 = new AlertRaisedEventArgs
        {
            AlertCode = "TEST",
            Severity = AlertSeverity.Warning,
            Message = "Test message",
            RaisedAt = raisedAt
        };

        var alert2 = new AlertRaisedEventArgs
        {
            AlertCode = "TEST",
            Severity = AlertSeverity.Warning,
            Message = "Test message",
            RaisedAt = raisedAt
        };

        var alert3 = new AlertRaisedEventArgs
        {
            AlertCode = "OTHER",
            Severity = AlertSeverity.Warning,
            Message = "Test message",
            RaisedAt = raisedAt
        };

        // Act & Assert
        Assert.Equal(alert1, alert2);
        Assert.NotEqual(alert1, alert3);
    }
}
