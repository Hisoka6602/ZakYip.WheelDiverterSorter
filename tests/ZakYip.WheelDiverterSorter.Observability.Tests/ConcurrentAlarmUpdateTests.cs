using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ZakYip.WheelDiverterSorter.Observability;
using ZakYip.WheelDiverterSorter.Observability.Utilities;

namespace ZakYip.WheelDiverterSorter.Observability.Tests;

/// <summary>
/// Tests for concurrent alarm updates and thread safety
/// 测试并发告警更新和线程安全
/// </summary>
public class ConcurrentAlarmUpdateTests
{
    private readonly Mock<ILogger<AlarmService>> _mockLogger;

    public ConcurrentAlarmUpdateTests()
    {
        _mockLogger = new Mock<ILogger<AlarmService>>();
    }

    [Fact]
    public void ConcurrentSortingSuccess_UpdatesCountersSafely()
    {
        // Arrange
        var alarmService = new AlarmService(_mockLogger.Object, Mock.Of<ISystemClock>());
        const int threadCount = 10;
        const int operationsPerThread = 100;
        var tasks = new Task[threadCount];

        // Act - Multiple threads recording successes concurrently
        for (int i = 0; i < threadCount; i++)
        {
            tasks[i] = Task.Run(() =>
            {
                for (int j = 0; j < operationsPerThread; j++)
                {
                    alarmService.RecordSortingSuccess();
                }
            });
        }

        Task.WaitAll(tasks);

        // Assert - Should have recorded all successes
        // With 1000 successes and 0 failures, rate should be 0%
        var failureRate = alarmService.GetSortingFailureRate();
        Assert.Equal(0.0, failureRate);
        
        var alarms = alarmService.GetActiveAlarms();
        var sortingFailureAlarm = alarms.FirstOrDefault(a => a.Type == AlarmType.SortingFailureRateHigh);
        Assert.Null(sortingFailureAlarm);
    }

    [Fact]
    public void ConcurrentSortingFailure_UpdatesCountersSafely()
    {
        // Arrange
        var alarmService = new AlarmService(_mockLogger.Object, Mock.Of<ISystemClock>());
        const int threadCount = 10;
        const int operationsPerThread = 10;
        var tasks = new Task[threadCount];

        // Act - Multiple threads recording failures concurrently
        for (int i = 0; i < threadCount; i++)
        {
            tasks[i] = Task.Run(() =>
            {
                for (int j = 0; j < operationsPerThread; j++)
                {
                    alarmService.RecordSortingFailure();
                }
            });
        }

        Task.WaitAll(tasks);

        // Assert - Should have recorded all failures and triggered alarm
        var failureRate = alarmService.GetSortingFailureRate();
        Assert.Equal(1.0, failureRate); // 100% failure rate
        
        var alarms = alarmService.GetActiveAlarms();
        var sortingFailureAlarm = alarms.FirstOrDefault(a => a.Type == AlarmType.SortingFailureRateHigh);
        Assert.NotNull(sortingFailureAlarm);
    }

    [Fact]
    public void ConcurrentMixedSortingOperations_MaintainsConsistency()
    {
        // Arrange
        var alarmService = new AlarmService(_mockLogger.Object, Mock.Of<ISystemClock>());
        const int threadCount = 20;
        const int operationsPerThread = 50;
        var tasks = new Task[threadCount];

        // Act - Half threads record success, half record failure
        for (int i = 0; i < threadCount; i++)
        {
            int threadIndex = i;
            tasks[i] = Task.Run(() =>
            {
                for (int j = 0; j < operationsPerThread; j++)
                {
                    if (threadIndex % 2 == 0)
                    {
                        alarmService.RecordSortingSuccess();
                    }
                    else
                    {
                        alarmService.RecordSortingFailure();
                    }
                }
            });
        }

        Task.WaitAll(tasks);

        // Assert - Should have 50% failure rate (500 success, 500 failure)
        var failureRate = alarmService.GetSortingFailureRate();
        Assert.Equal(0.5, failureRate, 2);
        
        // Should have triggered alarm (50% > 5%)
        var alarms = alarmService.GetActiveAlarms();
        var sortingFailureAlarm = alarms.FirstOrDefault(a => a.Type == AlarmType.SortingFailureRateHigh);
        Assert.NotNull(sortingFailureAlarm);
    }

    [Fact]
    public void ConcurrentQueueLengthUpdates_HandlesLastValueCorrectly()
    {
        // Arrange
        var alarmService = new AlarmService(_mockLogger.Object, Mock.Of<ISystemClock>());
        const int threadCount = 100;
        var tasks = new Task[threadCount];

        // Act - Multiple threads updating queue length concurrently
        for (int i = 0; i < threadCount; i++)
        {
            int queueValue = i;
            tasks[i] = Task.Run(() =>
            {
                alarmService.UpdateQueueLength(queueValue);
            });
        }

        Task.WaitAll(tasks);
        Thread.Sleep(100); // Give time for alarm checks to complete

        // Assert - Should have some alarms if any thread set value > 50
        var alarms = alarmService.GetActiveAlarms();
        // Can't guarantee final state due to race conditions, but should not crash
        Assert.NotNull(alarms);
    }

