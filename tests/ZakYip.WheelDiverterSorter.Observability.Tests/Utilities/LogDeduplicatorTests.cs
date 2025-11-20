using Microsoft.Extensions.Logging;
using Xunit;
using ZakYip.WheelDiverterSorter.Observability.Utilities;

namespace ZakYip.WheelDiverterSorter.Observability.Tests.Utilities;

/// <summary>
/// Tests for LogDeduplicator - PR-37
/// </summary>
public class LogDeduplicatorTests
{
    [Fact]
    public void ShouldLog_FirstTime_ReturnsTrue()
    {
        // Arrange
        var clock = new TestSystemClock(new DateTime(2024, 1, 1, 12, 0, 0));
        var deduplicator = new LogDeduplicator(clock);

        // Act
        var result = deduplicator.ShouldLog(LogLevel.Error, "Test message");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ShouldLog_WithinWindow_ReturnsFalse()
    {
        // Arrange
        var clock = new TestSystemClock(new DateTime(2024, 1, 1, 12, 0, 0));
        var deduplicator = new LogDeduplicator(clock, windowDurationSeconds: 1);

        // First log
        deduplicator.RecordLog(LogLevel.Error, "Test message");

        // Act - within 1 second window
        clock.AdvanceTime(TimeSpan.FromMilliseconds(500));
        var result = deduplicator.ShouldLog(LogLevel.Error, "Test message");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ShouldLog_AfterWindow_ReturnsTrue()
    {
        // Arrange
        var clock = new TestSystemClock(new DateTime(2024, 1, 1, 12, 0, 0));
        var deduplicator = new LogDeduplicator(clock, windowDurationSeconds: 1);

        // First log
        deduplicator.RecordLog(LogLevel.Error, "Test message");

        // Act - after 1 second window
        clock.AdvanceTime(TimeSpan.FromSeconds(1.1));
        var result = deduplicator.ShouldLog(LogLevel.Error, "Test message");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ShouldLog_DifferentLevel_ReturnsTrue()
    {
        // Arrange
        var clock = new TestSystemClock(new DateTime(2024, 1, 1, 12, 0, 0));
        var deduplicator = new LogDeduplicator(clock);

        // First log at Error level
        deduplicator.RecordLog(LogLevel.Error, "Test message");

        // Act - same message but different level
        var result = deduplicator.ShouldLog(LogLevel.Warning, "Test message");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ShouldLog_DifferentMessage_ReturnsTrue()
    {
        // Arrange
        var clock = new TestSystemClock(new DateTime(2024, 1, 1, 12, 0, 0));
        var deduplicator = new LogDeduplicator(clock);

        // First log
        deduplicator.RecordLog(LogLevel.Error, "Test message 1");

        // Act - different message
        var result = deduplicator.ShouldLog(LogLevel.Error, "Test message 2");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ShouldLog_DifferentExceptionType_ReturnsTrue()
    {
        // Arrange
        var clock = new TestSystemClock(new DateTime(2024, 1, 1, 12, 0, 0));
        var deduplicator = new LogDeduplicator(clock);

        // First log with exception type A
        deduplicator.RecordLog(LogLevel.Error, "Test message", "ArgumentException");

        // Act - same message but different exception type
        var result = deduplicator.ShouldLog(LogLevel.Error, "Test message", "InvalidOperationException");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ShouldLog_SameExceptionType_WithinWindow_ReturnsFalse()
    {
        // Arrange
        var clock = new TestSystemClock(new DateTime(2024, 1, 1, 12, 0, 0));
        var deduplicator = new LogDeduplicator(clock);

        // First log with exception type
        deduplicator.RecordLog(LogLevel.Error, "Test message", "ArgumentException");

        // Act - within window, same exception type
        clock.AdvanceTime(TimeSpan.FromMilliseconds(500));
        var result = deduplicator.ShouldLog(LogLevel.Error, "Test message", "ArgumentException");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ShouldLog_EmptyMessage_AlwaysReturnsTrue()
    {
        // Arrange
        var clock = new TestSystemClock(new DateTime(2024, 1, 1, 12, 0, 0));
        var deduplicator = new LogDeduplicator(clock);

        // Act
        var result1 = deduplicator.ShouldLog(LogLevel.Error, "");
        var result2 = deduplicator.ShouldLog(LogLevel.Error, "");

        // Assert
        Assert.True(result1);
        Assert.True(result2);
    }

    [Fact]
    public void RecordLog_MultipleTimes_OnlyLastTimeIsRecorded()
    {
        // Arrange
        var clock = new TestSystemClock(new DateTime(2024, 1, 1, 12, 0, 0));
        var deduplicator = new LogDeduplicator(clock);

        // Record at T=0
        deduplicator.RecordLog(LogLevel.Error, "Test message");

        // Advance time but still within window
        clock.AdvanceTime(TimeSpan.FromMilliseconds(500));

        // Record again at T=500ms
        deduplicator.RecordLog(LogLevel.Error, "Test message");

        // Advance time by another 600ms (total 1100ms from first, 600ms from second)
        clock.AdvanceTime(TimeSpan.FromMilliseconds(600));

        // Act - should return true because 600ms > window from last record
        var result = deduplicator.ShouldLog(LogLevel.Error, "Test message");

        // Assert - would be false if using first record time, true with last record time
        Assert.False(result); // Still within 1 second of second record
    }

    [Fact]
    public void LogDeduplicator_WithInMemoryLogger_OnlyWritesOnceWithinWindow()
    {
        // Arrange
        var clock = new TestSystemClock(new DateTime(2024, 1, 1, 12, 0, 0));
        var deduplicator = new LogDeduplicator(clock, windowDurationSeconds: 1);
        var logger = new InMemoryLogger();

        // Act - attempt to log same message 5 times within 1 second
        for (int i = 0; i < 5; i++)
        {
            if (deduplicator.ShouldLog(LogLevel.Error, "Repeated error", "TestException"))
            {
                logger.LogError("Repeated error");
                deduplicator.RecordLog(LogLevel.Error, "Repeated error", "TestException");
            }
            clock.AdvanceTime(TimeSpan.FromMilliseconds(100)); // Total 500ms
        }

        // Assert - should only have logged once
        Assert.Equal(1, logger.ErrorCount);
    }

    [Fact]
    public void LogDeduplicator_WithInMemoryLogger_WritesAgainAfterWindow()
    {
        // Arrange
        var clock = new TestSystemClock(new DateTime(2024, 1, 1, 12, 0, 0));
        var deduplicator = new LogDeduplicator(clock, windowDurationSeconds: 1);
        var logger = new InMemoryLogger();

        // First log
        if (deduplicator.ShouldLog(LogLevel.Error, "Repeated error"))
        {
            logger.LogError("Repeated error");
            deduplicator.RecordLog(LogLevel.Error, "Repeated error");
        }

        // Advance time beyond window
        clock.AdvanceTime(TimeSpan.FromSeconds(1.5));

        // Act - log again after window
        if (deduplicator.ShouldLog(LogLevel.Error, "Repeated error"))
        {
            logger.LogError("Repeated error");
            deduplicator.RecordLog(LogLevel.Error, "Repeated error");
        }

        // Assert - should have logged twice
        Assert.Equal(2, logger.ErrorCount);
    }

    [Fact]
    public void LogDeduplicator_DifferentMessages_NotDeduplicated()
    {
        // Arrange
        var clock = new TestSystemClock(new DateTime(2024, 1, 1, 12, 0, 0));
        var deduplicator = new LogDeduplicator(clock);
        var logger = new InMemoryLogger();

        // Act - log different messages
        var messages = new[] { "Error 1", "Error 2", "Error 3" };
        foreach (var message in messages)
        {
            if (deduplicator.ShouldLog(LogLevel.Error, message))
            {
                logger.LogError(message);
                deduplicator.RecordLog(LogLevel.Error, message);
            }
        }

        // Assert - all three should be logged
        Assert.Equal(3, logger.ErrorCount);
    }

    [Fact]
    public void LogDeduplicator_DifferentExceptionTypes_NotDeduplicated()
    {
        // Arrange
        var clock = new TestSystemClock(new DateTime(2024, 1, 1, 12, 0, 0));
        var deduplicator = new LogDeduplicator(clock);
        var logger = new InMemoryLogger();

        // Act - log same message but different exception types
        var exceptionTypes = new[] { "ArgumentException", "InvalidOperationException", "NullReferenceException" };
        foreach (var exType in exceptionTypes)
        {
            if (deduplicator.ShouldLog(LogLevel.Error, "Same message", exType))
            {
                logger.LogError("Same message");
                deduplicator.RecordLog(LogLevel.Error, "Same message", exType);
            }
        }

        // Assert - all three should be logged (different exception types)
        Assert.Equal(3, logger.ErrorCount);
    }

    [Fact]
    public void LogDeduplicator_ConcurrentAccess_ThreadSafe()
    {
        // Arrange
        var clock = new LocalSystemClock(); // Use real clock for concurrency test
        var deduplicator = new LogDeduplicator(clock, windowDurationSeconds: 2);
        var logger = new InMemoryLogger();
        var tasks = new List<Task>();
        
        // Act - multiple threads try to log the same message concurrently
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                for (int j = 0; j < 100; j++)
                {
                    if (deduplicator.ShouldLog(LogLevel.Error, "Concurrent error"))
                    {
                        logger.LogError("Concurrent error");
                        deduplicator.RecordLog(LogLevel.Error, "Concurrent error");
                    }
                }
            }));
        }

        Task.WaitAll(tasks.ToArray());

        // Assert - should have significantly fewer logs than 1000 due to deduplication
        // With 2 second window and threads running concurrently, we expect very few logs
        Assert.True(logger.ErrorCount < 100, 
            $"Expected fewer than 100 logs due to deduplication, but got {logger.ErrorCount}");
        Assert.True(logger.ErrorCount >= 1, 
            "Expected at least 1 log to have been written");
    }

    [Fact]
    public void LogDeduplicator_CleanupUnderHighLoad_NoExceptions()
    {
        // Arrange
        var clock = new TestSystemClock(new DateTime(2024, 1, 1, 12, 0, 0));
        var deduplicator = new LogDeduplicator(clock, windowDurationSeconds: 1);

        // Act - add over 1000 unique entries to trigger cleanup
        for (int i = 0; i < 1500; i++)
        {
            var message = $"Unique message {i}";
            if (deduplicator.ShouldLog(LogLevel.Error, message))
            {
                deduplicator.RecordLog(LogLevel.Error, message);
            }
            
            // Advance time slightly to make some entries old
            if (i % 100 == 0)
            {
                clock.AdvanceTime(TimeSpan.FromMilliseconds(200));
            }
        }

        // Assert - should complete without exceptions
        // Verify that deduplication still works after cleanup
        Assert.False(deduplicator.ShouldLog(LogLevel.Error, "Unique message 1400"));
    }

    /// <summary>
    /// Test helper for system clock with controllable time
    /// </summary>
    private class TestSystemClock : ISystemClock
    {
        private DateTime _currentTime;

        public TestSystemClock(DateTime initialTime)
        {
            _currentTime = initialTime;
        }

        public DateTime LocalNow => _currentTime;
        public DateTimeOffset LocalNowOffset => new(_currentTime);
        public DateTime UtcNow => _currentTime.ToUniversalTime();

        public void AdvanceTime(TimeSpan duration)
        {
            _currentTime = _currentTime.Add(duration);
        }
    }

    /// <summary>
    /// In-memory logger for testing log deduplication
    /// </summary>
    private class InMemoryLogger
    {
        private int _errorCount;
        private int _warningCount;
        private int _informationCount;
        private readonly object _lock = new();

        public int ErrorCount
        {
            get { lock (_lock) return _errorCount; }
        }

        public int WarningCount
        {
            get { lock (_lock) return _warningCount; }
        }

        public int InformationCount
        {
            get { lock (_lock) return _informationCount; }
        }

        public void LogError(string message)
        {
            lock (_lock)
            {
                _errorCount++;
            }
        }

        public void LogWarning(string message)
        {
            lock (_lock)
            {
                _warningCount++;
            }
        }

        public void LogInformation(string message)
        {
            lock (_lock)
            {
                _informationCount++;
            }
        }
    }
}
