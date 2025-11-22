using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ZakYip.WheelDiverterSorter.Observability;
using ZakYip.WheelDiverterSorter.Observability.Utilities;
using ZakYip.WheelDiverterSorter.Core.Enums.Observability;

namespace ZakYip.WheelDiverterSorter.Observability.Tests;

/// <summary>
/// Tests for alarm threshold boundary conditions
/// 测试报警阈值边界条件
/// </summary>
public class AlarmThresholdBoundaryTests
{
    private readonly Mock<ILogger<AlarmService>> _mockLogger;

    public AlarmThresholdBoundaryTests()
    {
        _mockLogger = new Mock<ILogger<AlarmService>>();
    }

    #region Sorting Failure Rate Boundary Tests

    [Fact]
    public void SortingFailureRate_ExactlyAtThreshold_DoesNotTriggerAlarm()
    {
        // Arrange
        var alarmService = new AlarmService(_mockLogger.Object, Mock.Of<ISystemClock>());
        
        // Act - Create exactly 5% failure rate (1 failure, 19 successes = 20 total, 5% exact)
        for (int i = 0; i < 19; i++)
        {
            alarmService.RecordSortingSuccess();
        }
        alarmService.RecordSortingFailure();

        // Assert
        var alarms = alarmService.GetActiveAlarms();
        var sortingFailureAlarm = alarms.FirstOrDefault(a => a.Type == AlarmType.SortingFailureRateHigh);
        Assert.Null(sortingFailureAlarm);
    }

    [Fact]
    public void SortingFailureRate_JustAboveThreshold_TriggersAlarm()
    {
        // Arrange
        var alarmService = new AlarmService(_mockLogger.Object, Mock.Of<ISystemClock>());
        
        // Act - Create 5.26% failure rate (1 failure, 18 successes = 19 total, but need 20+ samples)
        // So use 2 failures, 35 successes = 37 total, 5.41% failure rate
        for (int i = 0; i < 35; i++)
        {
            alarmService.RecordSortingSuccess();
        }
        for (int i = 0; i < 2; i++)
        {
            alarmService.RecordSortingFailure();
        }

        // Assert
        var alarms = alarmService.GetActiveAlarms();
        var sortingFailureAlarm = alarms.FirstOrDefault(a => a.Type == AlarmType.SortingFailureRateHigh);
        Assert.NotNull(sortingFailureAlarm);
        Assert.Equal(AlarmLevel.P1, sortingFailureAlarm.Level);
    }

    [Fact]
    public void SortingFailureRate_JustBelowThreshold_DoesNotTriggerAlarm()
    {
        // Arrange
        var alarmService = new AlarmService(_mockLogger.Object, Mock.Of<ISystemClock>());
        
        // Act - Create 4.76% failure rate (1 failure, 20 successes = 21 total, 4.76%)
        for (int i = 0; i < 20; i++)
        {
            alarmService.RecordSortingSuccess();
        }
        alarmService.RecordSortingFailure();

        // Assert
        var alarms = alarmService.GetActiveAlarms();
        var sortingFailureAlarm = alarms.FirstOrDefault(a => a.Type == AlarmType.SortingFailureRateHigh);
        Assert.Null(sortingFailureAlarm);
    }

    [Fact]
    public void SortingFailureRate_BelowMinimumSamples_DoesNotTriggerAlarm()
    {
        // Arrange
        var alarmService = new AlarmService(_mockLogger.Object, Mock.Of<ISystemClock>());
        
        // Act - Create 50% failure rate but only 10 samples (below minimum of 20)
        for (int i = 0; i < 5; i++)
        {
            alarmService.RecordSortingSuccess();
        }
        for (int i = 0; i < 5; i++)
        {
            alarmService.RecordSortingFailure();
        }

        // Assert
        var alarms = alarmService.GetActiveAlarms();
        var sortingFailureAlarm = alarms.FirstOrDefault(a => a.Type == AlarmType.SortingFailureRateHigh);
        Assert.Null(sortingFailureAlarm);
    }

