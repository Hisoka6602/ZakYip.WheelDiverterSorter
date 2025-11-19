using FluentAssertions;
using Xunit;
using ZakYip.WheelDiverterSorter.Execution.Concurrency;

namespace ZakYip.WheelDiverterSorter.Execution.Tests.Concurrency;

/// <summary>
/// Tests for DiverterResourceLock
/// </summary>
public class DiverterResourceLockTests
{
    [Fact]
    public void DiverterId_ReturnsCorrectValue()
    {
        // Arrange
        const string expectedId = "test-diverter-42";
        using var lockInstance = new DiverterResourceLock(expectedId);

        // Act
        var actualId = lockInstance.DiverterId;

        // Assert
        actualId.Should().Be(expectedId);
    }

    [Fact]
    public void Constructor_WithNullDiverterId_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => new DiverterResourceLock(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("diverterId");
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        // Arrange
        var lockInstance = new DiverterResourceLock("test-resource");

        // Act
        lockInstance.Dispose();
        lockInstance.Dispose();

        // Assert - Should not throw
        Assert.True(true);
    }

    // Note: More complex async lock tests are difficult to write due to ReaderWriterLockSlim
    // thread affinity requirements. The actual lock functionality is tested through
    // integration tests with ConcurrentSwitchingPathExecutor.
}
