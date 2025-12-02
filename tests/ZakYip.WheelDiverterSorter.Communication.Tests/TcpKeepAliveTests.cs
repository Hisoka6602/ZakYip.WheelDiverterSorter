using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ZakYip.WheelDiverterSorter.Communication.Clients;
using ZakYip.WheelDiverterSorter.Communication.Configuration;
using ZakYip.WheelDiverterSorter.Communication.Servers;
using ZakYip.WheelDiverterSorter.Core.Enums.Communication;
using ZakYip.WheelDiverterSorter.Core.Utilities;

namespace ZakYip.WheelDiverterSorter.Communication.Tests;

/// <summary>
/// TCP KeepAlive功能测试
/// </summary>
/// <remarks>
/// 测试TCP KeepAlive配置和功能，防止功能退化
/// </remarks>
public class TcpKeepAliveTests : IDisposable
{
    private readonly Mock<ILogger<TcpRuleEngineClient>> _clientLoggerMock;
    private readonly Mock<ILogger<TcpRuleEngineServer>> _serverLoggerMock;
    private readonly Mock<ISystemClock> _systemClockMock;
    private readonly DateTime _testTime = new(2025, 12, 2, 12, 0, 0);
    private readonly List<IDisposable> _disposables = new();

    public TcpKeepAliveTests()
    {
        _clientLoggerMock = new Mock<ILogger<TcpRuleEngineClient>>();
        _serverLoggerMock = new Mock<ILogger<TcpRuleEngineServer>>();
        _systemClockMock = new Mock<ISystemClock>();
        _systemClockMock.Setup(x => x.LocalNow).Returns(_testTime);
        _systemClockMock.Setup(x => x.LocalNowOffset).Returns(new DateTimeOffset(_testTime));
    }

    [Fact]
    public void TcpOptions_DefaultValues_ShouldEnableKeepAlive()
    {
        // Arrange & Act
        var options = new TcpOptions();

        // Assert
        Assert.True(options.EnableKeepAlive, "KeepAlive should be enabled by default");
        Assert.Equal(60, options.KeepAliveTime);
        Assert.Equal(10, options.KeepAliveInterval);
        Assert.Equal(3, options.KeepAliveRetryCount);
    }

    [Fact]
    public void TcpOptions_CanDisableKeepAlive()
    {
        // Arrange & Act
        var options = new TcpOptions
        {
            EnableKeepAlive = false
        };

        // Assert
        Assert.False(options.EnableKeepAlive);
    }

    [Fact]
    public void TcpOptions_CanCustomizeKeepAliveParameters()
    {
        // Arrange & Act
        var options = new TcpOptions
        {
            EnableKeepAlive = true,
            KeepAliveTime = 120,
            KeepAliveInterval = 20,
            KeepAliveRetryCount = 5
        };

        // Assert
        Assert.True(options.EnableKeepAlive);
        Assert.Equal(120, options.KeepAliveTime);
        Assert.Equal(20, options.KeepAliveInterval);
        Assert.Equal(5, options.KeepAliveRetryCount);
    }

    [Fact]
    public async Task TcpClient_WithKeepAliveEnabled_ShouldConnectSuccessfully()
    {
        // Arrange
        var port = GetAvailablePort();
        var serverOptions = new RuleEngineConnectionOptions
        {
            Mode = CommunicationMode.Tcp,
            ConnectionMode = ConnectionMode.Server,
            TcpServer = $"127.0.0.1:{port}",
            TimeoutMs = 5000,
            Tcp = new TcpOptions
            {
                EnableKeepAlive = true,
                KeepAliveTime = 60,
                KeepAliveInterval = 10
            }
        };

        var server = new TcpRuleEngineServer(
            _serverLoggerMock.Object,
            serverOptions,
            _systemClockMock.Object);
        _disposables.Add(server);

        var clientOptions = new RuleEngineConnectionOptions
        {
            Mode = CommunicationMode.Tcp,
            ConnectionMode = ConnectionMode.Client,
            TcpServer = $"127.0.0.1:{port}",
            TimeoutMs = 5000,
            Tcp = new TcpOptions
            {
                EnableKeepAlive = true,
                KeepAliveTime = 60,
                KeepAliveInterval = 10
            }
        };

        var client = new TcpRuleEngineClient(
            _clientLoggerMock.Object,
            clientOptions,
            _systemClockMock.Object);
        _disposables.Add(client);

        // Act
        await server.StartAsync();
        await Task.Delay(500);
        var connected = await client.ConnectAsync();

        // Assert
        Assert.True(connected, "Client should connect successfully with KeepAlive enabled");
        Assert.True(client.IsConnected);

        // Cleanup
        await client.DisconnectAsync();
        await server.StopAsync();
    }

    [Fact]
    public async Task TcpClient_WithKeepAliveDisabled_ShouldConnectSuccessfully()
    {
        // Arrange
        var port = GetAvailablePort();
        var serverOptions = new RuleEngineConnectionOptions
        {
            Mode = CommunicationMode.Tcp,
            ConnectionMode = ConnectionMode.Server,
            TcpServer = $"127.0.0.1:{port}",
            TimeoutMs = 5000,
            Tcp = new TcpOptions
            {
                EnableKeepAlive = false
            }
        };

        var server = new TcpRuleEngineServer(
            _serverLoggerMock.Object,
            serverOptions,
            _systemClockMock.Object);
        _disposables.Add(server);

        var clientOptions = new RuleEngineConnectionOptions
        {
            Mode = CommunicationMode.Tcp,
            ConnectionMode = ConnectionMode.Client,
            TcpServer = $"127.0.0.1:{port}",
            TimeoutMs = 5000,
            Tcp = new TcpOptions
            {
                EnableKeepAlive = false
            }
        };

        var client = new TcpRuleEngineClient(
            _clientLoggerMock.Object,
            clientOptions,
            _systemClockMock.Object);
        _disposables.Add(client);

        // Act
        await server.StartAsync();
        await Task.Delay(500);
        var connected = await client.ConnectAsync();

        // Assert
        Assert.True(connected, "Client should connect successfully with KeepAlive disabled");
        Assert.True(client.IsConnected);

        // Cleanup
        await client.DisconnectAsync();
        await server.StopAsync();
    }

    [Theory]
    [InlineData(30, 5, 2)]
    [InlineData(120, 20, 5)]
    [InlineData(180, 30, 10)]
    public void TcpOptions_ShouldAcceptVariousKeepAliveValues(
        int keepAliveTime,
        int keepAliveInterval,
        int keepAliveRetryCount)
    {
        // Arrange & Act
        var options = new TcpOptions
        {
            EnableKeepAlive = true,
            KeepAliveTime = keepAliveTime,
            KeepAliveInterval = keepAliveInterval,
            KeepAliveRetryCount = keepAliveRetryCount
        };

        // Assert
        Assert.Equal(keepAliveTime, options.KeepAliveTime);
        Assert.Equal(keepAliveInterval, options.KeepAliveInterval);
        Assert.Equal(keepAliveRetryCount, options.KeepAliveRetryCount);
    }

    private static int GetAvailablePort()
    {
        return Random.Shared.Next(50000, 60000);
    }

    public void Dispose()
    {
        foreach (var disposable in _disposables)
        {
            try
            {
                disposable.Dispose();
            }
            catch
            {
                // Ignore disposal errors in tests
            }
        }
        _disposables.Clear();
    }
}
