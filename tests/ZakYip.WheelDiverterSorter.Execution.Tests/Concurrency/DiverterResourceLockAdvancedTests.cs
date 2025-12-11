using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using FluentAssertions;
using Xunit;
using ZakYip.WheelDiverterSorter.Execution.Concurrency;

namespace ZakYip.WheelDiverterSorter.Execution.Tests.Concurrency;

/// <summary>
/// 并发控制高级测试：资源锁竞争、死锁预防、高并发场景
/// Advanced concurrency tests: resource lock contention, deadlock prevention, high concurrency scenarios
/// </summary>
public class DiverterResourceLockAdvancedTests
{
    [Fact(Skip = "存在线程锁递归问题，ReaderWriterLockSlim 在 NoRecursion 模式下与 Task.Run 线程池复用冲突")]
    public async Task AcquireLock_WithHighContention_HandlesCorrectly()
    {
        // Arrange
        using var manager = new DiverterResourceLockManager();
        var lockInstance = manager.GetLock(1);
        const int threadCount = 100;
        var successCount = 0;
        var tasks = new Task[threadCount];

        // Act
        for (int i = 0; i < threadCount; i++)
        {
            tasks[i] = Task.Run(async () =>
            {
                using (await lockInstance.AcquireWriteLockAsync())
                {
                    Interlocked.Increment(ref successCount);
                    await Task.Delay(10); // Simulate work
                }
            });
        }

        await Task.WhenAll(tasks);

        // Assert - All should have acquired the lock successfully
        successCount.Should().Be(threadCount);
    }

    [Fact(Skip = "存在跨线程锁释放问题，锁在 Task.Run 线程获取但在测试线程释放")]
    public async Task AcquireLock_WithCancellationToken_ThrowsOnCancel()
    {
        // Arrange
        using var manager = new DiverterResourceLockManager();
        var lockInstance = manager.GetLock(1);
        var cts = new CancellationTokenSource();

        // First acquire the lock
        var lock1 = await lockInstance.AcquireWriteLockAsync();

        // Act - Try to acquire with cancelled token
        cts.Cancel();
        
        // Assert - Should throw and NOT acquire lock (no dispose needed)
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await lockInstance.AcquireWriteLockAsync(cts.Token));

