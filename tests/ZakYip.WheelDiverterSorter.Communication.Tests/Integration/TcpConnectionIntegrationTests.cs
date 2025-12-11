using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ZakYip.WheelDiverterSorter.Communication.Clients;
using ZakYip.WheelDiverterSorter.Communication.Configuration;
using ZakYip.WheelDiverterSorter.Communication.Servers;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Core.Enums.Communication;
using ZakYip.WheelDiverterSorter.Core.Sorting.Policies;

namespace ZakYip.WheelDiverterSorter.Communication.Tests.Integration;

/// <summary>
/// TCP协议集成测试 - 测试Client和Server模式的实际连接
/// </summary>
public class TcpConnectionIntegrationTests : IDisposable
{
    private readonly Mock<ILogger<TcpRuleEngineClient>> _clientLoggerMock;
    private readonly Mock<ILogger<TcpRuleEngineServer>> _serverLoggerMock;
    private readonly Mock<ISystemClock> _systemClockMock;
    private readonly DateTime _testTime = new(2025, 11, 24, 12, 0, 0);
    private readonly List<IDisposable> _disposables = new();

    public TcpConnectionIntegrationTests()
    {
        _clientLoggerMock = new Mock<ILogger<TcpRuleEngineClient>>();
        _serverLoggerMock = new Mock<ILogger<TcpRuleEngineServer>>();
        _systemClockMock = new Mock<ISystemClock>();
        _systemClockMock.Setup(x => x.LocalNow).Returns(_testTime);
        _systemClockMock.Setup(x => x.LocalNowOffset).Returns(new DateTimeOffset(_testTime));
    }

    [Fact]
    public async Task TcpServer_ShouldStartAndAcceptConnection()
    {
        // Arrange
        var port = GetAvailablePort();
        var serverOptions = new UpstreamConnectionOptions
        {
            Mode = CommunicationMode.Tcp,
            ConnectionMode = Core.Enums.Communication.ConnectionMode.Server,
            TcpServer = $"127.0.0.1:{port}",
            TimeoutMs = 5000
        };

        var server = new TcpRuleEngineServer(
            _serverLoggerMock.Object,
            serverOptions,
            _systemClockMock.Object);
        _disposables.Add(server);

        var clientOptions = new UpstreamConnectionOptions
        {
            Mode = CommunicationMode.Tcp,
            ConnectionMode = Core.Enums.Communication.ConnectionMode.Client,
            TcpServer = $"127.0.0.1:{port}",
            TimeoutMs = 5000
        };

        var client = new TcpRuleEngineClient(
            _clientLoggerMock.Object,
            clientOptions,
            _systemClockMock.Object);
        _disposables.Add(client);

        // Act
        await server.StartAsync();
        await Task.Delay(500); // Give server time to start

        var connected = await client.ConnectAsync();

        // Assert
        Assert.True(server.IsRunning, "Server should be running");
        Assert.True(connected, "Client should connect successfully");
        Assert.True(client.IsConnected, "Client IsConnected should be true");

        // Give time for server to register the connection
        await Task.Delay(500);
        Assert.Equal(1, server.ConnectedClientsCount);

        // Cleanup
        await client.DisconnectAsync();
        await server.StopAsync();
    }

