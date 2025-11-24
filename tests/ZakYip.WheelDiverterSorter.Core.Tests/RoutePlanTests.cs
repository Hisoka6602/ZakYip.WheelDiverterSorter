using Xunit;
using ZakYip.WheelDiverterSorter.Core.LineModel;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.LineModel.Events;


using ZakYip.WheelDiverterSorter.Core.LineModel.Routing;namespace ZakYip.WheelDiverterSorter.Core.Tests;

public class RoutePlanTests
{
    [Fact]
    public void Constructor_ShouldCreateRoutePlanWithCorrectInitialState()
    {
        // Arrange
        var parcelId = 12345L;
        var targetChuteId = 10;
        var createdAt = DateTimeOffset.UtcNow;

        // Act
        var routePlan = new RoutePlan(parcelId, targetChuteId, createdAt);

        // Assert
        Assert.Equal(parcelId, routePlan.ParcelId);
        Assert.Equal(targetChuteId, routePlan.InitialTargetChuteId);
        Assert.Equal(targetChuteId, routePlan.CurrentTargetChuteId);
        Assert.Equal(RoutePlanStatus.Created, routePlan.Status);
        Assert.Equal(createdAt, routePlan.CreatedAt);
        Assert.Equal(0, routePlan.ChuteChangeCount);
    }

    [Fact]
    public void TryApplyChuteChange_WhenCreated_ShouldAcceptChange()
    {
        // Arrange
        var routePlan = new RoutePlan(12345L, 10, DateTimeOffset.UtcNow);
        var requestedChuteId = 20;
        var requestedAt = DateTimeOffset.UtcNow;

        // Act
        var result = routePlan.TryApplyChuteChange(requestedChuteId, requestedAt, out var decision);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(ChuteChangeOutcome.Accepted, decision.Outcome);
        Assert.Equal(requestedChuteId, decision.AppliedChuteId);
        Assert.Equal(requestedChuteId, routePlan.CurrentTargetChuteId);
        Assert.Equal(1, routePlan.ChuteChangeCount);
    }

    [Fact]
    public void TryApplyChuteChange_WhenExecuting_ShouldAcceptChange()
    {
        // Arrange
        var routePlan = new RoutePlan(12345L, 10, DateTimeOffset.UtcNow);
        routePlan.MarkAsExecuting(DateTimeOffset.UtcNow);
        var requestedChuteId = 20;
        var requestedAt = DateTimeOffset.UtcNow;

        // Act
        var result = routePlan.TryApplyChuteChange(requestedChuteId, requestedAt, out var decision);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(ChuteChangeOutcome.Accepted, decision.Outcome);
        Assert.Equal(requestedChuteId, routePlan.CurrentTargetChuteId);
    }

    [Fact]
    public void TryApplyChuteChange_WhenCompleted_ShouldIgnoreChange()
    {
        // Arrange
        var routePlan = new RoutePlan(12345L, 10, DateTimeOffset.UtcNow);
        routePlan.MarkAsCompleted(DateTimeOffset.UtcNow);
        var originalChuteId = routePlan.CurrentTargetChuteId;
        var requestedChuteId = 20;
        var requestedAt = DateTimeOffset.UtcNow;

        // Act
        var result = routePlan.TryApplyChuteChange(requestedChuteId, requestedAt, out var decision);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ChuteChangeOutcome.IgnoredAlreadyCompleted, decision.Outcome);
        Assert.Equal(originalChuteId, decision.AppliedChuteId);
        Assert.Equal(originalChuteId, routePlan.CurrentTargetChuteId);
        Assert.Equal(0, routePlan.ChuteChangeCount);
    }

    [Fact]
    public void TryApplyChuteChange_WhenExceptionRouted_ShouldIgnoreChange()
    {
        // Arrange
        var routePlan = new RoutePlan(12345L, 10, DateTimeOffset.UtcNow);
        routePlan.MarkAsExceptionRouted(DateTimeOffset.UtcNow);
        var originalChuteId = routePlan.CurrentTargetChuteId;
        var requestedChuteId = 20;
        var requestedAt = DateTimeOffset.UtcNow;

        // Act
        var result = routePlan.TryApplyChuteChange(requestedChuteId, requestedAt, out var decision);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ChuteChangeOutcome.IgnoredExceptionRouted, decision.Outcome);
        Assert.Equal(originalChuteId, decision.AppliedChuteId);
        Assert.Equal(originalChuteId, routePlan.CurrentTargetChuteId);
        Assert.Equal(0, routePlan.ChuteChangeCount);
    }

    [Fact]
    public void TryApplyChuteChange_WhenFailed_ShouldRejectChange()
    {
        // Arrange
        var routePlan = new RoutePlan(12345L, 10, DateTimeOffset.UtcNow);
        routePlan.MarkAsFailed(DateTimeOffset.UtcNow);
        var requestedChuteId = 20;
        var requestedAt = DateTimeOffset.UtcNow;

        // Act
        var result = routePlan.TryApplyChuteChange(requestedChuteId, requestedAt, out var decision);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ChuteChangeOutcome.RejectedInvalidState, decision.Outcome);
        Assert.Equal(10, decision.AppliedChuteId);
        Assert.Equal(10, routePlan.CurrentTargetChuteId); // Original chute
        Assert.Equal(0, routePlan.ChuteChangeCount);
    }

    [Fact]
    public void TryApplyChuteChange_WhenAfterDeadline_ShouldRejectChange()
    {
        // Arrange
        var createdAt = DateTimeOffset.UtcNow;
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
        Assert.Equal(10, routePlan.CurrentTargetChuteId);
        Assert.Equal(0, routePlan.ChuteChangeCount);
    }

    [Fact]
    public void TryApplyChuteChange_WhenBeforeDeadline_ShouldAcceptChange()
    {
        // Arrange
        var createdAt = DateTimeOffset.UtcNow;
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
        Assert.Equal(requestedChuteId, routePlan.CurrentTargetChuteId);
        Assert.Equal(1, routePlan.ChuteChangeCount);
    }

    [Fact]
    public void TryApplyChuteChange_ShouldRaiseDomainEvents()
    {
        // Arrange
        var routePlan = new RoutePlan(12345L, 10, DateTimeOffset.UtcNow);
        var requestedChuteId = 20;
        var requestedAt = DateTimeOffset.UtcNow;

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
        var routePlan = new RoutePlan(12345L, 10, DateTimeOffset.UtcNow);
        routePlan.MarkAsCompleted(DateTimeOffset.UtcNow);
        routePlan.ClearDomainEvents();
        var requestedChuteId = 20;
        var requestedAt = DateTimeOffset.UtcNow;

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
        var routePlan = new RoutePlan(12345L, 10, DateTimeOffset.UtcNow);

        // Act
        routePlan.TryApplyChuteChange(20, DateTimeOffset.UtcNow, out _);
        routePlan.TryApplyChuteChange(30, DateTimeOffset.UtcNow.AddSeconds(1), out _);
        routePlan.TryApplyChuteChange(40, DateTimeOffset.UtcNow.AddSeconds(2), out _);

        // Assert
        Assert.Equal(3, routePlan.ChuteChangeCount);
        Assert.Equal(40, routePlan.CurrentTargetChuteId);
    }
}
