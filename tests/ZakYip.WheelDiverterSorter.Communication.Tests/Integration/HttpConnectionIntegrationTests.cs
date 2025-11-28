using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ZakYip.WheelDiverterSorter.Communication.Clients;
using ZakYip.WheelDiverterSorter.Communication.Configuration;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Core.Enums.Communication;

namespace ZakYip.WheelDiverterSorter.Communication.Tests.Integration;

/// <summary>
/// HTTP协议集成测试 - 测试Client模式的实际连接
/// HTTP模式不支持Server模式，仅测试客户端连接
/// </summary>
public class HttpConnectionIntegrationTests : IDisposable
{
    private readonly Mock<ILogger<HttpRuleEngineClient>> _clientLoggerMock;
    private readonly Mock<ISystemClock> _systemClockMock;
    private readonly DateTime _testTime = new(2025, 11, 24, 12, 0, 0);
    private readonly List<IDisposable> _disposables = new();

    public HttpConnectionIntegrationTests()
    {
        _clientLoggerMock = new Mock<ILogger<HttpRuleEngineClient>>();
        _systemClockMock = new Mock<ISystemClock>();
        _systemClockMock.Setup(x => x.LocalNow).Returns(_testTime);
        _systemClockMock.Setup(x => x.LocalNowOffset).Returns(new DateTimeOffset(_testTime));
    }

    [Fact]
    public async Task HttpClient_ShouldReportNotConnected_WhenServerNotAvailable()
    {
        // Arrange
        var port = GetAvailablePort();
        var clientOptions = new RuleEngineConnectionOptions
        {
            Mode = CommunicationMode.Http,
            ConnectionMode = Core.Enums.Communication.ConnectionMode.Client,
            HttpApi = $"http://localhost:{port}/api/ruleengine",
            TimeoutMs = 2000,
            Http = new HttpOptions
            {
                MaxConnectionsPerServer = 10,
                UseHttp2 = false
            }
        };

        var client = new HttpRuleEngineClient(
            _clientLoggerMock.Object,
            clientOptions,
            _systemClockMock.Object);
        _disposables.Add(client);

        // Act
        var connected = await client.ConnectAsync();

        // Assert
        Assert.False(connected, "Client should fail to connect when server is not available");
        Assert.False(client.IsConnected, "Client IsConnected should be false");
    }

    [Fact]
    public async Task HttpClient_ShouldBeCreatedSuccessfully()
    {
        // Arrange
        var clientOptions = new RuleEngineConnectionOptions
        {
            Mode = CommunicationMode.Http,
            ConnectionMode = Core.Enums.Communication.ConnectionMode.Client,
            HttpApi = "http://localhost:9999/api/ruleengine",
            TimeoutMs = 5000,
            Http = new HttpOptions
            {
                MaxConnectionsPerServer = 10,
                UseHttp2 = true,
                PooledConnectionIdleTimeout = 60,
                PooledConnectionLifetime = 300
            }
        };

        // Act - Constructor should not throw
        var client = new HttpRuleEngineClient(
            _clientLoggerMock.Object,
            clientOptions,
            _systemClockMock.Object);
        _disposables.Add(client);

        // Assert
        Assert.NotNull(client);
        Assert.False(client.IsConnected, "Client should not be connected initially");
    }

    [Fact]
    public async Task HttpClient_ShouldHandleConnectionFailureGracefully()
    {
        // Arrange
        var clientOptions = new RuleEngineConnectionOptions
        {
            Mode = CommunicationMode.Http,
            ConnectionMode = Core.Enums.Communication.ConnectionMode.Client,
            HttpApi = "http://192.0.2.1:9999/api/ruleengine", // TEST-NET-1, should not be routable
            TimeoutMs = 1000,
            Http = new HttpOptions()
        };

        var client = new HttpRuleEngineClient(
            _clientLoggerMock.Object,
            clientOptions,
            _systemClockMock.Object);
        _disposables.Add(client);

        // Act
        var connected = await client.ConnectAsync();

        // Assert
        Assert.False(connected, "Client should handle connection failure gracefully");
        Assert.False(client.IsConnected, "Client IsConnected should be false after connection failure");
    }

    [Fact]
    public void HttpClient_ShouldThrowWhenHttpApiIsNull()
    {
        // Arrange
        var clientOptions = new RuleEngineConnectionOptions
        {
            Mode = CommunicationMode.Http,
            ConnectionMode = Core.Enums.Communication.ConnectionMode.Client,
            HttpApi = null, // Invalid
            TimeoutMs = 5000
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => new HttpRuleEngineClient(
            _clientLoggerMock.Object,
            clientOptions,
            _systemClockMock.Object));
    }

    private static int GetAvailablePort()
    {
        // Use a random port in the dynamic/private port range (49152-65535)
        return Random.Shared.Next(53000, 63000);
    }

    public void Dispose()
    {
        foreach (var disposable in _disposables)
        {
            disposable.Dispose();
        }
        _disposables.Clear();
    }
}
