using FluentAssertions;
using Xunit;
using ZakYip.WheelDiverterSorter.Execution.Concurrency;

namespace ZakYip.WheelDiverterSorter.Execution.Tests.Concurrency;

/// <summary>
/// Tests for PriorityParcelQueue
/// </summary>
public class PriorityParcelQueueTests
{
    [Fact]
    public void Constructor_WithDefaultCapacity_CreatesQueue()
    {
        // Act
        var queue = new PriorityParcelQueue();

        // Assert
        queue.Should().NotBeNull();
        queue.Count.Should().Be(0);
    }

    [Fact]
    public void Constructor_WithSpecificCapacity_CreatesQueue()
    {
        // Act
        var queue = new PriorityParcelQueue(100);

        // Assert
        queue.Should().NotBeNull();
        queue.Count.Should().Be(0);
    }

    [Fact]
    public async Task EnqueueAsync_WithValidItem_IncrementsCount()
    {
        // Arrange
        var queue = new PriorityParcelQueue();
        var item = new ParcelQueueItem { ParcelId = "PKG001", TargetChuteId = "1", Priority = 1 };

        // Act
        await queue.EnqueueAsync(item);

        // Assert
        queue.Count.Should().Be(1);
    }

    [Fact]
    public async Task EnqueueAsync_WithNullItem_ThrowsArgumentNullException()
    {
        // Arrange
        var queue = new PriorityParcelQueue();

        // Act
        Func<Task> act = async () => await queue.EnqueueAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task DequeueAsync_WithEnqueuedItem_ReturnsItem()
    {
        // Arrange
        var queue = new PriorityParcelQueue();
        var item = new ParcelQueueItem { ParcelId = "PKG001", TargetChuteId = "1", Priority = 1 };
        await queue.EnqueueAsync(item);

        // Act
        var dequeued = await queue.DequeueAsync();

        // Assert
        dequeued.Should().Be(item);
        queue.Count.Should().Be(0);
    }

    [Fact]
    public async Task EnqueueDequeue_MultipleItems_PreservesOrder()
    {
        // Arrange
        var queue = new PriorityParcelQueue();
        var item1 = new ParcelQueueItem { ParcelId = "PKG001", TargetChuteId = "1", Priority = 1 };
        var item2 = new ParcelQueueItem { ParcelId = "PKG002", TargetChuteId = "2", Priority = 1 };
        var item3 = new ParcelQueueItem { ParcelId = "PKG003", TargetChuteId = "3", Priority = 1 };

        // Act
        await queue.EnqueueAsync(item1);
        await queue.EnqueueAsync(item2);
        await queue.EnqueueAsync(item3);

        var dequeued1 = await queue.DequeueAsync();
        var dequeued2 = await queue.DequeueAsync();
        var dequeued3 = await queue.DequeueAsync();

        // Assert
        dequeued1.Should().Be(item1);
        dequeued2.Should().Be(item2);
        dequeued3.Should().Be(item3);
        queue.Count.Should().Be(0);
    }

    [Fact]
    public async Task Count_TracksQueueSize()
    {
        // Arrange
        var queue = new PriorityParcelQueue();
        var item1 = new ParcelQueueItem { ParcelId = "PKG001", TargetChuteId = "1", Priority = 1 };
        var item2 = new ParcelQueueItem { ParcelId = "PKG002", TargetChuteId = "2", Priority = 1 };

        // Act & Assert
        queue.Count.Should().Be(0);
        
        await queue.EnqueueAsync(item1);
        queue.Count.Should().Be(1);
        
        await queue.EnqueueAsync(item2);
        queue.Count.Should().Be(2);
        
        await queue.DequeueAsync();
        queue.Count.Should().Be(1);
        
        await queue.DequeueAsync();
        queue.Count.Should().Be(0);
    }

    [Fact]
    public async Task DequeueAsync_WithCancellationToken_CanBeCancelled()
    {
        // Arrange
        var queue = new PriorityParcelQueue();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        Func<Task> act = async () => await queue.DequeueAsync(cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task EnqueueAsync_ConcurrentOperations_ThreadSafe()
    {
        // Arrange
        var queue = new PriorityParcelQueue();
        const int itemCount = 100;
        var tasks = new List<Task>();

        // Act
        for (int i = 0; i < itemCount; i++)
        {
            int index = i;
            tasks.Add(Task.Run(async () =>
            {
                var item = new ParcelQueueItem { ParcelId = $"PKG{index:000}", TargetChuteId = index.ToString(), Priority = 1 };
                await queue.EnqueueAsync(item);
            }));
        }
        await Task.WhenAll(tasks);

        // Assert
        queue.Count.Should().Be(itemCount);
    }

    [Fact]
    public async Task DequeueAsync_ConcurrentOperations_ThreadSafe()
    {
        // Arrange
        var queue = new PriorityParcelQueue();
        const int itemCount = 50;
        
        for (int i = 0; i < itemCount; i++)
        {
            var item = new ParcelQueueItem { ParcelId = $"PKG{i:000}", TargetChuteId = i.ToString(), Priority = 1 };
            await queue.EnqueueAsync(item);
        }

        var tasks = new List<Task<ParcelQueueItem>>();

        // Act
        for (int i = 0; i < itemCount; i++)
        {
            tasks.Add(Task.Run(() => queue.DequeueAsync()));
        }
        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().HaveCount(itemCount);
        results.Should().OnlyHaveUniqueItems();
        queue.Count.Should().Be(0);
    }

    // Note: TryPeekAsync is not implemented in PriorityParcelQueue, removing these tests
}
