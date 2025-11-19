using Xunit;
using ZakYip.WheelDiverterSorter.Core.Sorting.Runtime;
using ZakYip.WheelDiverterSorter.Core.Sorting.Overload;

namespace ZakYip.WheelDiverterSorter.Core.Tests.ThrottleTests;

/// <summary>
/// 测试 DefaultOverloadHandlingPolicy：默认超载处置策略
/// </summary>
public class DefaultOverloadHandlingPolicyTests
{
    [Fact]
    public void Evaluate_PolicyDisabled_ReturnsContinueNormal()
    {
        // Arrange
        var config = new OverloadPolicyConfiguration
        {
            Enabled = false
        };
        var policy = new DefaultOverloadHandlingPolicy(config);
        
        var context = new OverloadContext
        {
            ParcelId = "P001",
            TargetChuteId = 10,
            CurrentCongestionLevel = CongestionLevel.Severe,
            InFlightParcels = 200,
            RemainingTtlMs = 100
        };

        // Act
        var decision = policy.Evaluate(in context);

        // Assert
        Assert.False(decision.ShouldForceException);
        Assert.False(decision.ShouldMarkAsOverflow);
    }

    [Fact]
    public void Evaluate_SevereCongestionWithForceEnabled_ReturnsForceException()
    {
        // Arrange
        var config = new OverloadPolicyConfiguration
        {
            Enabled = true,
            ForceExceptionOnSevere = true
        };
        var policy = new DefaultOverloadHandlingPolicy(config);
        
        var context = new OverloadContext
        {
            ParcelId = "P001",
            TargetChuteId = 10,
            CurrentCongestionLevel = CongestionLevel.Severe,
            InFlightParcels = 80,
            RemainingTtlMs = 5000
        };

        // Act
        var decision = policy.Evaluate(in context);

        // Assert
        Assert.True(decision.ShouldForceException);
        Assert.True(decision.ShouldMarkAsOverflow);
        Assert.Contains("严重拥堵", decision.Reason);
    }

    [Fact]
    public void Evaluate_SevereCongestionWithForceDisabled_ReturnsContinueNormal()
    {
        // Arrange
        var config = new OverloadPolicyConfiguration
        {
            Enabled = true,
            ForceExceptionOnSevere = false
        };
        var policy = new DefaultOverloadHandlingPolicy(config);
        
        var context = new OverloadContext
        {
            ParcelId = "P001",
            TargetChuteId = 10,
            CurrentCongestionLevel = CongestionLevel.Severe,
            InFlightParcels = 80,
            RemainingTtlMs = 5000
        };

        // Act
        var decision = policy.Evaluate(in context);

        // Assert
        Assert.False(decision.ShouldForceException);
    }

    [Fact]
    public void Evaluate_ExceedMaxInFlightWithForce_ReturnsForceException()
    {
        // Arrange
        var config = new OverloadPolicyConfiguration
        {
            Enabled = true,
            MaxInFlightParcels = 100,
            ForceExceptionOnOverCapacity = true
        };
        var policy = new DefaultOverloadHandlingPolicy(config);
        
        var context = new OverloadContext
        {
            ParcelId = "P001",
            TargetChuteId = 10,
            CurrentCongestionLevel = CongestionLevel.Normal,
            InFlightParcels = 120,
            RemainingTtlMs = 5000
        };

        // Act
        var decision = policy.Evaluate(in context);

        // Assert
        Assert.True(decision.ShouldForceException);
        Assert.Contains("在途包裹数超载", decision.Reason);
    }

    [Fact]
    public void Evaluate_ExceedMaxInFlightWithoutForce_ReturnsMarkOnly()
    {
        // Arrange
        var config = new OverloadPolicyConfiguration
        {
            Enabled = true,
            MaxInFlightParcels = 100,
            ForceExceptionOnOverCapacity = false
        };
        var policy = new DefaultOverloadHandlingPolicy(config);
        
        var context = new OverloadContext
        {
            ParcelId = "P001",
            TargetChuteId = 10,
            CurrentCongestionLevel = CongestionLevel.Normal,
            InFlightParcels = 120,
            RemainingTtlMs = 5000
        };

        // Act
        var decision = policy.Evaluate(in context);

        // Assert
        Assert.False(decision.ShouldForceException);
        Assert.True(decision.ShouldMarkAsOverflow);
        Assert.Contains("在途包裹数偏高", decision.Reason);
    }

