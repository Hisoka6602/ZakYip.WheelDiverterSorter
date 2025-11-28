using Microsoft.AspNetCore.SignalR;
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
/// SignalR协议集成测试 - 测试Client和Server模式的实际连接
/// 注意：SignalR Server需要ASP.NET Core Host，这里主要测试客户端连接失败的场景
/// 完整的Server测试需要在Host.IntegrationTests中进行
/// </summary>
public class SignalRConnectionIntegrationTests : IDisposable
{
    private readonly Mock<ILogger<SignalRRuleEngineClient>> _clientLoggerMock;
    private readonly Mock<ILogger<SignalRRuleEngineServer>> _serverLoggerMock;
    private readonly Mock<ISystemClock> _systemClockMock;
    private readonly DateTime _testTime = new(2025, 11, 24, 12, 0, 0);
    private readonly List<IDisposable> _disposables = new();

    public SignalRConnectionIntegrationTests()
    {
        _clientLoggerMock = new Mock<ILogger<SignalRRuleEngineClient>>();
        _serverLoggerMock = new Mock<ILogger<SignalRRuleEngineServer>>();
        _systemClockMock = new Mock<ISystemClock>();
        _systemClockMock.Setup(x => x.LocalNow).Returns(_testTime);
        _systemClockMock.Setup(x => x.LocalNowOffset).Returns(new DateTimeOffset(_testTime));
    }

    [Fact]
    public async Task SignalRServer_ShouldStartSuccessfully()
    {
        // Arrange
        var serverOptions = new RuleEngineConnectionOptions
        {
            Mode = CommunicationMode.SignalR,
            ConnectionMode = Core.Enums.Communication.ConnectionMode.Server,
            SignalRHub = "http://localhost:5999/ruleengine",
            TimeoutMs = 5000
        };

        var mockHubContext = new Mock<IHubContext<RuleEngineHub>>();
        var server = new SignalRRuleEngineServer(
            _serverLoggerMock.Object,
            serverOptions,
            _systemClockMock.Object,
            mockHubContext.Object);
        _disposables.Add(server);

        // Act
        await server.StartAsync();

        // Assert
        Assert.True(server.IsRunning, "Server should be running");

        // Cleanup
        await server.StopAsync();
    }

    [Fact]
    public async Task SignalRClient_ShouldReportNotConnected_WhenServerNotAvailable()
    {
        // Arrange
        var port = GetAvailablePort();
        var clientOptions = new RuleEngineConnectionOptions
        {
            Mode = CommunicationMode.SignalR,
            ConnectionMode = Core.Enums.Communication.ConnectionMode.Client,
            SignalRHub = $"http://localhost:{port}/ruleengine",
            TimeoutMs = 2000,
            EnableAutoReconnect = false, // Disable auto-reconnect for faster test
            SignalR = new SignalROptions
            {
                HandshakeTimeout = 2,
                ServerTimeout = 2,
                KeepAliveInterval = 1
            }
        };

        var client = new SignalRRuleEngineClient(
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
    public async Task SignalRServer_ShouldStopGracefully()
    {
        // Arrange
        var serverOptions = new RuleEngineConnectionOptions
        {
            Mode = CommunicationMode.SignalR,
            ConnectionMode = Core.Enums.Communication.ConnectionMode.Server,
            SignalRHub = "http://localhost:5998/ruleengine",
            TimeoutMs = 5000
        };

        var mockHubContext = new Mock<IHubContext<RuleEngineHub>>();
        var server = new SignalRRuleEngineServer(
            _serverLoggerMock.Object,
            serverOptions,
            _systemClockMock.Object,
            mockHubContext.Object);
        _disposables.Add(server);

        // Act
        await server.StartAsync();
        Assert.True(server.IsRunning);

        await server.StopAsync();

        // Assert
        Assert.False(server.IsRunning, "Server should not be running after stop");
    }

    [Fact]
    public async Task SignalRServer_ShouldBroadcastChuteAssignment()
    {
        // Arrange
        var serverOptions = new RuleEngineConnectionOptions
        {
            Mode = CommunicationMode.SignalR,
            ConnectionMode = Core.Enums.Communication.ConnectionMode.Server,
            SignalRHub = "http://localhost:5996/ruleengine",
            TimeoutMs = 5000
        };

        var mockClients = new Mock<IHubClients>();
        var mockClientProxy = new Mock<IClientProxy>();
        
        mockClients.Setup(c => c.All).Returns(mockClientProxy.Object);
        
        var mockHubContext = new Mock<IHubContext<RuleEngineHub>>();
        mockHubContext.Setup(h => h.Clients).Returns(mockClients.Object);

        var server = new SignalRRuleEngineServer(
            _serverLoggerMock.Object,
            serverOptions,
            _systemClockMock.Object,
            mockHubContext.Object);
        _disposables.Add(server);

        var broadcastCalled = false;
        mockClientProxy
            .Setup(c => c.SendCoreAsync(
                It.IsAny<string>(),
                It.IsAny<object[]>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, object[], CancellationToken>((method, args, ct) =>
            {
                if (method == "ReceiveChuteAssignment")
                {
                    broadcastCalled = true;
                }
            })
            .Returns(Task.CompletedTask);

        // Act
        await server.StartAsync();
        await Task.Delay(300);

        var testParcelId = 5555555555L;
        await server.BroadcastChuteAssignmentAsync(testParcelId, "3");
        await Task.Delay(300);

        // Assert
        Assert.True(server.IsRunning, "Server should be running");
        Assert.True(broadcastCalled, "Broadcast should have been called");

        // Cleanup
        await server.StopAsync();
    }

    [Fact]
    public async Task SignalRClient_ShouldInitializeConnectionBuilder()
    {
        // Arrange
        var clientOptions = new RuleEngineConnectionOptions
        {
            Mode = CommunicationMode.SignalR,
            ConnectionMode = Core.Enums.Communication.ConnectionMode.Client,
            SignalRHub = "http://localhost:5997/ruleengine",
            TimeoutMs = 2000,
            EnableAutoReconnect = true,
            SignalR = new SignalROptions
            {
                HandshakeTimeout = 10,
                ServerTimeout = 30,
                KeepAliveInterval = 15,
                ReconnectIntervals = new[] { 1000, 2000, 3000 }
            }
        };

        // Act - Constructor should not throw
        var client = new SignalRRuleEngineClient(
            _clientLoggerMock.Object,
            clientOptions,
            _systemClockMock.Object);
        _disposables.Add(client);

        // Assert - Client should be created successfully
        Assert.NotNull(client);
        Assert.False(client.IsConnected, "Client should not be connected initially");
    }

    private static int GetAvailablePort()
    {
        // Use a random port in the dynamic/private port range (49152-65535)
        return Random.Shared.Next(52000, 62000);
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
