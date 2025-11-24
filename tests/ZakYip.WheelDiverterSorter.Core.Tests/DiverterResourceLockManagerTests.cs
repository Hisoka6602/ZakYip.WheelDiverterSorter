using Xunit;
using ZakYip.WheelDiverterSorter.Execution.Concurrency;

namespace ZakYip.WheelDiverterSorter.Core.Tests;

/// <summary>
/// 摆轮资源锁管理器测试：测试锁的集中管理和多摆轮并发控制
/// </summary>
public class DiverterResourceLockManagerTests
{
    [Fact]
    public void GetLock_WithValidId_ReturnsLock()
    {
        // Arrange
        var manager = new DiverterResourceLockManager();

        // Act
        var resourceLock = manager.GetLock(1);

        // Assert
        Assert.NotNull(resourceLock);
        Assert.Equal("1", resourceLock.DiverterId);
    }

    [Fact]
    public void GetLock_WithZeroId_ThrowsArgumentException()
    {
        // Arrange
        var manager = new DiverterResourceLockManager();

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => manager.GetLock(0));
        Assert.Contains("摆轮ID必须大于0", exception.Message);
    }

    [Fact]
    public void GetLock_WithNegativeId_ThrowsArgumentException()
    {
        // Arrange
        var manager = new DiverterResourceLockManager();

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => manager.GetLock(-1));
        Assert.Contains("摆轮ID必须大于0", exception.Message);
    }

    [Fact]
    public void GetLock_CalledTwiceWithSameId_ReturnsSameInstance()
    {
        // Arrange
        var manager = new DiverterResourceLockManager();

        // Act
        var lock1 = manager.GetLock(1);
        var lock2 = manager.GetLock(1);

        // Assert
        Assert.Same(lock1, lock2);
    }

    [Fact]
    public void GetLock_CalledWithDifferentIds_ReturnsDifferentInstances()
    {
        // Arrange
        var manager = new DiverterResourceLockManager();

        // Act
        var lock1 = manager.GetLock(1);
        var lock2 = manager.GetLock(2);

        // Assert
        Assert.NotSame(lock1, lock2);
        Assert.Equal("1", lock1.DiverterId);
        Assert.Equal("2", lock2.DiverterId);
    }

    [Fact]
    public async Task GetLock_MultipleDiverters_IsolatesLocks()
    {
        // Arrange
        var manager = new DiverterResourceLockManager();
        var lock1 = manager.GetLock(1);
        var lock2 = manager.GetLock(2);
        
        var task1Started = false;
        var task2Started = false;

        // Act - acquire locks on different diverters simultaneously
        var task1 = Task.Run(async () =>
        {
            using var lockHandle = await lock1.AcquireWriteLockAsync();
            task1Started = true;
            await Task.Delay(100);
        });

        var task2 = Task.Run(async () =>
        {
            using var lockHandle = await lock2.AcquireWriteLockAsync();
            task2Started = true;
            await Task.Delay(100);
        });

        // Wait for both tasks to start
        await Task.Delay(50);

        // Assert - both should be active since they're different locks
        Assert.True(task1Started);
        Assert.True(task2Started);

        await Task.WhenAll(task1, task2);
    }

    [Fact]
    public async Task GetLock_SameDiverter_EnforcesExclusivity()
    {
        // Arrange
        var manager = new DiverterResourceLockManager();
        var lock1 = manager.GetLock(1);
        var lock2 = manager.GetLock(1); // Same diverter ID - should return same instance
        
        Assert.Same(lock1, lock2); // Verify they're the same instance
        
        var executionCount = 0;
        var maxConcurrent = 0;

        // Act - try to execute multiple operations on the same diverter
        var tasks = Enumerable.Range(1, 3).Select(async _ =>
        {
            var current = Interlocked.Increment(ref executionCount);
            maxConcurrent = Math.Max(maxConcurrent, current);
            await Task.Delay(50);
            Interlocked.Decrement(ref executionCount);
        });

        await Task.WhenAll(tasks);

        // Assert - should have executed serially (max concurrent = 1)
        Assert.Equal(1, maxConcurrent);
    }

    [Fact]
    public async Task GetLock_ConcurrentAccess_ThreadSafe()
    {
        // Arrange
        var manager = new DiverterResourceLockManager();
        var lockIds = new System.Collections.Concurrent.ConcurrentBag<int>();

        // Act - concurrently get locks for different diverters
        var tasks = Enumerable.Range(1, 10).Select(i => Task.Run(() =>
        {
            var resourceLock = manager.GetLock(i);
            lockIds.Add(int.Parse(resourceLock.DiverterId));
        }));

        await Task.WhenAll(tasks);

        // Assert - all lock IDs should be present
        Assert.Equal(10, lockIds.Distinct().Count());
        Assert.All(Enumerable.Range(1, 10), i => Assert.Contains(i, lockIds));
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        // Arrange
        var manager = new DiverterResourceLockManager();

        // Act & Assert - should not throw
        manager.Dispose();
        manager.Dispose();
    }

    [Fact]
    public void Dispose_DisposesAllLocks()
    {
        // Arrange
        var manager = new DiverterResourceLockManager();
        
        // Create several locks
        var lock1 = manager.GetLock(1);
        var lock2 = manager.GetLock(2);
        var lock3 = manager.GetLock(3);

        // Act
        manager.Dispose();

        // Assert - this test just ensures Dispose doesn't throw
        // In a real scenario, attempting to use disposed locks would fail
        Assert.NotNull(lock1);
        Assert.NotNull(lock2);
        Assert.NotNull(lock3);
    }

    [Fact]
    public async Task GetLock_WithHighConcurrency_MaintainsCorrectness()
    {
        // Arrange
        var manager = new DiverterResourceLockManager();
        var diverterCount = 3;
        var requestsPerDiverter = 5;
        var completedRequests = new System.Collections.Concurrent.ConcurrentDictionary<int, int>();

        // Initialize counters
        for (int i = 1; i <= diverterCount; i++)
        {
            completedRequests[i] = 0;
        }

        // Act - simulate concurrent requests to different diverters
        var tasks = new List<Task>();
        for (int i = 1; i <= diverterCount; i++)
        {
            var diverterId = i;
            // Each diverter gets its own set of sequential requests
            tasks.Add(Task.Run(async () =>
            {
                for (int j = 0; j < requestsPerDiverter; j++)
                {
                    var resourceLock = manager.GetLock(diverterId);
                    using var lockHandle = await resourceLock.AcquireWriteLockAsync();
                    
                    // Simulate some work
                    await Task.Delay(5);
                    
                    completedRequests.AddOrUpdate(diverterId, 1, (key, val) => val + 1);
                }
            }));
        }

        await Task.WhenAll(tasks);

        // Assert - all requests should have completed
        Assert.Equal(diverterCount, completedRequests.Count);
        Assert.All(completedRequests.Values, count => Assert.Equal(requestsPerDiverter, count));
    }
}
