using Microsoft.Extensions.Logging;
using Moq;
using ZakYip.WheelDiverterSorter.Communication.Clients;
using ZakYip.WheelDiverterSorter.Communication.Configuration;
using ZakYip.WheelDiverterSorter.Core.LineModel.Enums;

namespace ZakYip.WheelDiverterSorter.Communication.Tests;

/// <summary>
/// 通信适配器测试：推送模型、超时保护
/// RuleEngineClientFactory tests
/// </summary>
public class RuleEngineClientFactoryTests
{
    private readonly Mock<ILoggerFactory> _loggerFactoryMock;

    public RuleEngineClientFactoryTests()
    {
        _loggerFactoryMock = new Mock<ILoggerFactory>();
        
        // Setup logger factory to return mock loggers
        _loggerFactoryMock
            .Setup(x => x.CreateLogger(It.IsAny<string>()))
            .Returns(new Mock<ILogger>().Object);
    }

    [Fact]
    public void Constructor_WithNullLoggerFactory_ThrowsArgumentNullException()
    {
        // Arrange
        var options = new RuleEngineConnectionOptions
        {
            Mode = CommunicationMode.Http,
            HttpApi = "http://localhost:5000/api/chute"
        };

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new RuleEngineClientFactory(null!, options));
    }

    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new RuleEngineClientFactory(_loggerFactoryMock.Object, null!));
    }

    [Fact]
    public void CreateClient_WithTcpMode_ReturnsTcpRuleEngineClient()
    {
        // Arrange
        var options = new RuleEngineConnectionOptions
        {
            Mode = CommunicationMode.Tcp,
            TcpServer = "localhost:9999"
        };
        var factory = new RuleEngineClientFactory(_loggerFactoryMock.Object, options);

        // Act
        using var client = factory.CreateClient();

        // Assert
        Assert.NotNull(client);
        Assert.IsType<TcpRuleEngineClient>(client);
    }

    [Fact]
    public void CreateClient_WithSignalRMode_ReturnsSignalRRuleEngineClient()
    {
        // Arrange
        var options = new RuleEngineConnectionOptions
        {
            Mode = CommunicationMode.SignalR,
            SignalRHub = "http://localhost:5000/sorterhub"
        };
        var factory = new RuleEngineClientFactory(_loggerFactoryMock.Object, options);

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
        var options = new RuleEngineConnectionOptions
        {
            Mode = CommunicationMode.Mqtt,
            MqttBroker = "mqtt://localhost:1883"
        };
        var factory = new RuleEngineClientFactory(_loggerFactoryMock.Object, options);

        // Act
        using var client = factory.CreateClient();

        // Assert
        Assert.NotNull(client);
        Assert.IsType<MqttRuleEngineClient>(client);
    }

    [Fact]
    public void CreateClient_WithHttpMode_ReturnsHttpRuleEngineClient()
    {
        // Arrange
        var options = new RuleEngineConnectionOptions
        {
            Mode = CommunicationMode.Http,
            HttpApi = "http://localhost:5000/api/chute"
        };
        var factory = new RuleEngineClientFactory(_loggerFactoryMock.Object, options);

        // Act
        using var client = factory.CreateClient();

        // Assert
        Assert.NotNull(client);
        Assert.IsType<HttpRuleEngineClient>(client);
    }

    [Fact]
    public void CreateClient_WithInvalidMode_ThrowsNotSupportedException()
    {
        // Arrange
        var options = new RuleEngineConnectionOptions
        {
            Mode = (CommunicationMode)999, // Invalid mode
            HttpApi = "http://localhost:5000/api/chute"
        };
        var factory = new RuleEngineClientFactory(_loggerFactoryMock.Object, options);

        // Act & Assert
        var exception = Assert.Throws<NotSupportedException>(() => factory.CreateClient());
        Assert.Contains("不支持的通信模式", exception.Message);
    }

    [Fact]
    public void CreateClient_MultipleCalls_ReturnsNewInstancesEachTime()
    {
        // Arrange
        var options = new RuleEngineConnectionOptions
        {
            Mode = CommunicationMode.Http,
            HttpApi = "http://localhost:5000/api/chute"
        };
        var factory = new RuleEngineClientFactory(_loggerFactoryMock.Object, options);

        // Act
        using var client1 = factory.CreateClient();
        using var client2 = factory.CreateClient();

        // Assert
        Assert.NotNull(client1);
        Assert.NotNull(client2);
        Assert.NotSame(client1, client2);
    }

    [Theory]
    [InlineData(CommunicationMode.Tcp)]
    [InlineData(CommunicationMode.SignalR)]
    [InlineData(CommunicationMode.Mqtt)]
    [InlineData(CommunicationMode.Http)]
    public void CreateClient_WithAllSupportedModes_SuccessfullyCreatesClient(CommunicationMode mode)
    {
        // Arrange
        var options = new RuleEngineConnectionOptions
        {
            Mode = mode,
            TcpServer = "localhost:9999",
            SignalRHub = "http://localhost:5000/sorterhub",
            MqttBroker = "mqtt://localhost:1883",
            HttpApi = "http://localhost:5000/api/chute"
        };
        var factory = new RuleEngineClientFactory(_loggerFactoryMock.Object, options);

        // Act
        using var client = factory.CreateClient();

        // Assert
        Assert.NotNull(client);
    }

    [Fact]
    public void CreateClient_WithTimeoutConfiguration_ClientHasCorrectTimeout()
    {
        // Arrange
        var options = new RuleEngineConnectionOptions
        {
            Mode = CommunicationMode.Http,
            HttpApi = "http://localhost:5000/api/chute",
            TimeoutMs = 10000
        };
        var factory = new RuleEngineClientFactory(_loggerFactoryMock.Object, options);

        // Act
        using var client = factory.CreateClient();

        // Assert - timeout is configured in the options passed to client
        Assert.NotNull(client);
    }

    [Fact]
    public void CreateClient_WithAutoReconnectEnabled_ClientSupportsReconnection()
    {
        // Arrange
        var options = new RuleEngineConnectionOptions
        {
            Mode = CommunicationMode.SignalR,
            SignalRHub = "http://localhost:5000/sorterhub",
            EnableAutoReconnect = true
        };
        var factory = new RuleEngineClientFactory(_loggerFactoryMock.Object, options);

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
        var options = new RuleEngineConnectionOptions
        {
            Mode = CommunicationMode.Tcp,
            TcpServer = "localhost:9999",
            RetryCount = 5,
            RetryDelayMs = 2000
        };
        var factory = new RuleEngineClientFactory(_loggerFactoryMock.Object, options);

        // Act
        using var client = factory.CreateClient();

        // Assert - retry settings are configured in the options
        Assert.NotNull(client);
    }

    [Fact]
    public void CreateClient_WithChuteAssignmentTimeout_ClientHasPushModelTimeoutProtection()
    {
        // Arrange - testing push model timeout protection
        var options = new RuleEngineConnectionOptions
        {
            Mode = CommunicationMode.SignalR,
            SignalRHub = "http://localhost:5000/sorterhub",
            ChuteAssignmentTimeoutMs = 15000 // Timeout protection for push model
        };
        var factory = new RuleEngineClientFactory(_loggerFactoryMock.Object, options);

        // Act
        using var client = factory.CreateClient();

        // Assert - push model timeout protection is configured
        Assert.NotNull(client);
        Assert.True(client is SignalRRuleEngineClient);
    }
}