    [Fact]
    public void Evaluate_InsufficientTtlWithForce_ReturnsForceException()
    {
        // Arrange
        var config = new OverloadPolicyConfiguration
        {
            Enabled = true,
            MinRequiredTtlMs = 500,
            ForceExceptionOnTimeout = true
        };
        var policy = new DefaultOverloadHandlingPolicy(config);
        
        var context = new OverloadContext
        {
            ParcelId = "P001",
            TargetChuteId = 10,
            CurrentCongestionLevel = CongestionLevel.Normal,
            InFlightParcels = 50,
            RemainingTtlMs = 300
        };

        // Act
        var decision = policy.Evaluate(in context);

        // Assert
        Assert.True(decision.ShouldForceException);
        Assert.Contains("剩余TTL不足", decision.Reason);
    }

    [Fact]
    public void Evaluate_InsufficientTtlWithoutForce_ReturnsMarkOnly()
    {
        // Arrange
        var config = new OverloadPolicyConfiguration
        {
            Enabled = true,
            MinRequiredTtlMs = 500,
            ForceExceptionOnTimeout = false
        };
        var policy = new DefaultOverloadHandlingPolicy(config);
        
        var context = new OverloadContext
        {
            ParcelId = "P001",
            TargetChuteId = 10,
            CurrentCongestionLevel = CongestionLevel.Normal,
            InFlightParcels = 50,
            RemainingTtlMs = 300
        };

        // Act
        var decision = policy.Evaluate(in context);

        // Assert
        Assert.False(decision.ShouldForceException);
        Assert.True(decision.ShouldMarkAsOverflow);
    }

    [Fact]
    public void Evaluate_InsufficientArrivalWindowWithForce_ReturnsForceException()
    {
        // Arrange
        var config = new OverloadPolicyConfiguration
        {
            Enabled = true,
            MinArrivalWindowMs = 200,
            ForceExceptionOnWindowMiss = true
        };
        var policy = new DefaultOverloadHandlingPolicy(config);
        
        var context = new OverloadContext
        {
            ParcelId = "P001",
            TargetChuteId = 10,
            CurrentCongestionLevel = CongestionLevel.Normal,
            InFlightParcels = 50,
            RemainingTtlMs = 5000,
            EstimatedArrivalWindowMs = 150
        };

        // Act
        var decision = policy.Evaluate(in context);

        // Assert
        Assert.True(decision.ShouldForceException);
        Assert.Contains("到达窗口不足", decision.Reason);
    }

    [Fact]
    public void Evaluate_AllConditionsNormal_ReturnsContinueNormal()
    {
        // Arrange
        var config = new OverloadPolicyConfiguration
        {
            Enabled = true,
            MaxInFlightParcels = 100,
            MinRequiredTtlMs = 500,
            MinArrivalWindowMs = 200,
            ForceExceptionOnSevere = true
        };
        var policy = new DefaultOverloadHandlingPolicy(config);
        
        var context = new OverloadContext
        {
            ParcelId = "P001",
            TargetChuteId = 10,
            CurrentCongestionLevel = CongestionLevel.Normal,
            InFlightParcels = 50,
            RemainingTtlMs = 3000,
            EstimatedArrivalWindowMs = 500
        };

        // Act
        var decision = policy.Evaluate(in context);

        // Assert
        Assert.False(decision.ShouldForceException);
        Assert.False(decision.ShouldMarkAsOverflow);
        Assert.Equal("正常", decision.Reason);
    }

    [Fact]
    public void Evaluate_PrioritizesSevereOverOtherConditions()
    {
        // Arrange
        var config = new OverloadPolicyConfiguration
        {
            Enabled = true,
            MaxInFlightParcels = 100,
            MinRequiredTtlMs = 500,
            ForceExceptionOnSevere = true,
            ForceExceptionOnOverCapacity = true,
            ForceExceptionOnTimeout = true
        };
        var policy = new DefaultOverloadHandlingPolicy(config);
        
        var context = new OverloadContext
        {
            ParcelId = "P001",
            TargetChuteId = 10,
            CurrentCongestionLevel = CongestionLevel.Severe,
            InFlightParcels = 120,  // Also exceeds capacity
            RemainingTtlMs = 300  // Also insufficient TTL
        };

        // Act
        var decision = policy.Evaluate(in context);

        // Assert
        Assert.True(decision.ShouldForceException);
        // Severe congestion is checked first
        Assert.Contains("严重拥堵", decision.Reason);
    }
}
