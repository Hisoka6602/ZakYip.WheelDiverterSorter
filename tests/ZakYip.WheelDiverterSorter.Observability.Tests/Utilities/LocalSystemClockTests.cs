using Xunit;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Observability;
using ZakYip.WheelDiverterSorter.Observability.Utilities;

namespace ZakYip.WheelDiverterSorter.Observability.Tests.Utilities;

/// <summary>
/// Tests for LocalSystemClock - PR-37
/// </summary>
public class LocalSystemClockTests
{
    [Fact]
    public void LocalNow_ReturnsCurrentLocalTime()
    {
        // Arrange
        var clock = new LocalSystemClock();
        var before = DateTime.Now;

        // Act
        var localNow = clock.LocalNow;
        var after = DateTime.Now;

        // Assert
        Assert.InRange(localNow, before.AddSeconds(-1), after.AddSeconds(1));
        Assert.Equal(DateTimeKind.Local, localNow.Kind);
    }

    [Fact]
    public void LocalNowOffset_ReturnsCurrentLocalTimeAsOffset()
    {
        // Arrange
        var clock = new LocalSystemClock();
        var before = DateTimeOffset.Now;

        // Act
        var localNowOffset = clock.LocalNowOffset;
        var after = DateTimeOffset.Now;

        // Assert
        Assert.InRange(localNowOffset, before.AddSeconds(-1), after.AddSeconds(1));
        Assert.Equal(TimeZoneInfo.Local.GetUtcOffset(DateTime.Now), localNowOffset.Offset);
    }

    [Fact]
    public void TestSystemClock_CanReplaceLocalSystemClock()
    {
        // Arrange - create a test clock implementation
        var testClock = new TestSystemClock(new DateTime(2024, 6, 15, 14, 30, 0));

        // Act
        var localNow = testClock.LocalNow;
        var localNowOffset = testClock.LocalNowOffset;

        // Assert - verify the test clock returns the expected fixed time
        Assert.Equal(new DateTime(2024, 6, 15, 14, 30, 0), localNow);
        Assert.Equal(new DateTime(2024, 6, 15, 14, 30, 0), localNowOffset.DateTime);
        
        // Advance time and verify it works
        testClock.AdvanceTime(TimeSpan.FromHours(2));
        Assert.Equal(new DateTime(2024, 6, 15, 16, 30, 0), testClock.LocalNow);
    }

    [Fact]
    public void LocalSystemClock_ReturnsTimeInSystemTimeZone()
    {
        // Arrange
        var clock = new LocalSystemClock();

        // Act
        var localNow = clock.LocalNow;
        var localNowOffset = clock.LocalNowOffset;
        var systemOffset = TimeZoneInfo.Local.GetUtcOffset(DateTime.Now);

        // Assert - verify the offset matches the system time zone
        Assert.Equal(systemOffset, localNowOffset.Offset);
        
        // Verify that LocalNow is actually local time (not UTC)
        Assert.Equal(DateTimeKind.Local, localNow.Kind);
    }

    [Fact]
    public void TestSystemClock_SupportsTimeTravel()
    {
        // Arrange
        var testClock = new TestSystemClock(new DateTime(2024, 1, 1, 0, 0, 0));
        var times = new List<DateTime>();

        // Act - record times as we advance
        for (int i = 0; i < 5; i++)
        {
            times.Add(testClock.LocalNow);
            testClock.AdvanceTime(TimeSpan.FromMinutes(10));
        }

        // Assert - verify time advanced as expected
        Assert.Equal(new DateTime(2024, 1, 1, 0, 0, 0), times[0]);
        Assert.Equal(new DateTime(2024, 1, 1, 0, 10, 0), times[1]);
        Assert.Equal(new DateTime(2024, 1, 1, 0, 20, 0), times[2]);
        Assert.Equal(new DateTime(2024, 1, 1, 0, 30, 0), times[3]);
        Assert.Equal(new DateTime(2024, 1, 1, 0, 40, 0), times[4]);
        Assert.Equal(new DateTime(2024, 1, 1, 0, 50, 0), testClock.LocalNow);
    }

    /// <summary>
    /// Test helper for system clock with controllable time
    /// Demonstrates that ISystemClock abstraction allows for test implementations
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

        public void AdvanceTime(TimeSpan duration)
        {
            _currentTime = _currentTime.Add(duration);
        }
    }
}
