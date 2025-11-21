using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ZakYip.WheelDiverterSorter.Execution.Concurrency;
using ZakYip.WheelDiverterSorter.Observability;
using ZakYip.WheelDiverterSorter.Observability.Utilities;

namespace ZakYip.WheelDiverterSorter.Execution.Tests.Concurrency;

/// <summary>
/// Tests for MonitoredParcelQueue
/// </summary>
public class MonitoredParcelQueueTests
{
    private readonly Mock<IParcelQueue> _innerQueueMock;
    private readonly AlarmService _alarmService;
    private readonly MonitoredParcelQueue _monitoredQueue;

    public MonitoredParcelQueueTests()
    {
        _innerQueueMock = new Mock<IParcelQueue>();
        var loggerMock = new Mock<ILogger<AlarmService>>();
        _alarmService = new AlarmService(loggerMock.Object, Mock.Of<ISystemClock>());
        _monitoredQueue = new MonitoredParcelQueue(_innerQueueMock.Object, _alarmService);
    }

    [Fact]
    public void Constructor_WithNullInnerQueue_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => new MonitoredParcelQueue(null!, _alarmService);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("innerQueue");
    }

    [Fact]
    public void Constructor_WithNullAlarmService_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => new MonitoredParcelQueue(_innerQueueMock.Object, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("alarmService");
    }

    [Fact]
    public void Count_ReturnsInnerQueueCount()
    {
        // Arrange
        _innerQueueMock.Setup(q => q.Count).Returns(42);

        // Act
        var count = _monitoredQueue.Count;

        // Assert
        count.Should().Be(42);
    }

    [Fact]
    public async Task EnqueueAsync_CallsInnerQueue()
    {
        // Arrange
        var item = new ParcelQueueItem { ParcelId = "PKG001", TargetChuteId = "1", Priority = 1 };
        _innerQueueMock.Setup(q => q.Count).Returns(5);

        // Act
        await _monitoredQueue.EnqueueAsync(item);

        // Assert
        _innerQueueMock.Verify(q => q.EnqueueAsync(item, default), Times.Once);
    }

    [Fact]
    public async Task DequeueAsync_CallsInnerQueue()
    {
        // Arrange
        var item = new ParcelQueueItem { ParcelId = "PKG001", TargetChuteId = "1", Priority = 1 };
        _innerQueueMock.Setup(q => q.DequeueAsync(default)).ReturnsAsync(item);
        _innerQueueMock.Setup(q => q.Count).Returns(3);

        // Act
        var result = await _monitoredQueue.DequeueAsync();

        // Assert
        result.Should().Be(item);
        _innerQueueMock.Verify(q => q.DequeueAsync(default), Times.Once);
    }

    [Fact]
    public void TryDequeue_WhenSuccessful_ReturnsItem()
    {
        // Arrange
        var item = new ParcelQueueItem { ParcelId = "PKG001", TargetChuteId = "1", Priority = 1 };
        _innerQueueMock.Setup(q => q.TryDequeue(out It.Ref<ParcelQueueItem?>.IsAny))
            .Returns(new TryDequeueDelegate((out ParcelQueueItem? outItem) =>
            {
                outItem = item;
                return true;
            }));
        _innerQueueMock.Setup(q => q.Count).Returns(2);

        // Act
        var result = _monitoredQueue.TryDequeue(out var dequeuedItem);

        // Assert
        result.Should().BeTrue();
        dequeuedItem.Should().Be(item);
    }

    [Fact]
    public void TryDequeue_WhenUnsuccessful_ReturnsFalse()
    {
        // Arrange
        _innerQueueMock.Setup(q => q.TryDequeue(out It.Ref<ParcelQueueItem?>.IsAny))
            .Returns(new TryDequeueDelegate((out ParcelQueueItem? outItem) =>
            {
                outItem = null;
                return false;
            }));

        // Act
        var result = _monitoredQueue.TryDequeue(out var dequeuedItem);

        // Assert
        result.Should().BeFalse();
        dequeuedItem.Should().BeNull();
    }

    [Fact]
    public async Task DequeueBatchAsync_CallsInnerQueue()
    {
        // Arrange
        var items = new List<ParcelQueueItem>
        {
            new ParcelQueueItem { ParcelId = "PKG001", TargetChuteId = "1", Priority = 1 },
            new ParcelQueueItem { ParcelId = "PKG002", TargetChuteId = "2", Priority = 1 }
        };
        _innerQueueMock.Setup(q => q.DequeueBatchAsync(10, default))
            .ReturnsAsync(items);
        _innerQueueMock.Setup(q => q.Count).Returns(8);

        // Act
        var result = await _monitoredQueue.DequeueBatchAsync(10);

        // Assert
        result.Should().BeEquivalentTo(items);
        _innerQueueMock.Verify(q => q.DequeueBatchAsync(10, default), Times.Once);
    }

    // Helper delegate for TryDequeue mock setup
    private delegate bool TryDequeueDelegate(out ParcelQueueItem? item);
}
