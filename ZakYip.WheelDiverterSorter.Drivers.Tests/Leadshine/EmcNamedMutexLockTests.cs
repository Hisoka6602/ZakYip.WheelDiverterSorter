using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ZakYip.WheelDiverterSorter.Drivers.Leadshine;

namespace ZakYip.WheelDiverterSorter.Drivers.Tests.Leadshine;

public class EmcNamedMutexLockTests
{
    private readonly Mock<ILogger<EmcNamedMutexLock>> _mockLogger;

    public EmcNamedMutexLockTests()
    {
        _mockLogger = new Mock<ILogger<EmcNamedMutexLock>>();
    }

    [Fact]
    public void Constructor_WithValidResourceName_SetsLockIdentifier()
    {
        // Arrange & Act
        var resourceName = "CardNo_0";
        var emcLock = new EmcNamedMutexLock(_mockLogger.Object, resourceName);

        // Assert
        Assert.Equal(resourceName, emcLock.LockIdentifier);
        Assert.False(emcLock.IsLockHeld);
    }

    [Fact]
    public void Constructor_WithNullResourceName_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new EmcNamedMutexLock(_mockLogger.Object, null!));
    }

    [Fact]
    public void Constructor_WithEmptyResourceName_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new EmcNamedMutexLock(_mockLogger.Object, string.Empty));
    }

    [Fact]
    public async Task TryAcquireAsync_FirstTime_ReturnsTrue()
    {
        // Arrange
        var resourceName = $"TestResource_{Guid.NewGuid()}";
        using var emcLock = new EmcNamedMutexLock(_mockLogger.Object, resourceName);

        // Act
        var acquired = await emcLock.TryAcquireAsync(TimeSpan.FromSeconds(5));

        // Assert
        Assert.True(acquired);
        Assert.True(emcLock.IsLockHeld);
    }

    [Fact]
    public async Task TryAcquireAsync_WhenAlreadyHeld_ReturnsTrue()
    {
        // Arrange
        var resourceName = $"TestResource_{Guid.NewGuid()}";
        using var emcLock = new EmcNamedMutexLock(_mockLogger.Object, resourceName);
        await emcLock.TryAcquireAsync(TimeSpan.FromSeconds(5));

        // Act - try to acquire again
        var acquired = await emcLock.TryAcquireAsync(TimeSpan.FromSeconds(5));

        // Assert - should return true (idempotent)
        Assert.True(acquired);
        Assert.True(emcLock.IsLockHeld);
    }

    [Fact]
    public void Release_WhenLockNotHeld_DoesNotThrow()
    {
        // Arrange
        var resourceName = $"TestResource_{Guid.NewGuid()}";
        using var emcLock = new EmcNamedMutexLock(_mockLogger.Object, resourceName);

        // Act & Assert (should not throw)
        emcLock.Release();
        Assert.False(emcLock.IsLockHeld);
    }

    [Fact]
    public async Task Dispose_ReleasesLockAutomatically()
    {
        // Arrange
        var resourceName = $"TestResource_{Guid.NewGuid()}";
        var emcLock1 = new EmcNamedMutexLock(_mockLogger.Object, resourceName);
        await emcLock1.TryAcquireAsync(TimeSpan.FromSeconds(5));

        // Act
        emcLock1.Dispose();

        // Assert - should be able to acquire with another instance
        using var emcLock2 = new EmcNamedMutexLock(_mockLogger.Object, resourceName);
        var acquired = await emcLock2.TryAcquireAsync(TimeSpan.FromSeconds(5));
        Assert.True(acquired);
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        // Arrange
        var resourceName = $"TestResource_{Guid.NewGuid()}";
        var emcLock = new EmcNamedMutexLock(_mockLogger.Object, resourceName);

        // Act & Assert (should not throw)
        emcLock.Dispose();
        emcLock.Dispose();
        emcLock.Dispose();
    }

    [Fact]
    public async Task TryAcquireAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var resourceName = $"TestResource_{Guid.NewGuid()}";
        var emcLock = new EmcNamedMutexLock(_mockLogger.Object, resourceName);
        emcLock.Dispose();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => emcLock.TryAcquireAsync(TimeSpan.FromSeconds(5)));
    }
}
