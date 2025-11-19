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
}