    [Fact]
    public void SortingFailureRate_ExactlyMinimumSamples_TriggersAlarmIfOverThreshold()
    {
        // Arrange
        var alarmService = new AlarmService(_mockLogger.Object, Mock.Of<ISystemClock>());
        
        // Act - Create 10% failure rate with exactly 20 samples (2 failures, 18 successes)
        for (int i = 0; i < 18; i++)
        {
            alarmService.RecordSortingSuccess();
        }
        for (int i = 0; i < 2; i++)
        {
            alarmService.RecordSortingFailure();
        }

        // Assert
        var alarms = alarmService.GetActiveAlarms();
        var sortingFailureAlarm = alarms.FirstOrDefault(a => a.Type == AlarmType.SortingFailureRateHigh);
        Assert.NotNull(sortingFailureAlarm);
    }

    [Fact]
    public void SortingFailureRate_AllFailures_TriggersAlarm()
    {
        // Arrange
        var alarmService = new AlarmService(_mockLogger.Object, Mock.Of<ISystemClock>());
        
        // Act - 100% failure rate with 20 failures
        for (int i = 0; i < 20; i++)
        {
            alarmService.RecordSortingFailure();
        }

        // Assert
        var alarms = alarmService.GetActiveAlarms();
        var sortingFailureAlarm = alarms.FirstOrDefault(a => a.Type == AlarmType.SortingFailureRateHigh);
        Assert.NotNull(sortingFailureAlarm);
        Assert.Equal(1.0, (double)sortingFailureAlarm.Details["failureRate"], 2);
    }

    [Fact]
    public void SortingFailureRate_AllSuccesses_DoesNotTriggerAlarm()
    {
        // Arrange
        var alarmService = new AlarmService(_mockLogger.Object, Mock.Of<ISystemClock>());
        
        // Act - 0% failure rate with 100 successes
        for (int i = 0; i < 100; i++)
        {
            alarmService.RecordSortingSuccess();
        }

        // Assert
        var alarms = alarmService.GetActiveAlarms();
        var sortingFailureAlarm = alarms.FirstOrDefault(a => a.Type == AlarmType.SortingFailureRateHigh);
        Assert.Null(sortingFailureAlarm);
        Assert.Equal(0.0, alarmService.GetSortingFailureRate());
    }

    #endregion

    #region Queue Length Boundary Tests

    [Fact]
    public void QueueLength_ExactlyAtThreshold_DoesNotTriggerAlarm()
    {
        // Arrange
        var alarmService = new AlarmService(_mockLogger.Object, Mock.Of<ISystemClock>());

        // Act - Set queue to exactly 50 (threshold)
        alarmService.UpdateQueueLength(50);

        // Assert
        var alarms = alarmService.GetActiveAlarms();
        var queueAlarm = alarms.FirstOrDefault(a => a.Type == AlarmType.QueueBacklog);
        Assert.Null(queueAlarm);
    }

    [Fact]
    public void QueueLength_JustAboveThreshold_TriggersAlarm()
    {
        // Arrange
        var alarmService = new AlarmService(_mockLogger.Object, Mock.Of<ISystemClock>());

        // Act - Set queue to 51 (just above threshold)
        alarmService.UpdateQueueLength(51);

        // Assert
        var alarms = alarmService.GetActiveAlarms();
        var queueAlarm = alarms.FirstOrDefault(a => a.Type == AlarmType.QueueBacklog);
        Assert.NotNull(queueAlarm);
        Assert.Equal(AlarmLevel.P2, queueAlarm.Level);
        Assert.Equal(51, (int)queueAlarm.Details["queueLength"]);
    }

    [Fact]
    public void QueueLength_JustBelowThreshold_DoesNotTriggerAlarm()
    {
        // Arrange
        var alarmService = new AlarmService(_mockLogger.Object, Mock.Of<ISystemClock>());

        // Act - Set queue to 49 (just below threshold)
        alarmService.UpdateQueueLength(49);

        // Assert
        var alarms = alarmService.GetActiveAlarms();
        var queueAlarm = alarms.FirstOrDefault(a => a.Type == AlarmType.QueueBacklog);
        Assert.Null(queueAlarm);
    }

