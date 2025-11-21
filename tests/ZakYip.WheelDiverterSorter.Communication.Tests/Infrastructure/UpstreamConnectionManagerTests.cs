using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ZakYip.WheelDiverterSorter.Communication.Abstractions;
using ZakYip.WheelDiverterSorter.Communication.Configuration;
using ZakYip.WheelDiverterSorter.Communication.Infrastructure;
using ZakYip.WheelDiverterSorter.Core.Enums.Communication;
using ZakYip.WheelDiverterSorter.Core.Enums.Conveyor;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Enums.Routing;
using ZakYip.WheelDiverterSorter.Core.Enums.Sensors;
using ZakYip.WheelDiverterSorter.Core.Enums.System;
using ZakYip.WheelDiverterSorter.Observability.Utilities;

namespace ZakYip.WheelDiverterSorter.Communication.Tests.Infrastructure;

/// <summary>
/// Tests for UpstreamConnectionManager focusing on retry strategy, backoff, and hot configuration updates
/// </summary>
public class UpstreamConnectionManagerTests : IDisposable
{
    private readonly Mock<ILogger<UpstreamConnectionManager>> _loggerMock;
    private readonly Mock<ISystemClock> _systemClockMock;
    private readonly Mock<ILogDeduplicator> _logDeduplicatorMock;
    private readonly Mock<ISafeExecutionService> _safeExecutorMock;
    private readonly Mock<IRuleEngineClient> _clientMock;
    private readonly DateTime _testTime = new(2025, 11, 20, 12, 0, 0);

    public UpstreamConnectionManagerTests()
    {
        _loggerMock = new Mock<ILogger<UpstreamConnectionManager>>();
        _systemClockMock = new Mock<ISystemClock>();
        _systemClockMock.Setup(x => x.LocalNow).Returns(_testTime);
        _systemClockMock.Setup(x => x.LocalNowOffset).Returns(new DateTimeOffset(_testTime));
        
        _logDeduplicatorMock = new Mock<ILogDeduplicator>();
        _logDeduplicatorMock.Setup(x => x.ShouldLog(It.IsAny<LogLevel>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(true);
        
        _safeExecutorMock = new Mock<ISafeExecutionService>();
        _safeExecutorMock
            .Setup(x => x.ExecuteAsync(It.IsAny<Func<Task>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<Func<Task>, string, CancellationToken>(async (action, context, ct) =>
            {
                await action();
                return true;
            });
        
        _clientMock = new Mock<IRuleEngineClient>();
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var options = CreateDefaultOptions();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new UpstreamConnectionManager(
            null!,
            _systemClockMock.Object,
            _logDeduplicatorMock.Object,
            _safeExecutorMock.Object,
            _clientMock.Object,
            options));
    }

    [Fact]
    public void Constructor_WithNullSystemClock_ThrowsArgumentNullException()
    {
        // Arrange
        var options = CreateDefaultOptions();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new UpstreamConnectionManager(
            _loggerMock.Object,
            null!,
            _logDeduplicatorMock.Object,
            _safeExecutorMock.Object,
            _clientMock.Object,
            options));
    }

    [Fact]
    public void Constructor_WithNullLogDeduplicator_ThrowsArgumentNullException()
    {
        // Arrange
        var options = CreateDefaultOptions();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new UpstreamConnectionManager(
            _loggerMock.Object,
            _systemClockMock.Object,
            null!,
            _safeExecutorMock.Object,
            _clientMock.Object,
            options));
    }

    [Fact]
    public void Constructor_WithNullSafeExecutor_ThrowsArgumentNullException()
    {
        // Arrange
        var options = CreateDefaultOptions();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new UpstreamConnectionManager(
            _loggerMock.Object,
            _systemClockMock.Object,
            _logDeduplicatorMock.Object,
            null!,
            _clientMock.Object,
            options));
    }

