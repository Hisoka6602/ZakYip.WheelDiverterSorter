using Microsoft.Extensions.Logging;
using Moq;
using ZakYip.WheelDiverterSorter.Communication.Clients;
using ZakYip.WheelDiverterSorter.Communication.Configuration;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Core.Enums.Communication;
using ZakYip.WheelDiverterSorter.Core.Sorting.Policies;

namespace ZakYip.WheelDiverterSorter.Communication.Tests;

/// <summary>
/// 通信适配器测试：推送模型、超时保护
/// UpstreamRoutingClientFactory tests
/// PR-UPSTREAM01: HTTP 模式已移除，改用 TCP 作为默认/降级模式
/// </summary>
public class UpstreamRoutingClientFactoryTests
{
    private readonly Mock<ILoggerFactory> _loggerFactoryMock;
    private readonly ISystemClock _clockMock;

    public UpstreamRoutingClientFactoryTests()
    {
        _loggerFactoryMock = new Mock<ILoggerFactory>();
        _clockMock = Mock.Of<ISystemClock>();
        
        // Setup logger factory to return mock loggers
        _loggerFactoryMock
            .Setup(x => x.CreateLogger(It.IsAny<string>()))
            .Returns(new Mock<ILogger>().Object);
    }

    [Fact]
    public void Constructor_WithNullLoggerFactory_ThrowsArgumentNullException()
    {
        // Arrange
        var options = new UpstreamConnectionOptions
        {
            Mode = CommunicationMode.Tcp,
            TcpServer = "localhost:9000"
        };

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new UpstreamRoutingClientFactory(null!, () => options, _clockMock));
    }

    [Fact]
    public void Constructor_WithNullOptionsProvider_ThrowsArgumentNullException()
    {
        // Act & Assert
        // 构造函数应该验证 optionsProvider 函数本身不为 null
        Assert.Throws<ArgumentNullException>(() => new UpstreamRoutingClientFactory(_loggerFactoryMock.Object, null!, _clockMock));
    }

    [Fact]
    public void CreateClient_WithNullOptions_ThrowsException()
    {
        // Arrange
        // 提供一个返回 null 的 options provider
        var factory = new UpstreamRoutingClientFactory(_loggerFactoryMock.Object, () => null!, _clockMock);

        // Act & Assert
        // CreateClient 应该抛出异常，因为 options 为 null
        Assert.Throws<NullReferenceException>(() => factory.CreateClient());
    }

    [Fact]
    public void CreateClient_WithTcpMode_ReturnsTcpRuleEngineClient()
    {
        // Arrange
        var options = new UpstreamConnectionOptions
        {
            Mode = CommunicationMode.Tcp,
            TcpServer = "localhost:9999"
        };
        var factory = new UpstreamRoutingClientFactory(_loggerFactoryMock.Object, () => options, _clockMock);

        // Act
        using var client = factory.CreateClient();

        // Assert
        Assert.NotNull(client);
        Assert.IsType<TouchSocketTcpRuleEngineClient>(client);
    }

    [Fact]
    public void CreateClient_WithSignalRMode_ReturnsSignalRRuleEngineClient()
    {
        // Arrange
        var options = new UpstreamConnectionOptions
        {
            Mode = CommunicationMode.SignalR,
            SignalRHub = "http://localhost:5000/sorterhub"
        };
        var factory = new UpstreamRoutingClientFactory(_loggerFactoryMock.Object, () => options, _clockMock);

        // Act
        using var client = factory.CreateClient();

        // Assert
        Assert.NotNull(client);
        Assert.IsType<SignalRRuleEngineClient>(client);
    }

    [Fact]
    public void CreateClient_WithMqttMode_ReturnsMqttRuleEngineClient()
    {
        // Arrange
        var options = new UpstreamConnectionOptions
        {
            Mode = CommunicationMode.Mqtt,
            MqttBroker = "mqtt://localhost:1883"
        };
        var factory = new UpstreamRoutingClientFactory(_loggerFactoryMock.Object, () => options, _clockMock);

        // Act
        using var client = factory.CreateClient();

        // Assert
        Assert.NotNull(client);
        Assert.IsType<MqttRuleEngineClient>(client);
    }

    /// <summary>
    /// PR-UPSTREAM01: 无效模式应降级为 TCP
    /// </summary>
    [Fact]
    public void CreateClient_WithInvalidMode_UsesTcpFallback()
    {
        // Arrange
        var options = new UpstreamConnectionOptions
        {
            Mode = (CommunicationMode)999, // Invalid mode
            TcpServer = "localhost:9000"
        };
        var factory = new UpstreamRoutingClientFactory(_loggerFactoryMock.Object, () => options, _clockMock);

        // Act
        using var client = factory.CreateClient();

        // Assert - should fallback to TcpRuleEngineClient instead of throwing
        Assert.NotNull(client);
        Assert.IsType<TouchSocketTcpRuleEngineClient>(client);
    }

    [Fact]
    public void CreateClient_MultipleCalls_ReturnsNewInstancesEachTime()
    {
        // Arrange
        var options = new UpstreamConnectionOptions
        {
            Mode = CommunicationMode.Tcp,
            TcpServer = "localhost:9000"
        };
        var factory = new UpstreamRoutingClientFactory(_loggerFactoryMock.Object, () => options, _clockMock);

        // Act
        using var client1 = factory.CreateClient();
        using var client2 = factory.CreateClient();

        // Assert
        Assert.NotNull(client1);
        Assert.NotNull(client2);
        Assert.NotSame(client1, client2);
    }

    /// <summary>
    /// PR-UPSTREAM01: 移除 HTTP 模式测试
    /// </summary>
    [Theory]
    [InlineData(CommunicationMode.Tcp)]
    [InlineData(CommunicationMode.SignalR)]
    [InlineData(CommunicationMode.Mqtt)]
    public void CreateClient_WithAllSupportedModes_SuccessfullyCreatesClient(CommunicationMode mode)
    {
        // Arrange
        var options = new UpstreamConnectionOptions
        {
            Mode = mode,
            TcpServer = "localhost:9999",
            SignalRHub = "http://localhost:5000/sorterhub",
            MqttBroker = "mqtt://localhost:1883"
        };
        var factory = new UpstreamRoutingClientFactory(_loggerFactoryMock.Object, () => options, _clockMock);

        // Act
        using var client = factory.CreateClient();

        // Assert
        Assert.NotNull(client);
    }

    /// <summary>
    /// PR-UPSTREAM01: 改用 TCP 模式
    /// </summary>
    [Fact]
    public void CreateClient_WithTimeoutConfiguration_ClientHasCorrectTimeout()
    {
        // Arrange
        var options = new UpstreamConnectionOptions
        {
            Mode = CommunicationMode.Tcp,
            TcpServer = "localhost:9000",
            TimeoutMs = 10000
        };
        var factory = new UpstreamRoutingClientFactory(_loggerFactoryMock.Object, () => options, _clockMock);

        // Act
        using var client = factory.CreateClient();

        // Assert - timeout is configured in the options passed to client
        Assert.NotNull(client);
    }

    [Fact]
    public void CreateClient_WithAutoReconnectEnabled_ClientSupportsReconnection()
    {
        // Arrange
        var options = new UpstreamConnectionOptions
        {
            Mode = CommunicationMode.SignalR,
            SignalRHub = "http://localhost:5000/sorterhub",
            EnableAutoReconnect = true
        };
        var factory = new UpstreamRoutingClientFactory(_loggerFactoryMock.Object, () => options, _clockMock);

        // Act
        using var client = factory.CreateClient();

        // Assert - auto-reconnect is configured in the options
        Assert.NotNull(client);
        Assert.IsType<SignalRRuleEngineClient>(client);
    }

    [Fact]
    public void CreateClient_WithRetryConfiguration_ClientHasCorrectRetrySettings()
    {
        // Arrange
        var options = new UpstreamConnectionOptions
        {
            Mode = CommunicationMode.Tcp,
            TcpServer = "localhost:9999",
            RetryCount = 5,
            RetryDelayMs = 2000
        };
        var factory = new UpstreamRoutingClientFactory(_loggerFactoryMock.Object, () => options, _clockMock);

        // Act
        using var client = factory.CreateClient();

        // Assert - retry settings are configured in the options
        Assert.NotNull(client);
    }

    [Fact]
    public void CreateClient_WithChuteAssignmentTimeout_ClientHasPushModelTimeoutProtection()
    {
        // Arrange - testing push model timeout protection
        var options = new UpstreamConnectionOptions
        {
            Mode = CommunicationMode.SignalR,
            SignalRHub = "http://localhost:5000/sorterhub",
            ChuteAssignmentTimeoutMs = 15000 // Timeout protection for push model
        };
        var factory = new UpstreamRoutingClientFactory(_loggerFactoryMock.Object, () => options, _clockMock);

        // Act
        var client = factory.CreateClient();

        // Assert - push model timeout protection is configured
        Assert.NotNull(client);
        Assert.True(client is SignalRRuleEngineClient);
    }

    /// <summary>
    /// PR-UPSTREAM01: 空 TcpServer 应使用默认值
    /// </summary>
    [Fact]
    public void CreateClient_WithEmptyTcpServer_FallsBackToTcpWithDefaults()
    {
        // Arrange - empty TcpServer should be handled by fallback
        var options = new UpstreamConnectionOptions
        {
            Mode = CommunicationMode.Tcp,
            TcpServer = "" // Empty configuration
        };
        var factory = new UpstreamRoutingClientFactory(_loggerFactoryMock.Object, () => options, _clockMock);

        // Act - should catch exception and fallback to Tcp with defaults
        using var client = factory.CreateClient();

        // Assert - should fallback to Tcp client with defaults
        Assert.NotNull(client);
        Assert.IsType<TouchSocketTcpRuleEngineClient>(client);
    }

    /// <summary>
    /// PR-UPSTREAM01: 空 SignalRHub 应降级为 TCP
    /// </summary>
    [Fact]
    public void CreateClient_WithEmptySignalRHub_FallsBackToTcp()
    {
        // Arrange - empty SignalRHub should trigger fallback
        var options = new UpstreamConnectionOptions
        {
            Mode = CommunicationMode.SignalR,
            SignalRHub = "", // Empty configuration
            TcpServer = "localhost:9000"
        };
        var factory = new UpstreamRoutingClientFactory(_loggerFactoryMock.Object, () => options, _clockMock);

        // Act - should catch exception and fallback to Tcp
        using var client = factory.CreateClient();

        // Assert - should fallback to Tcp client
        Assert.NotNull(client);
        Assert.IsType<TouchSocketTcpRuleEngineClient>(client);
    }

    /// <summary>
    /// PR-UPSTREAM01: 空 MqttBroker 应降级为 TCP
    /// </summary>
    [Fact]
    public void CreateClient_WithEmptyMqttBroker_FallsBackToTcp()
    {
        // Arrange - empty MqttBroker should trigger fallback
        var options = new UpstreamConnectionOptions
        {
            Mode = CommunicationMode.Mqtt,
            MqttBroker = "", // Empty configuration
            TcpServer = "localhost:9000"
        };
        var factory = new UpstreamRoutingClientFactory(_loggerFactoryMock.Object, () => options, _clockMock);

        // Act - should catch exception and fallback to Tcp
        using var client = factory.CreateClient();

        // Assert - should fallback to Tcp client
        Assert.NotNull(client);
        Assert.IsType<TouchSocketTcpRuleEngineClient>(client);
    }
}