    [Fact]
    public void QueueLength_Zero_DoesNotTriggerAlarm()
    {
        // Arrange
        var alarmService = new AlarmService(_mockLogger.Object, Mock.Of<ISystemClock>());

        // Act
        alarmService.UpdateQueueLength(0);

        // Assert
        var alarms = alarmService.GetActiveAlarms();
        var queueAlarm = alarms.FirstOrDefault(a => a.Type == AlarmType.QueueBacklog);
        Assert.Null(queueAlarm);
    }

    [Fact]
    public void QueueLength_ExtremelyLarge_TriggersAlarm()
    {
        // Arrange
        var alarmService = new AlarmService(_mockLogger.Object, Mock.Of<ISystemClock>());

        // Act
        alarmService.UpdateQueueLength(10000);

        // Assert
        var alarms = alarmService.GetActiveAlarms();
        var queueAlarm = alarms.FirstOrDefault(a => a.Type == AlarmType.QueueBacklog);
        Assert.NotNull(queueAlarm);
        Assert.Equal(10000, (int)queueAlarm.Details["queueLength"]);
    }

    [Fact]
    public void QueueLength_FluctuatesAroundThreshold_TriggersAndClearsCorrectly()
    {
        // Arrange
        var alarmService = new AlarmService(_mockLogger.Object, Mock.Of<ISystemClock>());

        // Act & Assert - Cross threshold multiple times
        alarmService.UpdateQueueLength(51);
        Assert.Single(alarmService.GetActiveAlarms());

        alarmService.UpdateQueueLength(49);
        Assert.Empty(alarmService.GetActiveAlarms());

        alarmService.UpdateQueueLength(52);
        Assert.Single(alarmService.GetActiveAlarms());

        alarmService.UpdateQueueLength(50);
        Assert.Empty(alarmService.GetActiveAlarms());
    }

    #endregion

    #region Failure Rate Boundary with Reset

    [Fact]
    public void SortingFailureRate_AfterReset_StartsFromZero()
    {
        // Arrange
        var alarmService = new AlarmService(_mockLogger.Object, Mock.Of<ISystemClock>());
        
        // Trigger alarm first
        for (int i = 0; i < 18; i++)
        {
            alarmService.RecordSortingSuccess();
        }
        for (int i = 0; i < 2; i++)
        {
            alarmService.RecordSortingFailure();
        }
        Assert.NotEmpty(alarmService.GetActiveAlarms());

        // Act - Reset statistics
        alarmService.ResetSortingStatistics();

        // Assert
        Assert.Equal(0.0, alarmService.GetSortingFailureRate());
        
        // Add new data below threshold
        for (int i = 0; i < 19; i++)
        {
            alarmService.RecordSortingSuccess();
        }
        alarmService.RecordSortingFailure();
        
        var alarms = alarmService.GetActiveAlarms();
        var sortingFailureAlarm = alarms.FirstOrDefault(a => a.Type == AlarmType.SortingFailureRateHigh);
        // Note: The alarm from before reset may still be active
        // This tests that the rate calculation is reset
    }

    #endregion

    #region Multiple Alarm Types Coexisting

    [Fact]
    public void MultipleAlarmTypes_CanCoexistAtBoundaries()
    {
        // Arrange
        var alarmService = new AlarmService(_mockLogger.Object, Mock.Of<ISystemClock>());

        // Act - Trigger multiple alarms at exact boundaries
        alarmService.UpdateQueueLength(51); // Just above threshold
        
        // Create failure rate above threshold with minimum 20 samples
        // 2 failures, 35 successes = 37 total, 5.41% failure rate
        for (int i = 0; i < 35; i++)
        {
            alarmService.RecordSortingSuccess();
        }
        for (int i = 0; i < 2; i++)
        {
            alarmService.RecordSortingFailure();
        }
        
        alarmService.ReportDiverterFault("D1", "Test fault");

        // Assert
        var alarms = alarmService.GetActiveAlarms();
        Assert.Equal(3, alarms.Count);
        Assert.Contains(alarms, a => a.Type == AlarmType.QueueBacklog);
        Assert.Contains(alarms, a => a.Type == AlarmType.SortingFailureRateHigh);
        Assert.Contains(alarms, a => a.Type == AlarmType.DiverterFault);
    }

    #endregion
}
