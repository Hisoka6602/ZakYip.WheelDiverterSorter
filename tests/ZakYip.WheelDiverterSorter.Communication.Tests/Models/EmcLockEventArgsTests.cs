using ZakYip.WheelDiverterSorter.Communication.Models;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Enums.Communication;

namespace ZakYip.WheelDiverterSorter.Communication.Tests.Models;

/// <summary>
/// Tests for EmcLockEventArgs
/// </summary>
public class EmcLockEventArgsTests
{
    [Fact]
    public void EmcLockEventArgs_Constructor()
    {
        // Arrange
        var lockEvent = new EmcLockEvent
        {
            EventId = "test-123",
            InstanceId = "instance-1",
            NotificationType = EmcLockNotificationType.RequestLock,
            CardNo = 3
        };

        // Act
        var eventArgs = new EmcLockEventArgs(lockEvent);

        // Assert
        Assert.NotNull(eventArgs.LockEvent);
        Assert.Equal(lockEvent, eventArgs.LockEvent);
        Assert.Equal("test-123", eventArgs.LockEvent.EventId);
        Assert.Equal("instance-1", eventArgs.LockEvent.InstanceId);
    }

    [Fact]
    public void EmcLockEventArgs_InheritsFromEventArgs()
    {
        // Arrange
        var lockEvent = new EmcLockEvent();
        var eventArgs = new EmcLockEventArgs(lockEvent);

        // Assert
        Assert.IsAssignableFrom<EventArgs>(eventArgs);
    }
}
