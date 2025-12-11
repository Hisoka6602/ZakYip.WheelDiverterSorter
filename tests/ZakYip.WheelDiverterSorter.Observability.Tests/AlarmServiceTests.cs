using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using Microsoft.Extensions.Logging;
using Moq;
using System.Collections.Concurrent;
using Xunit;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Observability;
using ZakYip.WheelDiverterSorter.Observability.Utilities;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Enums.Monitoring;

namespace ZakYip.WheelDiverterSorter.Observability.Tests;

public class AlarmServiceTests
{
    private readonly AlarmService _alarmService;
    private readonly Mock<ILogger<AlarmService>> _mockLogger;
    private readonly Mock<ISystemClock> _mockClock;

    public AlarmServiceTests()
    {
        _mockLogger = new Mock<ILogger<AlarmService>>();
        _mockClock = new Mock<ISystemClock>();
        _mockClock.Setup(c => c.LocalNow).Returns(DateTime.Now);
        _alarmService = new AlarmService(_mockLogger.Object, _mockClock.Object);
    }

    [Fact]
    public void GetActiveAlarms_Initially_ReturnsEmptyList()
    {
        // Act
        var alarms = _alarmService.GetActiveAlarms();

        // Assert
        Assert.Empty(alarms);
    }

    [Fact]
    public void RecordSortingFailure_WhenFailureRateExceeds5Percent_TriggersP1Alarm()
    {
        // Arrange - Record 20 successes and 2 failures (10% failure rate > 5%)
        for (int i = 0; i < 18; i++)
        {
            _alarmService.RecordSortingSuccess();
        }

        // Act
        for (int i = 0; i < 2; i++)
        {
            _alarmService.RecordSortingFailure();
        }

        // Assert
        var alarms = _alarmService.GetActiveAlarms();
        var sortingFailureAlarm = alarms.FirstOrDefault(a => a.Type == AlarmType.SortingFailureRateHigh);
        
        Assert.NotNull(sortingFailureAlarm);
        Assert.Equal(AlarmLevel.P1, sortingFailureAlarm.Level);
        Assert.Contains("分拣失败率过高", sortingFailureAlarm.Message);
    }

    [Fact]
    public void RecordSortingSuccess_WhenFailureRateDropsBelow5Percent_ClearsAlarm()
    {
        // Arrange - First trigger alarm
        for (int i = 0; i < 18; i++)
        {
            _alarmService.RecordSortingSuccess();
        }
        for (int i = 0; i < 2; i++)
        {
            _alarmService.RecordSortingFailure();
        }

        // Verify alarm exists
        Assert.NotEmpty(_alarmService.GetActiveAlarms());

        // Act - Add more successes to bring rate below 5%
        for (int i = 0; i < 30; i++)
        {
            _alarmService.RecordSortingSuccess();
        }

        // Assert
        var alarms = _alarmService.GetActiveAlarms();
        var sortingFailureAlarm = alarms.FirstOrDefault(a => a.Type == AlarmType.SortingFailureRateHigh);
        Assert.Null(sortingFailureAlarm);
    }

    [Fact]
    public void UpdateQueueLength_WhenExceeds50Parcels_TriggersP2Alarm()
    {
        // Act
        _alarmService.UpdateQueueLength(51);

        // Assert
        var alarms = _alarmService.GetActiveAlarms();
        var queueBacklogAlarm = alarms.FirstOrDefault(a => a.Type == AlarmType.QueueBacklog);
        
        Assert.NotNull(queueBacklogAlarm);
        Assert.Equal(AlarmLevel.P2, queueBacklogAlarm.Level);
        Assert.Contains("队列积压", queueBacklogAlarm.Message);
    }

    [Fact]
    public void UpdateQueueLength_WhenDropsBelow50Parcels_ClearsAlarm()
    {
        // Arrange
        _alarmService.UpdateQueueLength(51);
        Assert.NotEmpty(_alarmService.GetActiveAlarms());

        // Act
        _alarmService.UpdateQueueLength(40);

        // Assert
        var alarms = _alarmService.GetActiveAlarms();
        var queueBacklogAlarm = alarms.FirstOrDefault(a => a.Type == AlarmType.QueueBacklog);
        Assert.Null(queueBacklogAlarm);
    }

    [Fact]
    public void ReportDiverterFault_TriggersP1Alarm()
    {
        // Act
        _alarmService.ReportDiverterFault("D1", "Motor stalled");

        // Assert
        var alarms = _alarmService.GetActiveAlarms();
        var diverterFaultAlarm = alarms.FirstOrDefault(a => a.Type == AlarmType.DiverterFault);
        
        Assert.NotNull(diverterFaultAlarm);
        Assert.Equal(AlarmLevel.P1, diverterFaultAlarm.Level);
        Assert.Contains("摆轮故障", diverterFaultAlarm.Message);
        Assert.Contains("D1", diverterFaultAlarm.Message);
    }

    [Fact]
    public void ClearDiverterFault_RemovesAlarm()
    {
        // Arrange
        _alarmService.ReportDiverterFault("D1", "Motor stalled");
        Assert.NotEmpty(_alarmService.GetActiveAlarms());

        // Act
        _alarmService.ClearDiverterFault("D1");

        // Assert
        var alarms = _alarmService.GetActiveAlarms();
        var diverterFaultAlarm = alarms.FirstOrDefault(a => 
            a.Type == AlarmType.DiverterFault && 
            a.Details.ContainsKey("diverterId") &&
            a.Details["diverterId"].ToString() == "D1");
        Assert.Null(diverterFaultAlarm);
    }

