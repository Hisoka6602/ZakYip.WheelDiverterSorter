using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ZakYip.WheelDiverterSorter.Communication.Clients;
using ZakYip.WheelDiverterSorter.Communication.Configuration;
using ZakYip.WheelDiverterSorter.Communication.Servers;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Core.Enums.Communication;

namespace ZakYip.WheelDiverterSorter.Communication.Tests.Integration;

/// <summary>
/// MQTT协议集成测试 - 测试Client和Server模式的实际连接
/// </summary>
public class MqttConnectionIntegrationTests : IDisposable
{
    private readonly Mock<ILogger<MqttRuleEngineClient>> _clientLoggerMock;
    private readonly Mock<ILogger<MqttRuleEngineServer>> _serverLoggerMock;
    private readonly Mock<ISystemClock> _systemClockMock;
    private readonly DateTime _testTime = new(2025, 11, 24, 12, 0, 0);
    private readonly List<IDisposable> _disposables = new();

    public MqttConnectionIntegrationTests()
    {
        _clientLoggerMock = new Mock<ILogger<MqttRuleEngineClient>>();
        _serverLoggerMock = new Mock<ILogger<MqttRuleEngineServer>>();
        _systemClockMock = new Mock<ISystemClock>();
        _systemClockMock.Setup(x => x.LocalNow).Returns(_testTime);
        _systemClockMock.Setup(x => x.LocalNowOffset).Returns(new DateTimeOffset(_testTime));
    }

    [Fact]
    public async Task MqttServer_ShouldStartAndAcceptConnection()
    {
        // Arrange
        var port = GetAvailablePort();
        var serverOptions = new RuleEngineConnectionOptions
        {
            Mode = CommunicationMode.Mqtt,
            ConnectionMode = Core.Enums.Communication.ConnectionMode.Server,
            MqttBroker = $"localhost:{port}",
            MqttTopic = "test/sorting",
            TimeoutMs = 5000
        };

        var server = new MqttRuleEngineServer(
            _serverLoggerMock.Object,
            serverOptions,
            _systemClockMock.Object);
        _disposables.Add(server);

        var clientOptions = new RuleEngineConnectionOptions
        {
            Mode = CommunicationMode.Mqtt,
            ConnectionMode = Core.Enums.Communication.ConnectionMode.Client,
            MqttBroker = $"mqtt://localhost:{port}",
            MqttTopic = "test/sorting",
            TimeoutMs = 5000,
            Mqtt = new MqttOptions
            {
                ClientIdPrefix = "test-client",
                CleanSession = true
            }
        };

        var client = new MqttRuleEngineClient(
            _clientLoggerMock.Object,
            clientOptions,
            _systemClockMock.Object);
        _disposables.Add(client);

        // Act
        await server.StartAsync();
        await Task.Delay(1000); // Give server more time to start

        var connected = await client.ConnectAsync();

        // Assert
        Assert.True(server.IsRunning, "Server should be running");
        Assert.True(connected, "Client should connect successfully");
        Assert.True(client.IsConnected, "Client IsConnected should be true");

        // Give time for server to register the connection
        await Task.Delay(1000);
        Assert.True(server.ConnectedClientsCount >= 1, "Server should have at least 1 connected client");

        // Cleanup
        await client.DisconnectAsync();
        await server.StopAsync();
    }

    [Fact]
    public async Task MqttClient_ShouldReportNotConnected_WhenServerNotAvailable()
    {
        // Arrange
        var port = GetAvailablePort();
        var clientOptions = new RuleEngineConnectionOptions
        {
            Mode = CommunicationMode.Mqtt,
            ConnectionMode = Core.Enums.Communication.ConnectionMode.Client,
            MqttBroker = $"mqtt://localhost:{port}",
            MqttTopic = "test/sorting",
            TimeoutMs = 2000,
            Mqtt = new MqttOptions
            {
                ClientIdPrefix = "test-client",
                CleanSession = true
            }
        };

        var client = new MqttRuleEngineClient(
            _clientLoggerMock.Object,
            clientOptions,
            _systemClockMock.Object);
        _disposables.Add(client);

        // Act
        var connected = await client.ConnectAsync();

        // Assert
        Assert.False(connected, "Client should fail to connect");
        Assert.False(client.IsConnected, "Client IsConnected should be false");
    }

    [Fact]
    public async Task MqttServer_ShouldStopGracefully()
    {
        // Arrange
        var port = GetAvailablePort();
        var serverOptions = new RuleEngineConnectionOptions
        {
            Mode = CommunicationMode.Mqtt,
            ConnectionMode = Core.Enums.Communication.ConnectionMode.Server,
            MqttBroker = $"localhost:{port}",
            MqttTopic = "test/sorting",
            TimeoutMs = 5000
        };

        var server = new MqttRuleEngineServer(
            _serverLoggerMock.Object,
            serverOptions,
            _systemClockMock.Object);
        _disposables.Add(server);

        // Act
        await server.StartAsync();
        await Task.Delay(500);
        Assert.True(server.IsRunning);

        await server.StopAsync();
        await Task.Delay(500);

        // Assert
        Assert.False(server.IsRunning, "Server should not be running after stop");
    }

    [Fact]
    public async Task MqttClient_CanConnectToServer()
    {
        // Arrange
        var port = GetAvailablePort();
        var serverOptions = new RuleEngineConnectionOptions
        {
            Mode = CommunicationMode.Mqtt,
            ConnectionMode = Core.Enums.Communication.ConnectionMode.Server,
            MqttBroker = $"localhost:{port}",
            MqttTopic = "test/sorting",
            TimeoutMs = 5000
        };

        var server = new MqttRuleEngineServer(
            _serverLoggerMock.Object,
            serverOptions,
            _systemClockMock.Object);
        _disposables.Add(server);

        var clientOptions = new RuleEngineConnectionOptions
        {
            Mode = CommunicationMode.Mqtt,
            ConnectionMode = Core.Enums.Communication.ConnectionMode.Client,
            MqttBroker = $"mqtt://localhost:{port}",
            MqttTopic = "test/sorting",
            TimeoutMs = 5000,
            Mqtt = new MqttOptions
            {
                ClientIdPrefix = "test-receiver",
                CleanSession = true
            }
        };

        var client = new MqttRuleEngineClient(
            _clientLoggerMock.Object,
            clientOptions,
            _systemClockMock.Object);
        _disposables.Add(client);

        // Act
        await server.StartAsync();
        await Task.Delay(1000);

        var connected = await client.ConnectAsync();
        await Task.Delay(1000);

        // Assert
        Assert.True(connected, "Client should connect successfully");
        Assert.True(client.IsConnected, "Client should be connected");
        Assert.True(server.IsRunning, "Server should be running");
        Assert.True(server.ConnectedClientsCount >= 1, "Server should have at least 1 connected client");

        // Cleanup
        await client.DisconnectAsync();
        await server.StopAsync();
    }

    private static int GetAvailablePort()
    {
        // Use a random port in the dynamic/private port range (49152-65535)
        // Use different range for MQTT to avoid conflicts
        return Random.Shared.Next(51883, 61883);
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