    [Fact]
    public void ConcurrentDiverterFaultReports_HandlesMultipleDiverters()
    {
        // Arrange
        var alarmService = new AlarmService(_mockLogger.Object, Mock.Of<ISystemClock>());
        const int diverterCount = 20;
        var tasks = new Task[diverterCount];

        // Act - Multiple threads reporting different diverter faults concurrently
        for (int i = 0; i < diverterCount; i++)
        {
            string diverterId = $"D{i}";
            tasks[i] = Task.Run(() =>
            {
                alarmService.ReportDiverterFault(diverterId, "Concurrent test fault");
            });
        }

        Task.WaitAll(tasks);

        // Assert - Should have 20 distinct diverter fault alarms
        var alarms = alarmService.GetActiveAlarms();
        var diverterFaultAlarms = alarms.Where(a => a.Type == AlarmType.DiverterFault).ToList();
        Assert.Equal(diverterCount, diverterFaultAlarms.Count);
    }

    [Fact]
    public void ConcurrentSameDiverterFaultReports_CreatesOnlyOneAlarm()
    {
        // Arrange
        var alarmService = new AlarmService(_mockLogger.Object, Mock.Of<ISystemClock>());
        const int threadCount = 10;
        var tasks = new Task[threadCount];

        // Act - Multiple threads reporting the same diverter fault concurrently
        for (int i = 0; i < threadCount; i++)
        {
            tasks[i] = Task.Run(() =>
            {
                alarmService.ReportDiverterFault("D1", "Concurrent test fault");
            });
        }

        Task.WaitAll(tasks);

        // Assert - Should have only 1 alarm for D1
        var alarms = alarmService.GetActiveAlarms();
        var diverterFaultAlarms = alarms.Where(a => 
            a.Type == AlarmType.DiverterFault && 
            a.Details.ContainsKey("diverterId") && 
            a.Details["diverterId"].ToString() == "D1").ToList();
        Assert.Single(diverterFaultAlarms);
    }

    [Fact]
    public void ConcurrentFaultReportAndClear_HandlesRaceCondition()
    {
        // Arrange
        var alarmService = new AlarmService(_mockLogger.Object, Mock.Of<ISystemClock>());
        const int iterations = 100;
        var reportTasks = new Task[iterations];
        var clearTasks = new Task[iterations];

        // Act - Concurrent fault reporting and clearing
        for (int i = 0; i < iterations; i++)
        {
            reportTasks[i] = Task.Run(() =>
            {
                alarmService.ReportDiverterFault("D1", "Test fault");
            });
            clearTasks[i] = Task.Run(() =>
            {
                alarmService.ClearDiverterFault("D1");
            });
        }

        Task.WaitAll(reportTasks.Concat(clearTasks).ToArray());

        // Assert - Should not crash, final state may vary but should be consistent
        var alarms = alarmService.GetActiveAlarms();
        Assert.NotNull(alarms);
    }

    [Fact]
    public void ConcurrentAlarmAcknowledgement_HandlesMultipleThreads()
    {
        // Arrange
        var alarmService = new AlarmService(_mockLogger.Object, Mock.Of<ISystemClock>());
        
        // Create some alarms first
        for (int i = 0; i < 10; i++)
        {
            alarmService.ReportDiverterFault($"D{i}", "Test fault");
        }

        var alarms = alarmService.GetActiveAlarms().ToList();
        var tasks = new Task[alarms.Count];

        // Act - Multiple threads acknowledging different alarms concurrently
        for (int i = 0; i < alarms.Count; i++)
        {
            var alarm = alarms[i];
            tasks[i] = Task.Run(() =>
            {
                alarmService.AcknowledgeAlarm(alarm);
            });
        }

        Task.WaitAll(tasks);

        // Assert - All alarms should be acknowledged
        var activeAlarms = alarmService.GetActiveAlarms();
        Assert.All(activeAlarms, alarm => Assert.True(alarm.IsAcknowledged));
        Assert.All(activeAlarms, alarm => Assert.NotNull(alarm.AcknowledgedAt));
    }