    [Fact]
    public void ReportSensorFault_TriggersP2Alarm()
    {
        // Act
        _alarmService.ReportSensorFault("S1", "No signal detected");

        // Assert
        var alarms = _alarmService.GetActiveAlarms();
        var sensorFaultAlarm = alarms.FirstOrDefault(a => a.Type == AlarmType.SensorFault);
        
        Assert.NotNull(sensorFaultAlarm);
        Assert.Equal(AlarmLevel.P2, sensorFaultAlarm.Level);
        Assert.Contains("传感器故障", sensorFaultAlarm.Message);
        Assert.Contains("S1", sensorFaultAlarm.Message);
    }

    [Fact]
    public void ClearSensorFault_RemovesAlarm()
    {
        // Arrange
        _alarmService.ReportSensorFault("S1", "No signal detected");
        Assert.NotEmpty(_alarmService.GetActiveAlarms());

        // Act
        _alarmService.ClearSensorFault("S1");

        // Assert
        var alarms = _alarmService.GetActiveAlarms();
        var sensorFaultAlarm = alarms.FirstOrDefault(a => 
            a.Type == AlarmType.SensorFault && 
            a.Details.ContainsKey("sensorId") &&
            a.Details["sensorId"].ToString() == "S1");
        Assert.Null(sensorFaultAlarm);
    }

    [Fact]
    public void CheckRuleEngineDisconnection_WhenDisconnectedOverOneMinute_TriggersP1Alarm()
    {
        // Arrange
        _alarmService.ReportRuleEngineDisconnection("tcp");
        
        // Simulate waiting for over 1 minute
        Thread.Sleep(100); // Small delay to ensure disconnection is recorded
        
        // Use reflection to set disconnection time to simulate 1+ minute
        var disconnectionsField = typeof(AlarmService)
            .GetField("_ruleEngineDisconnections", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var disconnections = disconnectionsField?.GetValue(_alarmService) as ConcurrentDictionary<string, DateTime>;
        if (disconnections != null)
        {
            disconnections["tcp"] = DateTime.UtcNow.AddMinutes(-2);
        }

        // Act
        _alarmService.CheckRuleEngineDisconnection();

        // Assert
        var alarms = _alarmService.GetActiveAlarms();
        var ruleEngineAlarm = alarms.FirstOrDefault(a => a.Type == AlarmType.RuleEngineDisconnected);
        
        Assert.NotNull(ruleEngineAlarm);
        Assert.Equal(AlarmLevel.P1, ruleEngineAlarm.Level);
        Assert.Contains("RuleEngine断线", ruleEngineAlarm.Message);
    }

    [Fact]
    public void ReportRuleEngineReconnection_ClearsAlarm()
    {
        // Arrange
        _alarmService.ReportRuleEngineDisconnection("tcp");
        
        // Use reflection to simulate 1+ minute disconnection
        var disconnectionsField = typeof(AlarmService)
            .GetField("_ruleEngineDisconnections", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var disconnections = disconnectionsField?.GetValue(_alarmService) as ConcurrentDictionary<string, DateTime>;
        if (disconnections != null)
        {
            disconnections["tcp"] = DateTime.UtcNow.AddMinutes(-2);
        }
        
        _alarmService.CheckRuleEngineDisconnection();
        Assert.NotEmpty(_alarmService.GetActiveAlarms());

        // Act
        _alarmService.ReportRuleEngineReconnection("tcp");

        // Assert
        var alarms = _alarmService.GetActiveAlarms();
        var ruleEngineAlarm = alarms.FirstOrDefault(a => 
            a.Type == AlarmType.RuleEngineDisconnected && 
            a.Details.ContainsKey("connectionType") &&
            a.Details["connectionType"].ToString() == "tcp");
        Assert.Null(ruleEngineAlarm);
    }

    [Fact]
    public void ReportSystemRestart_TriggersP3Alarm()
    {
        // Act
        _alarmService.ReportSystemRestart();

        // Assert
        var alarms = _alarmService.GetActiveAlarms();
        var systemRestartAlarm = alarms.FirstOrDefault(a => a.Type == AlarmType.SystemRestart);
        
        Assert.NotNull(systemRestartAlarm);
        Assert.Equal(AlarmLevel.P3, systemRestartAlarm.Level);
        Assert.Contains("系统重启", systemRestartAlarm.Message);
    }

    [Fact]
    public void AcknowledgeAlarm_SetsAcknowledgedFlag()
    {
        // Arrange
        _alarmService.ReportDiverterFault("D1", "Test fault");
        var alarms = _alarmService.GetActiveAlarms();
        var alarm = alarms.First();
        Assert.False(alarm.IsAcknowledged);

        // Act
        _alarmService.AcknowledgeAlarm(alarm);

        // Assert
        Assert.True(alarm.IsAcknowledged);
        Assert.NotNull(alarm.AcknowledgedAt);
    }

    [Fact]
    public void GetSortingFailureRate_ReturnsCorrectRate()
    {
        // Arrange
        for (int i = 0; i < 90; i++)
        {
            _alarmService.RecordSortingSuccess();
        }
        for (int i = 0; i < 10; i++)
        {
            _alarmService.RecordSortingFailure();
        }

        // Act
        var failureRate = _alarmService.GetSortingFailureRate();

        // Assert
        Assert.Equal(0.1, failureRate, 2); // 10% failure rate
    }

    [Fact]
    public void ResetSortingStatistics_ClearsCounters()
    {
        // Arrange
        _alarmService.RecordSortingSuccess();
        _alarmService.RecordSortingFailure();
        Assert.NotEqual(0, _alarmService.GetSortingFailureRate());

        // Act
        _alarmService.ResetSortingStatistics();

        // Assert
        Assert.Equal(0, _alarmService.GetSortingFailureRate());
    }
}
