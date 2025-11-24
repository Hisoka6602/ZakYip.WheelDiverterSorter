using Xunit;
using ZakYip.WheelDiverterSorter.Core.Sorting.Models;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Sorting.Policies;

namespace ZakYip.WheelDiverterSorter.Core.Tests.ThrottleTests;

public class DefaultReleaseThrottlePolicyTests
{
    [Fact]
    public void GetReleaseIntervalMs_Normal_ReturnsNormalInterval()
    {
        var config = new ReleaseThrottleConfiguration
        {
            NormalReleaseIntervalMs = 300,
            WarningReleaseIntervalMs = 500,
            SevereReleaseIntervalMs = 1000,
            EnableThrottling = true
        };
        var policy = new DefaultReleaseThrottlePolicy(config);

        var interval = policy.GetReleaseIntervalMs(CongestionLevel.Normal);
        Assert.Equal(300, interval);
    }

    [Fact]
    public void GetReleaseIntervalMs_Warning_ReturnsWarningInterval()
    {
        var config = new ReleaseThrottleConfiguration
        {
            NormalReleaseIntervalMs = 300,
            WarningReleaseIntervalMs = 500,
            SevereReleaseIntervalMs = 1000,
            EnableThrottling = true
        };
        var policy = new DefaultReleaseThrottlePolicy(config);

        var interval = policy.GetReleaseIntervalMs(CongestionLevel.Warning);
        Assert.Equal(500, interval);
    }

    [Fact]
    public void GetReleaseIntervalMs_Severe_ReturnsSevereInterval()
    {
        var config = new ReleaseThrottleConfiguration
        {
            NormalReleaseIntervalMs = 300,
            WarningReleaseIntervalMs = 500,
            SevereReleaseIntervalMs = 1000,
            EnableThrottling = true
        };
        var policy = new DefaultReleaseThrottlePolicy(config);

        var interval = policy.GetReleaseIntervalMs(CongestionLevel.Severe);
        Assert.Equal(1000, interval);
    }

    [Fact]
    public void AllowNewParcel_SevereWithPause_ReturnsFalse()
    {
        var config = new ReleaseThrottleConfiguration
        {
            ShouldPauseOnSevere = true,
            EnableThrottling = true
        };
        var policy = new DefaultReleaseThrottlePolicy(config);

        var allowed = policy.AllowNewParcel(CongestionLevel.Severe);
        Assert.False(allowed);
    }

    [Fact]
    public void IsPaused_SevereWithPause_ReturnsTrue()
    {
        var config = new ReleaseThrottleConfiguration
        {
            ShouldPauseOnSevere = true,
            EnableThrottling = true
        };
        var policy = new DefaultReleaseThrottlePolicy(config);

        var paused = policy.IsPaused(CongestionLevel.Severe);
        Assert.True(paused);
    }
}
