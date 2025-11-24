using FluentAssertions;
using Xunit;
using ZakYip.WheelDiverterSorter.Execution.Concurrency;

namespace ZakYip.WheelDiverterSorter.Execution.Tests.Concurrency;

/// <summary>
/// Tests for DiverterResourceLockManager
/// </summary>
public class DiverterResourceLockManagerTests
{
    [Fact]
    public void GetLock_WithValidDiverterId_ReturnsLock()
    {
        // Arrange
        using var manager = new DiverterResourceLockManager();

        // Act
        var lock1 = manager.GetLock(1);

        // Assert
        lock1.Should().NotBeNull();
    }

    [Fact]
    public void GetLock_WithSameDiverterId_ReturnsSameLock()
    {
        // Arrange

        // Act
        var lock1 = manager.GetLock(1);
        var lock2 = manager.GetLock(1);

        // Assert
        lock1.Should().BeSameAs(lock2);
    }

    [Fact]
    public void GetLock_WithDifferentDiverterIds_ReturnsDifferentLocks()
    {
        // Arrange

        // Act
        var lock1 = manager.GetLock(1);
        var lock2 = manager.GetLock(2);

        // Assert
        lock1.Should().NotBeSameAs(lock2);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void GetLock_WithInvalidDiverterId_ThrowsArgumentException(int diverterId)
    {
        // Arrange

        // Act
        Action act = () => manager.GetLock(diverterId);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("diverterId");
    }

    [Fact]
    public void Dispose_DisposesAllLocks()
    {
        // Arrange
        var manager = new DiverterResourceLockManager();
        var lock1 = manager.GetLock(1);
        var lock2 = manager.GetLock(2);

        // Act
        manager.Dispose();

        // Assert - Should not throw
        Assert.True(true);
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        // Arrange
        var manager = new DiverterResourceLockManager();
        manager.GetLock(1);

        // Act
        manager.Dispose();
        manager.Dispose();

        // Assert - Should not throw
        Assert.True(true);
    }

    [Fact]
    public void GetLock_ThreadSafe_ReturnsSameLockInstance()
    {
        // Arrange
        const int diverterId = 42;
        const int threadCount = 10;
        var locks = new IDiverterResourceLock[threadCount];
        var tasks = new Task[threadCount];

        // Act
        for (int i = 0; i < threadCount; i++)
        {
            int index = i;
            tasks[i] = Task.Run(() => { locks[index] = manager.GetLock(diverterId); });
        }
        Task.WaitAll(tasks);

        // Assert
        for (int i = 1; i < threadCount; i++)
        {
            locks[i].Should().BeSameAs(locks[0]);
        }
    }
}
