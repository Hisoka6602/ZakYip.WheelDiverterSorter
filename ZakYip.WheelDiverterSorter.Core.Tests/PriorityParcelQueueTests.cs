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

    [Fact]
    public async Task EnqueueAsync_ConcurrentAccess_MaintainsCorrectCount()
    {
        // Arrange
        var queue = new PriorityParcelQueue();
        var itemCount = 100;

        // Act - enqueue items concurrently
        var tasks = Enumerable.Range(1, itemCount).Select(i => 
            queue.EnqueueAsync(new ParcelQueueItem 
            { 
                ParcelId = $"P{i:D3}", 
                TargetChuteId = $"C{i % 10:D3}" 
            })
        );

        await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(itemCount, queue.Count);
    }

    [Fact]
    public async Task DequeueAsync_ConcurrentAccess_NoDuplicates()
    {
        // Arrange
        var queue = new PriorityParcelQueue();
        var itemCount = 50;
        var dequeuedItems = new System.Collections.Concurrent.ConcurrentBag<string>();

        // Enqueue items
        for (int i = 1; i <= itemCount; i++)
        {
            await queue.EnqueueAsync(new ParcelQueueItem 
            { 
                ParcelId = $"P{i:D3}", 
                TargetChuteId = "C001" 
            });
        }

        // Act - dequeue items concurrently
        var tasks = Enumerable.Range(1, itemCount).Select(async i =>
        {
            var item = await queue.DequeueAsync();
            dequeuedItems.Add(item.ParcelId);
        });

        await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(itemCount, dequeuedItems.Count);
        Assert.Equal(itemCount, dequeuedItems.Distinct().Count()); // No duplicates
        Assert.Equal(0, queue.Count);
    }

    [Fact]
    public async Task TryDequeue_ConcurrentAccess_ThreadSafe()
    {
        // Arrange
        var queue = new PriorityParcelQueue();
        var itemCount = 50;
        var successCount = 0;

        // Enqueue items
        for (int i = 1; i <= itemCount; i++)
        {
            await queue.EnqueueAsync(new ParcelQueueItem 
            { 
                ParcelId = $"P{i:D3}", 
                TargetChuteId = "C001" 
            });
        }

        // Act - try dequeue concurrently
        var tasks = Enumerable.Range(1, itemCount * 2).Select(i => Task.Run(() =>
        {
            if (queue.TryDequeue(out var item))
            {
                Interlocked.Increment(ref successCount);
            }
        }));

        await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(itemCount, successCount);
        Assert.Equal(0, queue.Count);
    }

    [Fact]
    public async Task DequeueBatchAsync_ConcurrentCalls_NoDuplicates()
    {
        // Arrange
        var queue = new PriorityParcelQueue();
        var itemCount = 100;
        var dequeuedIds = new System.Collections.Concurrent.ConcurrentBag<string>();

        // Enqueue items with same target
        for (int i = 1; i <= itemCount; i++)
        {
            await queue.EnqueueAsync(new ParcelQueueItem 
            { 
                ParcelId = $"P{i:D3}", 
                TargetChuteId = "C001" 
            });
        }

        // Act - dequeue batches concurrently
        var tasks = Enumerable.Range(1, 10).Select(async _ =>
        {
            try
            {
                var batch = await queue.DequeueBatchAsync(10, new CancellationTokenSource(1000).Token);
                foreach (var item in batch)
                {
                    dequeuedIds.Add(item.ParcelId);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when queue is empty
            }
        });

        await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(itemCount, dequeuedIds.Count);
        Assert.Equal(itemCount, dequeuedIds.Distinct().Count()); // No duplicates
        Assert.Equal(0, queue.Count);
    }

    [Fact]
    public async Task Queue_WithQueueCapacity_RespectsBounds()
    {
        // Arrange
        var queue = new PriorityParcelQueue(capacity: 5);
        
        // Act - enqueue items up to capacity
        for (int i = 1; i <= 5; i++)
        {
            await queue.EnqueueAsync(new ParcelQueueItem 
            { 
                ParcelId = $"P{i:D3}", 
                TargetChuteId = "C001" 
            });
        }

        // Assert
        Assert.Equal(5, queue.Count);
    }

    [Fact]
    public async Task DequeueBatchAsync_WithMixedTargets_SeparatesBatches()
    {
        // Arrange
        var queue = new PriorityParcelQueue();
        
        // Enqueue items with alternating targets
        for (int i = 1; i <= 10; i++)
        {
            await queue.EnqueueAsync(new ParcelQueueItem 
            { 
                ParcelId = $"P{i:D3}", 
                TargetChuteId = i % 2 == 0 ? "C001" : "C002" 
            });
        }

        // Act - dequeue first batch
        var batch1 = await queue.DequeueBatchAsync(10);
        
        // Assert - all items in batch1 should have same target
        Assert.NotEmpty(batch1);
        var firstTarget = batch1[0].TargetChuteId;
        Assert.All(batch1, item => Assert.Equal(firstTarget, item.TargetChuteId));
        
        // Items with different target should still be in queue
        Assert.True(queue.Count > 0);
    }

    [Fact]
    public async Task EnqueueDequeue_HighThroughput_MaintainsIntegrity()
    {
        // Arrange
        var queue = new PriorityParcelQueue();
        var enqueueCount = 0;
        var dequeueCount = 0;
        var totalItems = 200;
        
        // Act - concurrent enqueue and dequeue
        var enqueueTask = Task.Run(async () =>
        {
            for (int i = 1; i <= totalItems; i++)
            {
                await queue.EnqueueAsync(new ParcelQueueItem 
                { 
                    ParcelId = $"P{i:D3}", 
                    TargetChuteId = $"C{i % 5:D3}" 
                });
                Interlocked.Increment(ref enqueueCount);
                await Task.Delay(1);
            }
        });

        var dequeueTask = Task.Run(async () =>
        {
            while (dequeueCount < totalItems)
            {
                try
                {
                    var item = await queue.DequeueAsync(new CancellationTokenSource(100).Token);
                    Interlocked.Increment(ref dequeueCount);
                }
                catch (OperationCanceledException)
                {
                    // Queue might be temporarily empty
                    await Task.Delay(5);
                }
            }
        });

        await Task.WhenAll(enqueueTask, dequeueTask);

        // Assert
        Assert.Equal(totalItems, enqueueCount);
        Assert.Equal(totalItems, dequeueCount);
        Assert.Equal(0, queue.Count);
    }
}
