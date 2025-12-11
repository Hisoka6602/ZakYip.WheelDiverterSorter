using Xunit;
using ZakYip.WheelDiverterSorter.Core.LineModel;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Events.Sorting;
using ZakYip.WheelDiverterSorter.Core.Events.Hardware;
using ZakYip.WheelDiverterSorter.Core.Events.Chute;


using ZakYip.WheelDiverterSorter.Core.LineModel.Routing;namespace ZakYip.WheelDiverterSorter.Core.Tests;
using ZakYip.WheelDiverterSorter.Core.Enums.Sorting;

public class RoutePlanTests
{
    [Fact]
    public void Constructor_ShouldCreateRoutePlanWithCorrectInitialState()
    {
        // Arrange
        var parcelId = 12345L;
        var targetChuteId = 10;
        var createdAt = DateTimeOffset.Now;

        // Act
        var routePlan = new RoutePlan(parcelId, targetChuteId, createdAt);

        // Assert
        Assert.Equal(parcelId, routePlan.ParcelId);
        Assert.Equal(targetChuteId, routePlan.InitialTargetChuteId);
        Assert.Equal(targetChuteId, routePlan.CurrentStateargetChuteId);
        Assert.Equal(RoutePlanStatus.Created, routePlan.Status);
        Assert.Equal(createdAt, routePlan.CreatedAt);
        Assert.Equal(0, routePlan.ChuteChangeCount);
    }

    [Fact]
    public void TryApplyChuteChange_WhenCreated_ShouldAcceptChange()
    {
        // Arrange
        var routePlan = new RoutePlan(12345L, 10, DateTimeOffset.Now);
        var requestedChuteId = 20;
        var requestedAt = DateTimeOffset.Now;

        // Act
        var result = routePlan.TryApplyChuteChange(requestedChuteId, requestedAt, out var decision);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(ChuteChangeOutcome.Accepted, decision.Outcome);
        Assert.Equal(requestedChuteId, decision.AppliedChuteId);
        Assert.Equal(requestedChuteId, routePlan.CurrentStateargetChuteId);
        Assert.Equal(1, routePlan.ChuteChangeCount);
    }

    [Fact]
    public void TryApplyChuteChange_WhenExecuting_ShouldAcceptChange()
    {
        // Arrange
        var routePlan = new RoutePlan(12345L, 10, DateTimeOffset.Now);
        routePlan.MarkAsExecuting(DateTimeOffset.Now);
        var requestedChuteId = 20;
        var requestedAt = DateTimeOffset.Now;

        // Act
        var result = routePlan.TryApplyChuteChange(requestedChuteId, requestedAt, out var decision);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(ChuteChangeOutcome.Accepted, decision.Outcome);
        Assert.Equal(requestedChuteId, routePlan.CurrentStateargetChuteId);
    }

