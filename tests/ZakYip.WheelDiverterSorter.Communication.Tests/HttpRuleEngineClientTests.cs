using Microsoft.Extensions.Logging;
using Moq;
using ZakYip.WheelDiverterSorter.Communication.Clients;
using ZakYip.WheelDiverterSorter.Communication.Configuration;
using ZakYip.WheelDiverterSorter.Core.LineModel;
using ZakYip.WheelDiverterSorter.Core.Enums.Communication;
using ZakYip.WheelDiverterSorter.Core.Enums.Conveyor;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Enums.IoBinding;
using ZakYip.WheelDiverterSorter.Core.Enums.Routing;
using ZakYip.WheelDiverterSorter.Core.Enums.Sensors;
using ZakYip.WheelDiverterSorter.Core.Enums.Sorting;
using ZakYip.WheelDiverterSorter.Core.Enums.System;


using ZakYip.WheelDiverterSorter.Core.LineModel.Chutes;namespace ZakYip.WheelDiverterSorter.Communication.Tests;
using ZakYip.WheelDiverterSorter.Core.Enums.Communication;
using ZakYip.WheelDiverterSorter.Core.Enums.Conveyor;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Enums.IoBinding;
using ZakYip.WheelDiverterSorter.Core.Enums.Routing;
using ZakYip.WheelDiverterSorter.Core.Enums.Sensors;
using ZakYip.WheelDiverterSorter.Core.Enums.Sorting;
using ZakYip.WheelDiverterSorter.Core.Enums.System;

/// <summary>
/// HTTP客户端测试：请求、响应、错误处理
/// </summary>
public class HttpRuleEngineClientTests
{
    private readonly Mock<ILogger<HttpRuleEngineClient>> _loggerMock;