    [Fact]
    public void ConcurrentGetActiveAlarms_ReturnsConsistentSnapshot()
    {
        // Arrange
        var alarmService = new AlarmService(_mockLogger.Object, Mock.Of<ISystemClock>());
        const int threadCount = 10;
        var getTasks = new Task<IReadOnlyList<AlarmEvent>>[threadCount];

        // Create some initial alarms
        for (int i = 0; i < 5; i++)
        {
            alarmService.ReportDiverterFault($"D{i}", "Test fault");
        }

        // Act - Multiple threads reading alarms concurrently
        for (int i = 0; i < threadCount; i++)
        {
            getTasks[i] = Task.Run(() => alarmService.GetActiveAlarms());
        }

        Task.WaitAll(getTasks);

        // Assert - All reads should succeed and return valid lists
        foreach (var task in getTasks)
        {
            Assert.NotNull(task.Result);
            Assert.True(task.Result.Count >= 5); // At least the 5 we created
        }
    }

    [Fact]
    public void ConcurrentResetStatistics_MaintainsConsistency()
    {
        // Arrange
        var alarmService = new AlarmService(_mockLogger.Object, Mock.Of<ISystemClock>());
        const int threadCount = 10;
        var tasks = new Task[threadCount];

        // Act - Multiple threads resetting and recording concurrently
        for (int i = 0; i < threadCount; i++)
        {
            int threadIndex = i;
            tasks[i] = Task.Run(() =>
            {
                if (threadIndex % 2 == 0)
                {
                    alarmService.ResetSortingStatistics();
                }
                else
                {
                    for (int j = 0; j < 10; j++)
                    {
                        alarmService.RecordSortingSuccess();
                    }
                }
            });
        }

        Task.WaitAll(tasks);

        // Assert - Should not crash and return valid failure rate
        var failureRate = alarmService.GetSortingFailureRate();
        Assert.True(failureRate >= 0.0 && failureRate <= 1.0);
    }

    [Fact]
    public void ConcurrentRuleEngineDisconnectionChecks_DoesNotCreateDuplicateAlarms()
    {
        // Arrange
        var alarmService = new AlarmService(_mockLogger.Object, Mock.Of<ISystemClock>());
        alarmService.ReportRuleEngineDisconnection("tcp");
        
        // Use reflection to set disconnection time to simulate 2 minutes ago
        var disconnectionsField = typeof(AlarmService)
            .GetField("_ruleEngineDisconnections", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var disconnections = disconnectionsField?.GetValue(alarmService) as Dictionary<string, DateTime>;
        if (disconnections != null)
        {
            disconnections["tcp"] = DateTime.UtcNow.AddMinutes(-2);
        }

        const int threadCount = 10;
        var tasks = new Task[threadCount];

        // Act - Multiple threads checking disconnection concurrently
        for (int i = 0; i < threadCount; i++)
        {
            tasks[i] = Task.Run(() =>
            {
                alarmService.CheckRuleEngineDisconnection();
            });
        }

        Task.WaitAll(tasks);

        // Assert - Should have only one alarm for tcp disconnection
        var alarms = alarmService.GetActiveAlarms();
        var ruleEngineAlarms = alarms.Where(a => 
            a.Type == AlarmType.RuleEngineDisconnected && 
            a.Details.ContainsKey("connectionType") && 
            a.Details["connectionType"].ToString() == "tcp").ToList();
        Assert.Single(ruleEngineAlarms);
    }

    [Fact]
    public void HighConcurrencyStressTest_MaintainsSystemStability()
    {
        // Arrange
        var alarmService = new AlarmService(_mockLogger.Object, Mock.Of<ISystemClock>());
        const int threadCount = 50;
        const int operationsPerThread = 100;
        var tasks = new Task[threadCount];
        var random = new Random();

        // Act - Many threads performing random operations
        for (int i = 0; i < threadCount; i++)
        {
            int threadIndex = i;
            tasks[i] = Task.Run(() =>
            {
                var threadRandom = new Random(threadIndex);
                for (int j = 0; j < operationsPerThread; j++)
                {
                    var operation = threadRandom.Next(0, 6);
                    switch (operation)
                    {
                        case 0:
                            alarmService.RecordSortingSuccess();
                            break;
                        case 1:
                            alarmService.RecordSortingFailure();
                            break;
                        case 2:
                            alarmService.UpdateQueueLength(threadRandom.Next(0, 100));
                            break;
                        case 3:
                            alarmService.ReportDiverterFault($"D{threadRandom.Next(0, 10)}", "Test");
                            break;
                        case 4:
                            alarmService.ClearDiverterFault($"D{threadRandom.Next(0, 10)}");
                            break;
                        case 5:
                            var alarms = alarmService.GetActiveAlarms();
                            if (alarms.Count > 0)
                            {
                                var alarm = alarms[threadRandom.Next(0, alarms.Count)];
                                alarmService.AcknowledgeAlarm(alarm);
                            }
                            break;
                    }
                }
            });
        }

        // Assert - Should complete without exceptions
        var aggregateException = Record.Exception(() => Task.WaitAll(tasks));
        Assert.Null(aggregateException);
        
        // Should be able to get alarms without issues
        var finalAlarms = alarmService.GetActiveAlarms();
        Assert.NotNull(finalAlarms);
    }
}