    [Fact]
    public void TryApplyChuteChange_WhenCompleted_ShouldIgnoreChange()
    {
        // Arrange
        var routePlan = new RoutePlan(12345L, 10, DateTimeOffset.Now);
        routePlan.MarkAsCompleted(DateTimeOffset.Now);
        var originalChuteId = routePlan.CurrentStateargetChuteId;
        var requestedChuteId = 20;
        var requestedAt = DateTimeOffset.Now;

        // Act
        var result = routePlan.TryApplyChuteChange(requestedChuteId, requestedAt, out var decision);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ChuteChangeOutcome.IgnoredAlreadyCompleted, decision.Outcome);
        Assert.Equal(originalChuteId, decision.AppliedChuteId);
        Assert.Equal(originalChuteId, routePlan.CurrentStateargetChuteId);
        Assert.Equal(0, routePlan.ChuteChangeCount);
    }

    [Fact]
    public void TryApplyChuteChange_WhenExceptionRouted_ShouldIgnoreChange()
    {
        // Arrange
        var routePlan = new RoutePlan(12345L, 10, DateTimeOffset.Now);
        routePlan.MarkAsExceptionRouted(DateTimeOffset.Now);
        var originalChuteId = routePlan.CurrentStateargetChuteId;
        var requestedChuteId = 20;
        var requestedAt = DateTimeOffset.Now;

        // Act
        var result = routePlan.TryApplyChuteChange(requestedChuteId, requestedAt, out var decision);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ChuteChangeOutcome.IgnoredExceptionRouted, decision.Outcome);
        Assert.Equal(originalChuteId, decision.AppliedChuteId);
        Assert.Equal(originalChuteId, routePlan.CurrentStateargetChuteId);
        Assert.Equal(0, routePlan.ChuteChangeCount);
    }

    [Fact]
    public void TryApplyChuteChange_WhenFailed_ShouldRejectChange()
    {
        // Arrange
        var routePlan = new RoutePlan(12345L, 10, DateTimeOffset.Now);
        routePlan.MarkAsFailed(DateTimeOffset.Now);
        var requestedChuteId = 20;
        var requestedAt = DateTimeOffset.Now;

        // Act
        var result = routePlan.TryApplyChuteChange(requestedChuteId, requestedAt, out var decision);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ChuteChangeOutcome.RejectedInvalidState, decision.Outcome);
        Assert.Equal(10, decision.AppliedChuteId);
        Assert.Equal(10, routePlan.CurrentStateargetChuteId); // Original chute
        Assert.Equal(0, routePlan.ChuteChangeCount);
    }

    [Fact]
    public void TryApplyChuteChange_WhenAfterDeadline_ShouldRejectChange()
    {
        // Arrange
        var createdAt = DateTimeOffset.Now;
        var deadline = createdAt.AddSeconds(10);
        var routePlan = new RoutePlan(12345L, 10, createdAt, deadline);
        var requestedChuteId = 20;
        var requestedAt = deadline.AddSeconds(5); // After deadline

        // Act
        var result = routePlan.TryApplyChuteChange(requestedChuteId, requestedAt, out var decision);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ChuteChangeOutcome.RejectedTooLate, decision.Outcome);
        Assert.Equal(10, decision.AppliedChuteId);
        Assert.Equal(10, routePlan.CurrentStateargetChuteId);
        Assert.Equal(0, routePlan.ChuteChangeCount);
    }

    [Fact]
    public void TryApplyChuteChange_WhenBeforeDeadline_ShouldAcceptChange()
    {
        // Arrange
        var createdAt = DateTimeOffset.Now;
        var deadline = createdAt.AddSeconds(10);
        var routePlan = new RoutePlan(12345L, 10, createdAt, deadline);
        var requestedChuteId = 20;
        var requestedAt = createdAt.AddSeconds(5); // Before deadline

        // Act
        var result = routePlan.TryApplyChuteChange(requestedChuteId, requestedAt, out var decision);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(ChuteChangeOutcome.Accepted, decision.Outcome);
        Assert.Equal(requestedChuteId, decision.AppliedChuteId);
        Assert.Equal(requestedChuteId, routePlan.CurrentStateargetChuteId);
        Assert.Equal(1, routePlan.ChuteChangeCount);
    }

    [Fact]
    public void TryApplyChuteChange_ShouldRaiseDomainEvents()
    {
        // Arrange
        var routePlan = new RoutePlan(12345L, 10, DateTimeOffset.Now);
        var requestedChuteId = 20;
        var requestedAt = DateTimeOffset.Now;

        // Act
        routePlan.TryApplyChuteChange(requestedChuteId, requestedAt, out _);

        // Assert
        Assert.Equal(2, routePlan.DomainEvents.Count);
        Assert.Contains(routePlan.DomainEvents, e => e is ChuteChangeRequestedEventArgs);
        Assert.Contains(routePlan.DomainEvents, e => e is ChuteChangeAcceptedEventArgs);
    }

    [Fact]
    public void TryApplyChuteChange_WhenIgnored_ShouldRaiseIgnoredEvent()
    {
        // Arrange
        var routePlan = new RoutePlan(12345L, 10, DateTimeOffset.Now);
        routePlan.MarkAsCompleted(DateTimeOffset.Now);
        routePlan.ClearDomainEvents();
        var requestedChuteId = 20;
        var requestedAt = DateTimeOffset.Now;

        // Act
        routePlan.TryApplyChuteChange(requestedChuteId, requestedAt, out _);

        // Assert
        Assert.Equal(2, routePlan.DomainEvents.Count);
        Assert.Contains(routePlan.DomainEvents, e => e is ChuteChangeRequestedEventArgs);
        Assert.Contains(routePlan.DomainEvents, e => e is ChuteChangeIgnoredEventArgs);
    }

    [Fact]
    public void MultipleChanges_ShouldIncrementCountCorrectly()
    {
        // Arrange
        var routePlan = new RoutePlan(12345L, 10, DateTimeOffset.Now);

        // Act
        routePlan.TryApplyChuteChange(20, DateTimeOffset.Now, out _);
        routePlan.TryApplyChuteChange(30, DateTimeOffset.Now.AddSeconds(1), out _);
        routePlan.TryApplyChuteChange(40, DateTimeOffset.Now.AddSeconds(2), out _);

        // Assert
        Assert.Equal(3, routePlan.ChuteChangeCount);
        Assert.Equal(40, routePlan.CurrentStateargetChuteId);
    }
}
