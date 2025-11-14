using Microsoft.Extensions.Logging;
using Moq;
using ZakYip.WheelDiverterSorter.Communication.Clients;
using ZakYip.WheelDiverterSorter.Communication.Configuration;
using ZakYip.WheelDiverterSorter.Core;

namespace ZakYip.WheelDiverterSorter.Communication.Tests;

/// <summary>
/// SignalR客户端测试：Hub连接、方法调用、回调
/// </summary>
public class SignalRRuleEngineClientTests
{
    private readonly Mock<ILogger<SignalRRuleEngineClient>> _loggerMock;

    public SignalRRuleEngineClientTests()
    {
        _loggerMock = new Mock<ILogger<SignalRRuleEngineClient>>();
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var options = new RuleEngineConnectionOptions
        {
            SignalRHub = "http://localhost:5000/sorterhub"
        };

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new SignalRRuleEngineClient(null!, options));
    }

    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new SignalRRuleEngineClient(_loggerMock.Object, null!));
    }

    [Fact]
    public void Constructor_WithEmptySignalRHub_ThrowsArgumentException()
    {
        // Arrange
        var options = new RuleEngineConnectionOptions
        {
            SignalRHub = ""
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => new SignalRRuleEngineClient(_loggerMock.Object, options));
    }

    [Fact]
    public void Constructor_WithNullSignalRHub_ThrowsArgumentException()
    {
        // Arrange
        var options = new RuleEngineConnectionOptions
        {
            SignalRHub = null
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => new SignalRRuleEngineClient(_loggerMock.Object, options));
    }

    [Fact]
    public void IsConnected_InitiallyReturnsFalse()
    {
        // Arrange
        var options = new RuleEngineConnectionOptions
        {
            SignalRHub = "http://localhost:5000/sorterhub"
        };
        using var client = new SignalRRuleEngineClient(_loggerMock.Object, options);

        // Assert
        Assert.False(client.IsConnected);
    }

    [Fact]
    public async Task ConnectAsync_WithInvalidHub_ReturnsFalse()
    {
        // Arrange
        var options = new RuleEngineConnectionOptions
        {
            SignalRHub = "http://localhost:19999/invalidhub",
            SignalR = new SignalROptions
            {
                HandshakeTimeout = 1
            }
        };
        using var client = new SignalRRuleEngineClient(_loggerMock.Object, options);

        // Act
        var result = await client.ConnectAsync();

        // Assert
        Assert.False(result);
        Assert.False(client.IsConnected);
    }

    [Fact]
    public async Task DisconnectAsync_WhenNotConnected_CompletesSuccessfully()
    {
        // Arrange
        var options = new RuleEngineConnectionOptions
        {
            SignalRHub = "http://localhost:5000/sorterhub"
        };
        using var client = new SignalRRuleEngineClient(_loggerMock.Object, options);

        // Act & Assert (should not throw)
        await client.DisconnectAsync();
        Assert.False(client.IsConnected);
    }

    [Fact]
    public async Task NotifyParcelDetectedAsync_WithInvalidParcelId_ThrowsArgumentException()
    {
        // Arrange
        var options = new RuleEngineConnectionOptions
        {
            SignalRHub = "http://localhost:5000/sorterhub"
        };
        using var client = new SignalRRuleEngineClient(_loggerMock.Object, options);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => client.NotifyParcelDetectedAsync(0));
        await Assert.ThrowsAsync<ArgumentException>(() => client.NotifyParcelDetectedAsync(-1));
    }

    [Fact]
    public async Task NotifyParcelDetectedAsync_WhenNotConnected_ReturnsFalse()
    {
        // Arrange
        var options = new RuleEngineConnectionOptions
        {
            SignalRHub = "http://localhost:19999/invalidhub",
            SignalR = new SignalROptions
            {
                HandshakeTimeout = 1
            }
        };
        using var client = new SignalRRuleEngineClient(_loggerMock.Object, options);
        var parcelId = 123456789L;

        // Act
        var result = await client.NotifyParcelDetectedAsync(parcelId);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task RequestChuteAssignmentAsync_WithInvalidParcelId_ThrowsArgumentException()
    {
        // Arrange
        var options = new RuleEngineConnectionOptions
        {
            SignalRHub = "http://localhost:5000/sorterhub"
        };
        using var client = new SignalRRuleEngineClient(_loggerMock.Object, options);

        // Act & Assert
#pragma warning disable CS0618
        await Assert.ThrowsAsync<ArgumentException>(() => client.RequestChuteAssignmentAsync(0));
        await Assert.ThrowsAsync<ArgumentException>(() => client.RequestChuteAssignmentAsync(-1));
#pragma warning restore CS0618
    }

    [Fact]
    public async Task RequestChuteAssignmentAsync_WhenNotConnected_ReturnsErrorResponse()
    {
        // Arrange
        var options = new RuleEngineConnectionOptions
        {
            SignalRHub = "http://localhost:19999/invalidhub",
            SignalR = new SignalROptions
            {
                HandshakeTimeout = 1
            }
        };
        using var client = new SignalRRuleEngineClient(_loggerMock.Object, options);
        var parcelId = 123456789L;

        // Act
#pragma warning disable CS0618
        var response = await client.RequestChuteAssignmentAsync(parcelId);
#pragma warning restore CS0618

        // Assert
        Assert.NotNull(response);
        Assert.False(response.IsSuccess);
        Assert.Equal(WellKnownChuteIds.DefaultException, response.ChuteId);
        Assert.Equal(parcelId, response.ParcelId);
    }

    [Fact]
    public void ChuteAssignmentReceived_EventCanBeSubscribed()
    {
        // Arrange
        var options = new RuleEngineConnectionOptions
        {
            SignalRHub = "http://localhost:5000/sorterhub"
        };
        using var client = new SignalRRuleEngineClient(_loggerMock.Object, options);
        var eventRaised = false;

        // Act
        client.ChuteAssignmentReceived += (sender, args) =>
        {
            eventRaised = true;
        };

        // Assert - just verify event can be subscribed without error
        Assert.False(eventRaised); // Event not raised yet
    }

    [Fact]
    public void Constructor_ConfiguresAutoReconnect_WhenEnabled()
    {
        // Arrange
        var options = new RuleEngineConnectionOptions
        {
            SignalRHub = "http://localhost:5000/sorterhub",
            EnableAutoReconnect = true,
            SignalR = new SignalROptions
            {
                ReconnectIntervals = new[] { 1000, 2000, 3000 }
            }
        };

        // Act & Assert (should not throw)
        using var client = new SignalRRuleEngineClient(_loggerMock.Object, options);
        Assert.NotNull(client);
    }

    [Fact]
    public void Constructor_ConfiguresTimeouts()
    {
        // Arrange
        var options = new RuleEngineConnectionOptions
        {
            SignalRHub = "http://localhost:5000/sorterhub",
            SignalR = new SignalROptions
            {
                HandshakeTimeout = 5,
                KeepAliveInterval = 10,
                ServerTimeout = 20
            }
        };

        // Act & Assert (should not throw)
        using var client = new SignalRRuleEngineClient(_loggerMock.Object, options);
        Assert.NotNull(client);
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        // Arrange
        var options = new RuleEngineConnectionOptions
        {
            SignalRHub = "http://localhost:5000/sorterhub"
        };
        var client = new SignalRRuleEngineClient(_loggerMock.Object, options);

        // Act & Assert (should not throw)
        client.Dispose();
        client.Dispose();
    }
}