    [Fact]
    public async Task TcpClient_ShouldReportNotConnected_WhenServerNotAvailable()
    {
        // Arrange
        var port = GetAvailablePort();
        var clientOptions = new UpstreamConnectionOptions
        {
            Mode = CommunicationMode.Tcp,
            ConnectionMode = Core.Enums.Communication.ConnectionMode.Client,
            TcpServer = $"127.0.0.1:{port}",
            TimeoutMs = 2000
        };

        var client = new TcpRuleEngineClient(
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
    public async Task TcpServer_ShouldAcceptMultipleClients()
    {
        // Arrange
        var port = GetAvailablePort();
        var serverOptions = new UpstreamConnectionOptions
        {
            Mode = CommunicationMode.Tcp,
            ConnectionMode = Core.Enums.Communication.ConnectionMode.Server,
            TcpServer = $"127.0.0.1:{port}",
            TimeoutMs = 5000
        };

        var server = new TcpRuleEngineServer(
            _serverLoggerMock.Object,
            serverOptions,
            _systemClockMock.Object);
        _disposables.Add(server);

        var client1Options = new UpstreamConnectionOptions
        {
            Mode = CommunicationMode.Tcp,
            ConnectionMode = Core.Enums.Communication.ConnectionMode.Client,
            TcpServer = $"127.0.0.1:{port}",
            TimeoutMs = 5000
        };

        var client1 = new TcpRuleEngineClient(
            _clientLoggerMock.Object,
            client1Options,
            _systemClockMock.Object);
        _disposables.Add(client1);

        var client2 = new TcpRuleEngineClient(
            _clientLoggerMock.Object,
            client1Options,
            _systemClockMock.Object);
        _disposables.Add(client2);

        // Act
        await server.StartAsync();
        await Task.Delay(500);

        var connected1 = await client1.ConnectAsync();
        var connected2 = await client2.ConnectAsync();

        // Assert
        Assert.True(connected1, "Client 1 should connect successfully");
        Assert.True(connected2, "Client 2 should connect successfully");
        Assert.True(client1.IsConnected, "Client 1 IsConnected should be true");
        Assert.True(client2.IsConnected, "Client 2 IsConnected should be true");

        await Task.Delay(500);
        Assert.Equal(2, server.ConnectedClientsCount);

        // Cleanup
        await client1.DisconnectAsync();
        await client2.DisconnectAsync();
        await server.StopAsync();
    }

    [Fact]
    public async Task TcpServer_ShouldStopGracefully()
    {
        // Arrange
        var port = GetAvailablePort();
        var serverOptions = new UpstreamConnectionOptions
        {
            Mode = CommunicationMode.Tcp,
            ConnectionMode = Core.Enums.Communication.ConnectionMode.Server,
            TcpServer = $"127.0.0.1:{port}",
            TimeoutMs = 5000
        };

        var server = new TcpRuleEngineServer(
            _serverLoggerMock.Object,
            serverOptions,
            _systemClockMock.Object);
        _disposables.Add(server);

        // Act
        await server.StartAsync();
        await Task.Delay(300);
        Assert.True(server.IsRunning);

        await server.StopAsync();
        await Task.Delay(300);

        // Assert
        Assert.False(server.IsRunning, "Server should not be running after stop");
        Assert.Equal(0, server.ConnectedClientsCount);
    }

    [Fact]
    public async Task TcpServer_ShouldReceiveParcelNotification()
    {
        // Arrange
        var port = GetAvailablePort();
        var serverOptions = new UpstreamConnectionOptions
        {
            Mode = CommunicationMode.Tcp,
            ConnectionMode = Core.Enums.Communication.ConnectionMode.Server,
            TcpServer = $"127.0.0.1:{port}",
            TimeoutMs = 5000
        };

        var server = new TcpRuleEngineServer(
            _serverLoggerMock.Object,
            serverOptions,
            _systemClockMock.Object);
        _disposables.Add(server);

        var parcelNotificationReceived = false;
        var receivedParcelId = 0L;

        server.ParcelNotificationReceived += (sender, args) =>
        {
            parcelNotificationReceived = true;
            receivedParcelId = args.ParcelId;
        };

        // Act
        await server.StartAsync();
        await Task.Delay(500);

        // Manually send a TCP request to the server to simulate client
        using var testClient = new System.Net.Sockets.TcpClient();
        await testClient.ConnectAsync("127.0.0.1", port);
        await Task.Delay(300);

        var testParcelId = 1234567890L;
        var request = new { ParcelId = testParcelId, DetectionTime = _systemClockMock.Object.LocalNowOffset };
        var requestJson = System.Text.Json.JsonSerializer.Serialize(request);
        var requestBytes = System.Text.Encoding.UTF8.GetBytes(requestJson + "\n");

        var stream = testClient.GetStream();
        await stream.WriteAsync(requestBytes);
        await stream.FlushAsync();

        // Wait for server to process
        await Task.Delay(1000);

        // Assert
        Assert.True(parcelNotificationReceived, "Server should receive parcel notification");
        Assert.Equal(testParcelId, receivedParcelId);

        // Cleanup
        testClient.Close();
        await server.StopAsync();
    }

    private static int GetAvailablePort()
    {
        // Use a random port in the dynamic/private port range (49152-65535)
        return Random.Shared.Next(50000, 60000);
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
