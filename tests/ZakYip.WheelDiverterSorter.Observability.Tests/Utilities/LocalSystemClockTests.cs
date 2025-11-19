using Xunit;
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
    public void UtcNow_ReturnsCurrentUtcTime()
    {
        // Arrange
        var clock = new LocalSystemClock();
        var before = DateTime.UtcNow;

        // Act
        var utcNow = clock.UtcNow;
        var after = DateTime.UtcNow;

        // Assert
        Assert.InRange(utcNow, before.AddSeconds(-1), after.AddSeconds(1));
        Assert.Equal(DateTimeKind.Utc, utcNow.Kind);
    }

    [Fact]
    public void LocalNow_And_UtcNow_AreConsistent()
    {
        // Arrange
        var clock = new LocalSystemClock();

        // Act
        var localNow = clock.LocalNow;
        var utcNow = clock.UtcNow;

        // Assert - convert to UTC and compare
        var localAsUtc = localNow.ToUniversalTime();
        var diff = Math.Abs((localAsUtc - utcNow).TotalSeconds);
        Assert.True(diff < 1, "LocalNow and UtcNow should represent the same moment in time");
    }
}