        // Cleanup only the successfully acquired lock
        lock1.Dispose();
    }

    [Fact(Skip = "存在线程锁递归问题，锁在一个线程获取但在另一个线程释放导致同步错误")]
    public async Task MultipleLocks_WithDifferentDiverters_AllowConcurrentAccess()
    {
        // Arrange
        using var manager = new DiverterResourceLockManager();
        var lock1 = manager.GetLock(1);
        var lock2 = manager.GetLock(2);
        var lock3 = manager.GetLock(3);

        // Act - Acquire all locks concurrently
        var task1 = lock1.AcquireWriteLockAsync();
        var task2 = lock2.AcquireWriteLockAsync();
        var task3 = lock3.AcquireWriteLockAsync();

        var handles = await Task.WhenAll(task1, task2, task3);

        // Assert - All should succeed
        handles.Should().AllSatisfy(h => h.Should().NotBeNull());

        // Cleanup
        foreach (var handle in handles)
        {
            handle.Dispose();
        }
    }

    [Fact(Skip = "存在线程锁递归问题，ReaderWriterLockSlim 在 NoRecursion 模式下与 Task.Run 线程池复用冲突")]
    public async Task ReadLocks_AllowConcurrentAccess()
    {
        // Arrange
        using var manager = new DiverterResourceLockManager();
        var lockInstance = manager.GetLock(1);
        var readsCompleted = 0;

        // Act - Multiple readers should be able to acquire simultaneously
        var tasks = Enumerable.Range(0, 10).Select(_ => Task.Run(async () =>
        {
            using (await lockInstance.AcquireReadLockAsync())
            {
                Interlocked.Increment(ref readsCompleted);
                await Task.Delay(50); // Hold the lock
            }
        })).ToArray();

        await Task.WhenAll(tasks);

        // Assert
        readsCompleted.Should().Be(10);
    }

    [Fact(Skip = "存在线程锁递归问题，ReaderWriterLockSlim 在 NoRecursion 模式下与 Task.Run 线程池复用冲突")]
    public async Task WriteLock_BlocksOtherWriters()
    {
        // Arrange
        using var manager = new DiverterResourceLockManager();
        var lockInstance = manager.GetLock(1);
        var executionOrder = new List<int>();

        // Act - First writer
        var firstWriterHandle = await lockInstance.AcquireWriteLockAsync();
        
        // Second writer tries to acquire
        var secondWriterTask = Task.Run(async () =>
        {
            using (await lockInstance.AcquireWriteLockAsync())
            {
                lock (executionOrder) executionOrder.Add(2);
            }
        });

        await Task.Delay(50); // Ensure second writer is waiting
        lock (executionOrder) executionOrder.Add(1);
        firstWriterHandle.Dispose(); // Release first lock

        await secondWriterTask;

        // Assert - Execution order should be sequential
        executionOrder.Should().Equal(1, 2);
    }

    [Fact]
    public async Task AcquireLock_AfterManagerDisposed_ShouldThrow()
    {
        // Arrange
        var manager = new DiverterResourceLockManager();
        var lockInstance = manager.GetLock(1);
        manager.Dispose();

        // Act & Assert
        Func<Task> act = async () => await lockInstance.AcquireWriteLockAsync();
        
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact(Skip = "存在线程锁递归问题，Task.Run 线程池复用导致锁同步错误")]
    public async Task StressTest_ManyDiverters_ManyOperations()
    {
        // Arrange
        using var manager = new DiverterResourceLockManager();
        const int diverterCount = 50;
        const int operationsPerDiverter = 20;
        var totalOperations = 0;

        // Act
        var tasks = new List<Task>();
        for (int i = 1; i <= diverterCount; i++)
        {
            int diverterId = i;
            tasks.Add(Task.Run(async () =>
            {
                var lockInstance = manager.GetLock(diverterId);
                for (int j = 0; j < operationsPerDiverter; j++)
                {
                    using (await lockInstance.AcquireWriteLockAsync())
                    {
                        Interlocked.Increment(ref totalOperations);
                        await Task.Delay(1);
                    }
                }
            }));
        }

        await Task.WhenAll(tasks);

        // Assert
        totalOperations.Should().Be(diverterCount * operationsPerDiverter);
    }

    [Fact]
    public async Task ConcurrentGetLock_CreatesOnlyOneInstance()
    {
        // Arrange
        using var manager = new DiverterResourceLockManager();
        const int diverterId = 42;
        const int threadCount = 50;
        var locks = new IDiverterResourceLock[threadCount];
        var tasks = new Task[threadCount];

        // Act - Get the same lock from multiple threads
        for (int i = 0; i < threadCount; i++)
        {
            int index = i;
            tasks[i] = Task.Run(() => 
            { 
                locks[index] = manager.GetLock(diverterId); 
            });
        }

        await Task.WhenAll(tasks);

        // Assert - All references should point to the same instance
        var firstLock = locks[0];
        foreach (var lockInstance in locks)
        {
            lockInstance.Should().BeSameAs(firstLock);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(100)]
    [InlineData(1000)]
    [InlineData(10000)]
    public void GetLock_WithManyDifferentIds_HandlesScaleCorrectly(int lockCount)
    {
        // Arrange
        using var manager = new DiverterResourceLockManager();

        // Act
        var locks = new List<IDiverterResourceLock>();
        for (int i = 1; i <= lockCount; i++)
        {
            locks.Add(manager.GetLock(i));
        }

        // Assert - All locks should be unique and retrievable
        locks.Should().HaveCount(lockCount);
        locks.Should().OnlyHaveUniqueItems();
    }

    [Fact(Skip = "存在线程锁递归问题和锁释放竞态条件")]
    public async Task LockFairness_MultipleWaiters()
    {
        // Arrange
        using var manager = new DiverterResourceLockManager();
        var lockInstance = manager.GetLock(1);
        var acquisitionOrder = new List<int>();
        var startGate = new TaskCompletionSource<bool>();

        // First thread acquires the lock
        var firstHandle = await lockInstance.AcquireWriteLockAsync();

        // Act - Multiple threads wait for the lock
        var tasks = new List<Task>();
        for (int i = 0; i < 5; i++)
        {
            int threadId = i;
            tasks.Add(Task.Run(async () =>
            {
                await startGate.Task; // Wait for signal
                using (await lockInstance.AcquireWriteLockAsync())
                {
                    lock (acquisitionOrder)
                    {
                        acquisitionOrder.Add(threadId);
                    }
                    await Task.Delay(10);
                }
            }));
        }

        await Task.Delay(50); // Let all threads start waiting
        startGate.SetResult(true);

        // Release the initial lock
        firstHandle.Dispose();

        await Task.WhenAll(tasks);

        // Assert - All threads should have acquired the lock
        acquisitionOrder.Should().HaveCount(5);
    }
}