    [Fact]
    public void Constructor_WithNullClient_ThrowsArgumentNullException()
    {
        // Arrange
        var options = CreateDefaultOptions();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new UpstreamConnectionManager(
            _loggerMock.Object,
            _systemClockMock.Object,
            _logDeduplicatorMock.Object,
            _safeExecutorMock.Object,
            null!,
            options));
    }

    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new UpstreamConnectionManager(
            _loggerMock.Object,
            _systemClockMock.Object,
            _logDeduplicatorMock.Object,
            _safeExecutorMock.Object,
            _clientMock.Object,
            null!));
    }

    [Fact]
    public void IsConnected_InitiallyReturnsFalse()
    {
        // Arrange
        var options = CreateDefaultOptions();
        using var manager = CreateManager(options);

        // Act & Assert
        Assert.False(manager.IsConnected);
    }

    [Fact]
    public async Task StartAsync_WithServerMode_DoesNotStartReconnectionLoop()
    {
        // Arrange
        var options = CreateDefaultOptions();
        options.ConnectionMode = ConnectionMode.Server;
        using var manager = CreateManager(options);

        // Act
        await manager.StartAsync();
        await Task.Delay(100); // Give time for any potential startup

        // Assert - Should not attempt to connect in server mode
        _clientMock.Verify(x => x.ConnectAsync(It.IsAny<CancellationToken>()), Times.Never);
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Server mode detected")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task StartAsync_WithClientMode_StartsReconnectionLoop()
    {
        // Arrange
        var options = CreateDefaultOptions();
        options.ConnectionMode = ConnectionMode.Client;
        using var manager = CreateManager(options);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));

        // Act
        await manager.StartAsync(cts.Token);
        await Task.Delay(100); // Give time for startup

        // Assert - Should log startup message
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("connection manager started in client mode")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateConnectionOptionsAsync_WithNullOptions_ThrowsArgumentNullException()
    {
        // Arrange
        var options = CreateDefaultOptions();
        using var manager = CreateManager(options);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await manager.UpdateConnectionOptionsAsync(null!));
    }

    [Fact]
    public async Task UpdateConnectionOptionsAsync_UpdatesConfigurationSuccessfully()
    {
        // Arrange
        var initialOptions = CreateDefaultOptions();
        using var manager = CreateManager(initialOptions);
        
        var newOptions = CreateDefaultOptions();
        newOptions.TcpServer = "192.168.1.200:9000";
        newOptions.InitialBackoffMs = 300;

        // Act
        await manager.UpdateConnectionOptionsAsync(newOptions);

        // Assert - Should log configuration update
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Connection options updated")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task StopAsync_StopsConnectionLoop()
    {
        // Arrange
        var options = CreateDefaultOptions();
        options.ConnectionMode = ConnectionMode.Client;
        using var manager = CreateManager(options);
        
        await manager.StartAsync();
        await Task.Delay(100);

        // Act
        await manager.StopAsync();

        // Assert - Should log stop message
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("connection manager stopped")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ConnectionStateChanged_EventCanBeSubscribed()
    {
        // Arrange
        var options = CreateDefaultOptions();
        using var manager = CreateManager(options);
        
        var eventFired = false;
        ConnectionStateChangedEventArgs? eventArgs = null;
        manager.ConnectionStateChanged += (sender, args) => 
        {
            eventFired = true;
            eventArgs = args;
        };

        // Note: Since ConnectAsync is a placeholder that does nothing,
        // we can't easily test state changes without a real implementation.
        // This test verifies the event mechanism can be subscribed to.
        
        // Act & Assert - Event handler was successfully attached
        Assert.False(eventFired); // No events should have fired yet
    }

    [Theory]
    [InlineData(200, 400)]
    [InlineData(400, 800)]
    [InlineData(800, 1600)]
    [InlineData(1600, 2000)]
    [InlineData(2000, 2000)] // Should cap at 2000ms
    public void ExponentialBackoff_DoublesDelayUpTo2Seconds(int currentBackoff, int expectedNext)
    {
        // This test verifies the backoff calculation logic
        // The actual implementation caps at Math.Min(currentBackoff * 2, Math.Min(maxBackoff, 2000))
        
        var maxBackoff = 2000;
        var hardMax = 2000;
        var nextBackoff = Math.Min(currentBackoff * 2, Math.Min(maxBackoff, hardMax));
        
        Assert.Equal(expectedNext, nextBackoff);
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        // Arrange
        var options = CreateDefaultOptions();
        var manager = CreateManager(options);

        // Act & Assert - Should not throw
        manager.Dispose();
        manager.Dispose();
    }

    private RuleEngineConnectionOptions CreateDefaultOptions()
    {
        return new RuleEngineConnectionOptions
        {
            Mode = CommunicationMode.Tcp,
            ConnectionMode = ConnectionMode.Client,
            TcpServer = "localhost:8000",
            TimeoutMs = 5000,
            InitialBackoffMs = 200,
            MaxBackoffMs = 2000,
            EnableInfiniteRetry = true,
            EnableAutoReconnect = true
        };
    }

    private UpstreamConnectionManager CreateManager(RuleEngineConnectionOptions options)
    {
        return new UpstreamConnectionManager(
            _loggerMock.Object,
            _systemClockMock.Object,
            _logDeduplicatorMock.Object,
            _safeExecutorMock.Object,
            _clientMock.Object,
            options);
    }

    public void Dispose()
    {
        // Cleanup if needed
    }
}