    public HttpRuleEngineClientTests()
    {
        _loggerMock = new Mock<ILogger<HttpRuleEngineClient>>();
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var options = new RuleEngineConnectionOptions
        {
            HttpApi = "http://localhost:5000/api/chute"
        };

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new HttpRuleEngineClient(null!, options));
    }

    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new HttpRuleEngineClient(_loggerMock.Object, null!));
    }

    [Fact]
    public void Constructor_WithEmptyHttpApi_ThrowsArgumentException()
    {
        // Arrange
        var options = new RuleEngineConnectionOptions
        {
            HttpApi = ""
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => new HttpRuleEngineClient(_loggerMock.Object, options));
    }

    [Fact]
    public void Constructor_WithNullHttpApi_ThrowsArgumentException()
    {
        // Arrange
        var options = new RuleEngineConnectionOptions
        {
            HttpApi = null
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => new HttpRuleEngineClient(_loggerMock.Object, options));
    }

    [Fact]
    public void IsConnected_AlwaysReturnsTrue()
    {
        // Arrange
        var options = new RuleEngineConnectionOptions
        {
            HttpApi = "http://localhost:5000/api/chute"
        };
        using var client = new HttpRuleEngineClient(_loggerMock.Object, options);

        // Assert - HTTP is stateless, so IsConnected is always true
        Assert.True(client.IsConnected);
    }

    [Fact]
    public async Task ConnectAsync_AlwaysReturnsTrue()
    {
        // Arrange
        var options = new RuleEngineConnectionOptions
        {
            HttpApi = "http://localhost:5000/api/chute"
        };
        using var client = new HttpRuleEngineClient(_loggerMock.Object, options);

        // Act
        var result = await client.ConnectAsync();

        // Assert - HTTP is stateless, so ConnectAsync always succeeds
        Assert.True(result);
    }

    [Fact]
    public async Task DisconnectAsync_CompletesSuccessfully()
    {
        // Arrange
        var options = new RuleEngineConnectionOptions
        {
            HttpApi = "http://localhost:5000/api/chute"
        };
        using var client = new HttpRuleEngineClient(_loggerMock.Object, options);

        // Act & Assert (should not throw)
        await client.DisconnectAsync();
        Assert.True(client.IsConnected); // Still "connected" because HTTP is stateless
    }

    [Fact]
    public async Task NotifyParcelDetectedAsync_WithInvalidParcelId_ThrowsArgumentException()
    {
        // Arrange
        var options = new RuleEngineConnectionOptions
        {
            HttpApi = "http://localhost:5000/api/chute"
        };
        using var client = new HttpRuleEngineClient(_loggerMock.Object, options);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => client.NotifyParcelDetectedAsync(0));
        await Assert.ThrowsAsync<ArgumentException>(() => client.NotifyParcelDetectedAsync(-1));
    }

    [Fact]
    public async Task NotifyParcelDetectedAsync_WithInvalidUrl_ReturnsFalse()
    {
        // Arrange
        var options = new RuleEngineConnectionOptions
        {
            HttpApi = "http://localhost:19999/api/chute",
            TimeoutMs = 1000,
            RetryCount = 0
        };
        using var client = new HttpRuleEngineClient(_loggerMock.Object, options);
        var parcelId = 123456789L;

        // Act
        var result = await client.NotifyParcelDetectedAsync(parcelId);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ChuteAssignmentReceived_EventCanBeSubscribed()
    {
        // Arrange
        var options = new RuleEngineConnectionOptions
        {
            HttpApi = "http://localhost:5000/api/chute"
        };
        using var client = new HttpRuleEngineClient(_loggerMock.Object, options);
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
    public void Constructor_ConfiguresTimeout()
    {
        // Arrange
        var options = new RuleEngineConnectionOptions
        {
            HttpApi = "http://localhost:5000/api/chute",
            TimeoutMs = 10000
        };

        // Act & Assert (should not throw)
        using var client = new HttpRuleEngineClient(_loggerMock.Object, options);
        Assert.NotNull(client);
    }

    [Fact]
    public void Constructor_ConfiguresRetry()
    {
        // Arrange
        var options = new RuleEngineConnectionOptions
        {
            HttpApi = "http://localhost:5000/api/chute",
            RetryCount = 5,
            RetryDelayMs = 500
        };

        // Act & Assert (should not throw)
        using var client = new HttpRuleEngineClient(_loggerMock.Object, options);
        Assert.NotNull(client);
    }

    [Fact]
    public void Constructor_ConfiguresHttp2()
    {
        // Arrange
        var options = new RuleEngineConnectionOptions
        {
            HttpApi = "http://localhost:5000/api/chute",
            Http = new HttpOptions
            {
                UseHttp2 = true
            }
        };

        // Act & Assert (should not throw)
        using var client = new HttpRuleEngineClient(_loggerMock.Object, options);
        Assert.NotNull(client);
    }

    [Fact]
    public void Constructor_ConfiguresConnectionPool()
    {
        // Arrange
        var options = new RuleEngineConnectionOptions
        {
            HttpApi = "http://localhost:5000/api/chute",
            Http = new HttpOptions
            {
                MaxConnectionsPerServer = 10,
                PooledConnectionIdleTimeout = 30,
                PooledConnectionLifetime = 300
            }
        };

        // Act & Assert (should not throw)
        using var client = new HttpRuleEngineClient(_loggerMock.Object, options);
        Assert.NotNull(client);
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        // Arrange
        var options = new RuleEngineConnectionOptions
        {
            HttpApi = "http://localhost:5000/api/chute"
        };
        var client = new HttpRuleEngineClient(_loggerMock.Object, options);

        // Act & Assert (should not throw)
        client.Dispose();
        client.Dispose();
    }

    [Fact]
    public void Constructor_LogsWarningAboutProductionUsage()
    {
        // Arrange
        var options = new RuleEngineConnectionOptions
        {
            HttpApi = "http://localhost:5000/api/chute"
        };

        // Act
        using var client = new HttpRuleEngineClient(_loggerMock.Object, options);

        // Assert - verify warning was logged
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("仅用于测试")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Theory]
    [InlineData("http://localhost:5000/api/chute")]
    [InlineData("https://api.example.com/chute")]
    [InlineData("http://192.168.1.100:8080/api/v1/chute")]
    public void Constructor_WithVariousUrls_InitializesSuccessfully(string apiUrl)
    {
        // Arrange
        var options = new RuleEngineConnectionOptions
        {
            HttpApi = apiUrl
        };

        // Act & Assert (should not throw)
        using var client = new HttpRuleEngineClient(_loggerMock.Object, options);
        Assert.NotNull(client);
        Assert.True(client.IsConnected);
    }
}
