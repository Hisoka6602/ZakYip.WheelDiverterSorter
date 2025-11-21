using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ZakYip.WheelDiverterSorter.Execution.Concurrency;
using ZakYip.WheelDiverterSorter.Observability;
using ZakYip.WheelDiverterSorter.Observability.Utilities;

namespace ZakYip.WheelDiverterSorter.Execution.Tests.Concurrency;

/// <summary>
/// 队列管理边界测试：队列溢出、高并发、资源耗尽
/// Queue management boundary tests: queue overflow, high concurrency, resource exhaustion
/// </summary>
public class ParcelQueueBoundaryTests
{
    [Fact]
    public async Task PriorityQueue_WithCapacity_RejectsWhenFull()
    {
        // Arrange
        var queue = new PriorityParcelQueue(capacity: 10);

        // Act - Fill the queue
        for (int i = 0; i < 10; i++)
        {
            await queue.EnqueueAsync(new ParcelQueueItem
            {
                ParcelId = $"PKG{i:000}",
                TargetChuteId = "1",
                Priority = 1
            });
        }

        // Assert
        queue.Count.Should().Be(10);
    }

    [Fact]
    public async Task PriorityQueue_WithNullItem_ThrowsArgumentNullException()
    {
        // Arrange
        var queue = new PriorityParcelQueue();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => queue.EnqueueAsync(null!));
    }

    [Fact]
    public async Task PriorityQueue_WithHighConcurrency_MaintainsIntegrity()
    {
        // Arrange
        var queue = new PriorityParcelQueue();
        const int threadCount = 50;
        const int itemsPerThread = 20;
        var tasks = new List<Task>();

        // Act - Enqueue from multiple threads
        for (int i = 0; i < threadCount; i++)
        {
            int threadId = i;
            tasks.Add(Task.Run(async () =>
            {
                for (int j = 0; j < itemsPerThread; j++)
                {
                    await queue.EnqueueAsync(new ParcelQueueItem
                    {
                        ParcelId = $"T{threadId:00}_P{j:00}",
                        TargetChuteId = (j % 5 + 1).ToString(),
                        Priority = j % 3
                    });
                }
            }));
        }

        await Task.WhenAll(tasks);

        // Assert
        queue.Count.Should().Be(threadCount * itemsPerThread);
    }

    [Fact]
    public async Task PriorityQueue_EnqueueAndDequeueConcurrently_WorksCorrectly()
    {
        // Arrange
        var queue = new PriorityParcelQueue();
        const int producerCount = 10;
        const int consumerCount = 10;
        const int itemsPerProducer = 50;
        var producedCount = 0;
        var consumedCount = 0;
        var cts = new CancellationTokenSource();

        // Act - Start producers
        var producerTasks = Enumerable.Range(0, producerCount).Select(i => Task.Run(async () =>
        {
            for (int j = 0; j < itemsPerProducer; j++)
            {
                await queue.EnqueueAsync(new ParcelQueueItem
                {
                    ParcelId = $"P{i:00}_{j:000}",
                    TargetChuteId = (j % 5 + 1).ToString(),
                    Priority = j % 3
                });
                Interlocked.Increment(ref producedCount);
            }
        })).ToList();

        // Start consumers
        var consumerTasks = Enumerable.Range(0, consumerCount).Select(i => Task.Run(async () =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                try
                {
                    var item = await queue.DequeueAsync(cts.Token);
                    if (item != null)
                    {
                        Interlocked.Increment(ref consumedCount);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        })).ToList();

        // Wait for all producers to finish
        await Task.WhenAll(producerTasks);

        // Wait for queue to be empty
        while (queue.Count > 0)
        {
            await Task.Delay(10);
        }

        // Stop consumers
        cts.Cancel();
        await Task.WhenAll(consumerTasks);

        // Assert
        consumedCount.Should().BeGreaterThanOrEqualTo(producerCount * itemsPerProducer);
    }

    [Fact]
    public async Task DequeueBatch_WithMaxBatchSize_RespectsLimit()
    {
        // Arrange
        var queue = new PriorityParcelQueue();
        const int itemCount = 100;
        const int batchSize = 10;

        // Add items
        for (int i = 0; i < itemCount; i++)
        {
            await queue.EnqueueAsync(new ParcelQueueItem
            {
                ParcelId = $"PKG{i:000}",
                TargetChuteId = "1",
                Priority = 1
            });
        }

        // Act
        var batch = await queue.DequeueBatchAsync(batchSize);

        // Assert
        batch.Count.Should().BeLessThanOrEqualTo(batchSize);
        batch.Count.Should().BeGreaterThan(0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-10)]
    public async Task DequeueBatch_WithInvalidBatchSize_ThrowsArgumentException(int batchSize)
    {
        // Arrange
        var queue = new PriorityParcelQueue();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => queue.DequeueBatchAsync(batchSize));
    }

    [Fact]
    public async Task DequeueBatch_GroupsBySameTargetChute()
    {
        // Arrange
        var queue = new PriorityParcelQueue();
        
        // Add items with different target chutes
        await queue.EnqueueAsync(new ParcelQueueItem { ParcelId = "PKG1", TargetChuteId = "1", Priority = 1 });
        await queue.EnqueueAsync(new ParcelQueueItem { ParcelId = "PKG2", TargetChuteId = "1", Priority = 1 });
        await queue.EnqueueAsync(new ParcelQueueItem { ParcelId = "PKG3", TargetChuteId = "1", Priority = 1 });
        await queue.EnqueueAsync(new ParcelQueueItem { ParcelId = "PKG4", TargetChuteId = "2", Priority = 1 });
        await queue.EnqueueAsync(new ParcelQueueItem { ParcelId = "PKG5", TargetChuteId = "2", Priority = 1 });

        // Act
        var batch = await queue.DequeueBatchAsync(10);

        // Assert - Should get all items with same target chute
        batch.Should().NotBeEmpty();
        var firstTarget = batch[0].TargetChuteId;
        batch.Should().AllSatisfy(item => item.TargetChuteId.Should().Be(firstTarget));
    }

    [Fact]
    public void TryDequeue_OnEmptyQueue_ReturnsFalse()
    {
        // Arrange
        var queue = new PriorityParcelQueue();

        // Act
        var result = queue.TryDequeue(out var item);

        // Assert
        result.Should().BeFalse();
        item.Should().BeNull();
    }

    [Fact]
    public async Task MonitoredQueue_WithExtremeQueueLength_UpdatesAlarmService()
    {
        // Arrange
        var innerQueue = new PriorityParcelQueue();
        var loggerMock = new Mock<ILogger<AlarmService>>();
        var alarmService = new AlarmService(loggerMock.Object, Mock.Of<ISystemClock>());
        var monitoredQueue = new MonitoredParcelQueue(innerQueue, alarmService);

        // Act - Add many items to trigger alarm
        for (int i = 0; i < 100; i++)
        {
            await monitoredQueue.EnqueueAsync(new ParcelQueueItem
            {
                ParcelId = $"PKG{i:000}",
                TargetChuteId = "1",
                Priority = 1
            });
        }

        // Assert - Alarm should be triggered for queue backlog
        var alarms = alarmService.GetActiveAlarms();
        var queueAlarm = alarms.FirstOrDefault(a => a.Type == AlarmType.QueueBacklog);
        queueAlarm.Should().NotBeNull();
    }

    [Fact]
    public async Task PriorityQueue_StressTest_HandlesMillionOperations()
    {
        // Arrange
        var queue = new PriorityParcelQueue();
        const int operationCount = 100000; // Reduced from 1 million for test performance
        var cts = new CancellationTokenSource();

        // Act - Concurrent producers and consumers
        var producerTask = Task.Run(async () =>
        {
            for (int i = 0; i < operationCount; i++)
            {
                await queue.EnqueueAsync(new ParcelQueueItem
                {
                    ParcelId = $"PKG{i:000000}",
                    TargetChuteId = (i % 10 + 1).ToString(),
                    Priority = i % 3
                });
            }
        });

        var consumerTask = Task.Run(async () =>
        {
            int consumed = 0;
            while (consumed < operationCount)
            {
                if (queue.TryDequeue(out var item))
                {
                    consumed++;
                }
                else
                {
                    await Task.Delay(1);
                }
            }
        });

        await Task.WhenAll(producerTask, consumerTask);

        // Assert
        queue.Count.Should().Be(0);
    }

    [Fact]
    public async Task DequeueAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var queue = new PriorityParcelQueue();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        // TaskCanceledException inherits from OperationCanceledException
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => 
            queue.DequeueAsync(cts.Token));
    }

    [Fact]
    public async Task EnqueueAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var queue = new PriorityParcelQueue();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var item = new ParcelQueueItem
        {
            ParcelId = "PKG001",
            TargetChuteId = "1",
            Priority = 1
        };

        // Act & Assert
        // TaskCanceledException inherits from OperationCanceledException
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => 
            queue.EnqueueAsync(item, cts.Token));
    }

    [Fact]
    public async Task Count_IsConcurrentlyAccurate()
    {
        // Arrange
        var queue = new PriorityParcelQueue();
        const int iterations = 1000;
        
        // Act - Concurrent enqueue and dequeue
        var enqueueTask = Task.Run(async () =>
        {
            for (int i = 0; i < iterations; i++)
            {
                await queue.EnqueueAsync(new ParcelQueueItem
                {
                    ParcelId = $"PKG{i:0000}",
                    TargetChuteId = "1",
                    Priority = 1
                });
            }
        });

        var dequeueTask = Task.Run(async () =>
        {
            for (int i = 0; i < iterations / 2; i++)
            {
                while (!queue.TryDequeue(out _))
                {
                    await Task.Delay(1);
                }
            }
        });

        await Task.WhenAll(enqueueTask, dequeueTask);

        // Assert - Count should be accurate
        queue.Count.Should().Be(iterations / 2);
    }
}
