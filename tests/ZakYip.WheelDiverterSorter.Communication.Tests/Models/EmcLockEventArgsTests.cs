using ZakYip.WheelDiverterSorter.Core.Events.Communication;
using ZakYip.WheelDiverterSorter.Core.Enums.Communication;

namespace ZakYip.WheelDiverterSorter.Communication.Tests.Models;

/// <summary>
/// Tests for EmcLockEventArgs
/// </summary>
public class EmcLockEventArgsTests
{
    [Fact]
    public void EmcLockEventArgs_Properties()
    {
        // Arrange & Act
        var eventArgs = new EmcLockEventArgs
        {
            EventId = "test-123",
            InstanceId = "instance-1",
            NotificationType = EmcLockNotificationType.RequestLock,
            CardNo = 3,
            Timestamp = DateTime.Now,
            Message = "Test message",
            TimeoutMs = 5000
        };

        // Assert
        Assert.Equal("test-123", eventArgs.EventId);
        Assert.Equal("instance-1", eventArgs.InstanceId);
        Assert.Equal(EmcLockNotificationType.RequestLock, eventArgs.NotificationType);
        Assert.Equal(3, eventArgs.CardNo);
    }

    [Fact]
    public void EmcLockEventArgs_InheritsFromEventArgs()
    {
        // Arrange
        var eventArgs = new EmcLockEventArgs();

        // Assert
        Assert.IsAssignableFrom<EventArgs>(eventArgs);
    }

    [Fact]
    public void EmcLockEventArgs_DefaultValues()
    {
        // Arrange & Act
        var eventArgs = new EmcLockEventArgs();

        // Assert
        Assert.NotNull(eventArgs.EventId);
        Assert.Equal(string.Empty, eventArgs.InstanceId);
        Assert.Equal(5000, eventArgs.TimeoutMs);
    }
}
