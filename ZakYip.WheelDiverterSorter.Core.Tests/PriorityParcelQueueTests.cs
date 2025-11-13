using Xunit;
using ZakYip.WheelDiverterSorter.Execution.Concurrency;

namespace ZakYip.WheelDiverterSorter.Core.Tests;

public class PriorityParcelQueueTests
{
    [Fact]
    public void Count_InitiallyZero()
    {
        // Arrange
        var queue = new PriorityParcelQueue();

        // Assert
        Assert.Equal(0, queue.Count);
    }

    [Fact]
    public async Task EnqueueAsync_IncreasesCount()
    {
        // Arrange
        var queue = new PriorityParcelQueue();
        var item = new ParcelQueueItem
        {
            ParcelId = "P001",
            TargetChuteId = "C001"
        };

        // Act
        await queue.EnqueueAsync(item);

        // Assert
        Assert.Equal(1, queue.Count);
    }

    [Fact]
    public async Task EnqueueAsync_WithNullItem_ThrowsArgumentNullException()
    {
        // Arrange
        var queue = new PriorityParcelQueue();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => queue.EnqueueAsync(null!));
    }

    [Fact]
    public async Task DequeueAsync_DecreasesCount()
    {
        // Arrange
        var queue = new PriorityParcelQueue();
        var item = new ParcelQueueItem
        {
            ParcelId = "P001",
            TargetChuteId = "C001"
        };
        await queue.EnqueueAsync(item);

        // Act
        await queue.DequeueAsync();

        // Assert
        Assert.Equal(0, queue.Count);
    }

    [Fact]
    public async Task DequeueAsync_ReturnsCorrectItem()
    {
        // Arrange
        var queue = new PriorityParcelQueue();
        var item = new ParcelQueueItem
        {
            ParcelId = "P001",
            TargetChuteId = "C001"
        };
        await queue.EnqueueAsync(item);

        // Act
        var result = await queue.DequeueAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal("P001", result.ParcelId);
        Assert.Equal("C001", result.TargetChuteId);
    }

    [Fact]
    public void TryDequeue_WithEmptyQueue_ReturnsFalse()
    {
        // Arrange
        var queue = new PriorityParcelQueue();

        // Act
        var result = queue.TryDequeue(out var item);

        // Assert
        Assert.False(result);
        Assert.Null(item);
    }

    [Fact]
    public async Task TryDequeue_WithItems_ReturnsTrue()
    {
        // Arrange
        var queue = new PriorityParcelQueue();
        var item = new ParcelQueueItem
        {
            ParcelId = "P001",
            TargetChuteId = "C001"
        };
        await queue.EnqueueAsync(item);

        // Act
        var result = queue.TryDequeue(out var dequeuedItem);

        // Assert
        Assert.True(result);
        Assert.NotNull(dequeuedItem);
        Assert.Equal("P001", dequeuedItem.ParcelId);
    }

    [Fact]
    public async Task DequeueBatchAsync_WithMaxBatchSizeZero_ThrowsArgumentException()
    {
        // Arrange
        var queue = new PriorityParcelQueue();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => queue.DequeueBatchAsync(0));
    }

    [Fact]
    public async Task DequeueBatchAsync_WithMaxBatchSizeNegative_ThrowsArgumentException()
    {
        // Arrange
        var queue = new PriorityParcelQueue();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => queue.DequeueBatchAsync(-1));
    }

    [Fact]
    public async Task DequeueBatchAsync_WithSingleItem_ReturnsSingleItemBatch()
    {
        // Arrange
        var queue = new PriorityParcelQueue();
        var item = new ParcelQueueItem
        {
            ParcelId = "P001",
            TargetChuteId = "C001"
        };
        await queue.EnqueueAsync(item);

        // Act
        var batch = await queue.DequeueBatchAsync(5);

        // Assert
        Assert.Single(batch);
        Assert.Equal("P001", batch[0].ParcelId);
    }

    [Fact]
    public async Task DequeueBatchAsync_WithSameTarget_ReturnsAllItems()
    {
        // Arrange
        var queue = new PriorityParcelQueue();
        var items = new[]
        {
            new ParcelQueueItem { ParcelId = "P001", TargetChuteId = "C001" },
            new ParcelQueueItem { ParcelId = "P002", TargetChuteId = "C001" },
            new ParcelQueueItem { ParcelId = "P003", TargetChuteId = "C001" }
        };

        foreach (var item in items)
        {
            await queue.EnqueueAsync(item);
        }

        // Act
        var batch = await queue.DequeueBatchAsync(5);

        // Assert
        Assert.Equal(3, batch.Count);
        Assert.All(batch, item => Assert.Equal("C001", item.TargetChuteId));
    }

    [Fact]
    public async Task DequeueBatchAsync_WithDifferentTargets_ReturnsOnlyMatchingTarget()
    {
        // Arrange
        var queue = new PriorityParcelQueue();
        var items = new[]
        {
            new ParcelQueueItem { ParcelId = "P001", TargetChuteId = "C001" },
            new ParcelQueueItem { ParcelId = "P002", TargetChuteId = "C001" },
            new ParcelQueueItem { ParcelId = "P003", TargetChuteId = "C002" } // Different target
        };

        foreach (var item in items)
        {
            await queue.EnqueueAsync(item);
        }

        // Act
        var batch = await queue.DequeueBatchAsync(5);

        // Assert
        Assert.Equal(2, batch.Count);
        Assert.All(batch, item => Assert.Equal("C001", item.TargetChuteId));
        
        // The third item should still be in the queue
        Assert.Equal(1, queue.Count);
    }

    [Fact]
    public async Task DequeueBatchAsync_RespectsBatchSizeLimit()
    {
        // Arrange
        var queue = new PriorityParcelQueue();
        var items = new[]
        {
            new ParcelQueueItem { ParcelId = "P001", TargetChuteId = "C001" },
            new ParcelQueueItem { ParcelId = "P002", TargetChuteId = "C001" },
            new ParcelQueueItem { ParcelId = "P003", TargetChuteId = "C001" },
            new ParcelQueueItem { ParcelId = "P004", TargetChuteId = "C001" }
        };

        foreach (var item in items)
        {
            await queue.EnqueueAsync(item);
        }

        // Act
        var batch = await queue.DequeueBatchAsync(2);

        // Assert
        Assert.Equal(2, batch.Count);
        Assert.Equal(2, queue.Count); // Two items should remain
    }

    [Fact]
    public async Task EnqueueAndDequeue_MultipleTimes_MaintainsCorrectCount()
    {
        // Arrange
        var queue = new PriorityParcelQueue();

        // Act & Assert
        await queue.EnqueueAsync(new ParcelQueueItem { ParcelId = "P001", TargetChuteId = "C001" });
        Assert.Equal(1, queue.Count);

        await queue.EnqueueAsync(new ParcelQueueItem { ParcelId = "P002", TargetChuteId = "C001" });
        Assert.Equal(2, queue.Count);

        await queue.DequeueAsync();
        Assert.Equal(1, queue.Count);

        await queue.EnqueueAsync(new ParcelQueueItem { ParcelId = "P003", TargetChuteId = "C001" });
        Assert.Equal(2, queue.Count);

        await queue.DequeueAsync();
        await queue.DequeueAsync();
        Assert.Equal(0, queue.Count);
    }

    [Fact]
    public async Task DequeueAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var queue = new PriorityParcelQueue();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => 
            queue.DequeueAsync(cts.Token));
    }
}
