using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using ZakYip.WheelDiverterSorter.Core.Events.Communication;
using ZakYip.WheelDiverterSorter.Core.Enums.Communication;

namespace ZakYip.WheelDiverterSorter.Communication.Tests.Models;

/// <summary>
/// Tests for EmcLockEventArgs
/// PR-PERF-EVENTS01: Updated to test sealed record class
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
            Timestamp = DateTimeOffset.Now,
            Message = "Test message",
            TimeoutMs = 5000
        };

        // Assert
        Assert.Equal("test-123", eventArgs.EventId);
        Assert.Equal("instance-1", eventArgs.InstanceId);
        Assert.Equal(EmcLockNotificationType.RequestLock, eventArgs.NotificationType);
        Assert.Equal((ushort)3, eventArgs.CardNo);
        Assert.Equal("Test message", eventArgs.Message);
        Assert.Equal(5000, eventArgs.TimeoutMs);
    }

    [Fact]
    public void EmcLockEventArgs_IsRecordClass()
    {
        // Arrange
        var eventArgs = new EmcLockEventArgs
        {
            EventId = "test-123",
            InstanceId = "instance-1",
            NotificationType = EmcLockNotificationType.RequestLock,
            CardNo = 1,
            Timestamp = DateTimeOffset.Now
        };

        // Assert - record class provides value equality
        var eventArgs2 = new EmcLockEventArgs
        {
            EventId = "test-123",
            InstanceId = "instance-1",
            NotificationType = EmcLockNotificationType.RequestLock,
            CardNo = 1,
            Timestamp = eventArgs.Timestamp
        };
        
        Assert.Equal(eventArgs, eventArgs2);
    }

    [Fact]
    public void EmcLockEventArgs_DefaultTimeoutMs()
    {
        // Arrange & Act
        var eventArgs = new EmcLockEventArgs
        {
            EventId = "test-123",
            InstanceId = "instance-1",
            NotificationType = EmcLockNotificationType.RequestLock,
            CardNo = 1,
            Timestamp = DateTimeOffset.Now
        };

        // Assert
        Assert.Equal(5000, eventArgs.TimeoutMs); // Default value
    }
}
