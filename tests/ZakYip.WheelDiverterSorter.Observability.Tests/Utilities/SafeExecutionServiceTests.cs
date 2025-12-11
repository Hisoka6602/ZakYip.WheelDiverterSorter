using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Observability;
using ZakYip.WheelDiverterSorter.Observability.Utilities;

namespace ZakYip.WheelDiverterSorter.Observability.Tests.Utilities;

/// <summary>
/// Tests for SafeExecutionService - PR-37
/// </summary>
public class SafeExecutionServiceTests
{
    private readonly Mock<ILogger<SafeExecutionService>> _mockLogger;
    private readonly Mock<ISystemClock> _mockSystemClock;
    private readonly SafeExecutionService _service;
    private readonly DateTime _testTime = new(2024, 1, 1, 12, 0, 0);

    public SafeExecutionServiceTests()
    {
        _mockLogger = new Mock<ILogger<SafeExecutionService>>();
        _mockSystemClock = new Mock<ISystemClock>();
        _mockSystemClock.Setup(c => c.LocalNow).Returns(_testTime);
        _service = new SafeExecutionService(_mockLogger.Object, _mockSystemClock.Object);
    }

    [Fact]
    public async Task ExecuteAsync_SuccessfulOperation_ReturnsTrue()
    {
        // Arrange
        var executed = false;
        Func<Task> action = () =>
        {
            executed = true;
            return Task.CompletedTask;
        };

        // Act
        var result = await _service.ExecuteAsync(action, "TestOperation");

        // Assert
        Assert.True(result);
        Assert.True(executed);
    }

    [Fact]
    public async Task ExecuteAsync_ThrowsException_CatchesAndReturnsFalse()
    {
        // Arrange
        Func<Task> action = () => throw new InvalidOperationException("Test exception");

        // Act
        var result = await _service.ExecuteAsync(action, "TestOperation");

        // Assert
        Assert.False(result);
        
        // Verify that error was logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("TestOperation")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ThrowsException_DoesNotRethrow()
    {
        // Arrange
        Func<Task> action = () => throw new InvalidOperationException("Test exception");

        // Act & Assert - should not throw
        var result = await _service.ExecuteAsync(action, "TestOperation");
        Assert.False(result);
    }

    [Fact]
    public async Task ExecuteAsync_Cancelled_ReturnsFalseAndLogsInformation()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();
        Func<Task> action = () => throw new OperationCanceledException();

        // Act
        var result = await _service.ExecuteAsync(action, "TestOperation", cts.Token);

        // Assert
        Assert.False(result);
        
        // Verify that cancellation was logged as information
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("cancelled")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithReturnValue_SuccessfulOperation_ReturnsValue()
    {
        // Arrange
        const int expectedValue = 42;
        Func<Task<int>> action = () => Task.FromResult(expectedValue);

        // Act
        var result = await _service.ExecuteAsync(action, "TestOperation", defaultValue: 0);

        // Assert
        Assert.Equal(expectedValue, result);
    }

    [Fact]
    public async Task ExecuteAsync_WithReturnValue_ThrowsException_ReturnsDefaultValue()
    {
        // Arrange
        const int defaultValue = -1;
        Func<Task<int>> action = () => throw new InvalidOperationException("Test exception");

        // Act
        var result = await _service.ExecuteAsync(action, "TestOperation", defaultValue);

        // Assert
        Assert.Equal(defaultValue, result);
        
        // Verify that error was logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("TestOperation")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_LogsExceptionTypeAndMessage()
    {
        // Arrange
        const string exceptionMessage = "Specific test error";
        Func<Task> action = () => throw new ArgumentException(exceptionMessage);

        // Act
        await _service.ExecuteAsync(action, "TestOperation");

        // Assert - verify log contains exception type and message
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => 
                    v.ToString()!.Contains("ArgumentException") && 
                    v.ToString()!.Contains(exceptionMessage)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_LogsLocalTime()
    {
        // Arrange
        Func<Task> action = () => throw new InvalidOperationException("Test");

        // Act
        await _service.ExecuteAsync(action, "TestOperation");

        // Assert - verify log contains local time
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(_testTime.ToString())),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_NullAction_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => 
            _service.ExecuteAsync((Func<Task>)null!, "TestOperation"));
    }

    [Fact]
    public async Task ExecuteAsync_EmptyOperationName_ThrowsArgumentException()
    {
        // Arrange
        Func<Task> action = () => Task.CompletedTask;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _service.ExecuteAsync(action, ""));
    }

    [Fact]
    public async Task ExecuteAsync_OperationCanceledException_WithoutCancelledToken_LogsAsError()
    {
        // Arrange
        // Token is NOT cancelled, but action throws OperationCanceledException
        var cts = new CancellationTokenSource();
        Func<Task> action = () => throw new OperationCanceledException("Unexpected cancellation");

        // Act
        var result = await _service.ExecuteAsync(action, "TestOperation", cts.Token);

        // Assert
        Assert.False(result);
        
        // Should be logged as error, not information, because token was not cancelled
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("TestOperation")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithReturnValue_Cancelled_ReturnsDefaultValue()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();
        Func<Task<int>> action = () => throw new OperationCanceledException();

        // Act
        var result = await _service.ExecuteAsync(action, "TestOperation", defaultValue: 99, cts.Token);

        // Assert
        Assert.Equal(99, result);
        
        // Verify that cancellation was logged as information
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("cancelled")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithReturnValue_OperationCanceledException_WithoutCancelledToken_ReturnsDefaultValue()
    {
        // Arrange
        // Token is NOT cancelled, but action throws OperationCanceledException
        var cts = new CancellationTokenSource();
        Func<Task<int>> action = () => throw new OperationCanceledException("Unexpected cancellation");

        // Act
        var result = await _service.ExecuteAsync(action, "TestOperation", defaultValue: 42, cts.Token);

        // Assert
        Assert.Equal(42, result);
        
        // Should be logged as error, not information, because token was not cancelled
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("TestOperation")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithReturnValue_NullAction_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => 
            _service.ExecuteAsync((Func<Task<int>>)null!, "TestOperation", defaultValue: 0));
    }

    [Fact]
    public async Task ExecuteAsync_WithReturnValue_EmptyOperationName_ThrowsArgumentException()
    {
        // Arrange
        Func<Task<int>> action = () => Task.FromResult(42);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _service.ExecuteAsync(action, "", defaultValue: 0));
    }
}
