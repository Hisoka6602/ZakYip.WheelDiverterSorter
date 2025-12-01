using ZakYip.WheelDiverterSorter.Core.Events.Communication;
using ZakYip.WheelDiverterSorter.Observability.Utilities;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Enums.Communication;

namespace ZakYip.WheelDiverterSorter.Communication.Tests.Models;

/// <summary>
/// Tests for EmcLockEvent
/// </summary>
public class EmcLockEventTests
{
    [Fact]
    public void EmcLockEvent_DefaultValues()
    {
        // Act
        var lockEvent = new EmcLockEvent();

        // Assert
        Assert.NotNull(lockEvent.EventId);
        Assert.NotEqual(Guid.Empty.ToString(), lockEvent.EventId);
        Assert.Equal(string.Empty, lockEvent.InstanceId);
        Assert.Equal(EmcLockNotificationType.RequestLock, lockEvent.NotificationType);
        Assert.Equal((ushort)0, lockEvent.CardNo);
        // Timestamp 不再有默认值，应该是默认DateTime值
        Assert.Equal(default(DateTime), lockEvent.Timestamp);
        Assert.Null(lockEvent.Message);
        Assert.Equal(5000, lockEvent.TimeoutMs);
    }

    [Fact]
    public void EmcLockEvent_CanSetAllProperties()
    {
        // Arrange
        var eventId = "test-event-123";
        var instanceId = "instance-1";
        var notificationType = EmcLockNotificationType.ReleaseLock;
        ushort cardNo = 5;
        var timestamp = DateTime.UtcNow;
        var message = "Test message";
        var timeoutMs = 10000;

        // Act
        var lockEvent = new EmcLockEvent
        {
            EventId = eventId,
            InstanceId = instanceId,
            NotificationType = notificationType,
            CardNo = cardNo,
            Timestamp = timestamp,
            Message = message,
            TimeoutMs = timeoutMs
        };

        // Assert
        Assert.Equal(eventId, lockEvent.EventId);
        Assert.Equal(instanceId, lockEvent.InstanceId);
        Assert.Equal(notificationType, lockEvent.NotificationType);
        Assert.Equal(cardNo, lockEvent.CardNo);
        Assert.Equal(timestamp, lockEvent.Timestamp);
        Assert.Equal(message, lockEvent.Message);
        Assert.Equal(timeoutMs, lockEvent.TimeoutMs);
    }

    [Fact]
    public void EmcLockEvent_GeneratesUniqueEventIds()
    {
        // Act
        var event1 = new EmcLockEvent();
        var event2 = new EmcLockEvent();

        // Assert
        Assert.NotEqual(event1.EventId, event2.EventId);
    }

    [Theory]
    [InlineData(EmcLockNotificationType.RequestLock)]
    [InlineData(EmcLockNotificationType.ReleaseLock)]
    [InlineData(EmcLockNotificationType.Acknowledge)]
    [InlineData(EmcLockNotificationType.Ready)]
    [InlineData(EmcLockNotificationType.ColdReset)]
    [InlineData(EmcLockNotificationType.HotReset)]
    [InlineData(EmcLockNotificationType.ResetComplete)]
    public void EmcLockEvent_SupportsAllNotificationTypes(EmcLockNotificationType notificationType)
    {
        // Act
        var lockEvent = new EmcLockEvent
        {
            NotificationType = notificationType
        };

        // Assert
        Assert.Equal(notificationType, lockEvent.NotificationType);
    }
}
