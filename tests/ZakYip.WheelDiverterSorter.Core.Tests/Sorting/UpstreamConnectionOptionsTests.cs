using Xunit;
using ZakYip.WheelDiverterSorter.Core.Sorting.Policies;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Enums.Communication;

namespace ZakYip.WheelDiverterSorter.Core.Tests.Sorting;

/// <summary>
/// UpstreamConnectionOptions 和 UpstreamConnectionOptionsValidator 单元测试
/// PR-UPSTREAM01: HTTP 相关测试已移除
/// </summary>
public class UpstreamConnectionOptionsTests
{
    private readonly UpstreamConnectionOptionsValidator _validator = new();

    #region 默认值测试

    [Fact]
    public void DefaultOptions_ShouldHaveValidDefaults()
    {
        // Arrange
        var options = new UpstreamConnectionOptions();

        // Assert
        Assert.Equal(CommunicationMode.Tcp, options.Mode);
        Assert.Equal(ConnectionMode.Client, options.ConnectionMode);
        Assert.Equal(UpstreamConnectionOptions.DefaultTcpServer, options.TcpServer); // 默认值
        Assert.Null(options.SignalRHub);
        Assert.Null(options.MqttBroker);
        Assert.Equal("sorting/chute/assignment", options.MqttTopic);
        Assert.Equal(5000, options.TimeoutMs);
        Assert.True(options.EnableAutoReconnect);
        Assert.Equal(200, options.InitialBackoffMs);
        Assert.Equal(2000, options.MaxBackoffMs);
        Assert.True(options.EnableInfiniteRetry);
    }

    #endregion

    #region TCP 模式校验

    [Fact]
    public void Validate_TcpMode_WithoutTcpServer_ShouldFail()
    {
        // Arrange
        // 显式设置 TcpServer 为 null 以覆盖默认值
        var options = new UpstreamConnectionOptions
        {
            Mode = CommunicationMode.Tcp,
            TcpServer = null!
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains("TcpServer", result.FailureMessage);
    }

    [Fact]
    public void Validate_TcpMode_WithEmptyTcpServer_ShouldFail()
    {
        // Arrange
        var options = new UpstreamConnectionOptions
        {
            Mode = CommunicationMode.Tcp,
            TcpServer = "   "
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains("TcpServer", result.FailureMessage);
    }

    [Fact]
    public void Validate_TcpMode_WithValidTcpServer_ShouldPass()
    {
        // Arrange
        var options = new UpstreamConnectionOptions
        {
            Mode = CommunicationMode.Tcp,
            TcpServer = "192.168.1.100:8000"
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.True(result.Succeeded);
    }

    #endregion

    #region SignalR 模式校验

    [Fact]
    public void Validate_SignalRMode_WithoutSignalRHub_ShouldFail()
    {
        // Arrange
        var options = new UpstreamConnectionOptions
        {
            Mode = CommunicationMode.SignalR,
            SignalRHub = null
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains("SignalRHub", result.FailureMessage);
    }

    [Fact]
    public void Validate_SignalRMode_WithValidSignalRHub_ShouldPass()
    {
        // Arrange
        var options = new UpstreamConnectionOptions
        {
            Mode = CommunicationMode.SignalR,
            SignalRHub = "http://localhost:5000/sortingHub"
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.True(result.Succeeded);
    }

    #endregion

    #region MQTT 模式校验

    [Fact]
    public void Validate_MqttMode_WithoutMqttBroker_ShouldFail()
    {
        // Arrange
        var options = new UpstreamConnectionOptions
        {
            Mode = CommunicationMode.Mqtt,
            MqttBroker = null
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains("MqttBroker", result.FailureMessage);
    }

    [Fact]
    public void Validate_MqttMode_WithoutMqttTopic_ShouldFail()
    {
        // Arrange
        var options = new UpstreamConnectionOptions
        {
            Mode = CommunicationMode.Mqtt,
            MqttBroker = "mqtt://localhost:1883",
            MqttTopic = ""
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains("MqttTopic", result.FailureMessage);
    }

    [Fact]
    public void Validate_MqttMode_WithValidConfig_ShouldPass()
    {
        // Arrange
        var options = new UpstreamConnectionOptions
        {
            Mode = CommunicationMode.Mqtt,
            MqttBroker = "mqtt://localhost:1883",
            MqttTopic = "sorting/chute/assignment"
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.True(result.Succeeded);
    }

    #endregion

    #region 通用配置校验（PR-UPSTREAM01: 改用 TCP 模式）

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-1000)]
    public void Validate_WithInvalidTimeoutMs_ShouldFail(int timeoutMs)
    {
        // Arrange
        var options = new UpstreamConnectionOptions
        {
            Mode = CommunicationMode.Tcp,
            TcpServer = "localhost:9000",
            TimeoutMs = timeoutMs
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains("TimeoutMs", result.FailureMessage);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_WithInvalidInitialBackoffMs_ShouldFail(int backoffMs)
    {
        // Arrange
        var options = new UpstreamConnectionOptions
        {
            Mode = CommunicationMode.Tcp,
            TcpServer = "localhost:9000",
            InitialBackoffMs = backoffMs
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains("InitialBackoffMs", result.FailureMessage);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_WithInvalidMaxBackoffMs_ShouldFail(int backoffMs)
    {
        // Arrange
        var options = new UpstreamConnectionOptions
        {
            Mode = CommunicationMode.Tcp,
            TcpServer = "localhost:9000",
            MaxBackoffMs = backoffMs
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains("MaxBackoffMs", result.FailureMessage);
    }

    [Fact]
    public void Validate_WithMaxBackoffLessThanInitialBackoff_ShouldFail()
    {
        // Arrange
        var options = new UpstreamConnectionOptions
        {
            Mode = CommunicationMode.Tcp,
            TcpServer = "localhost:9000",
            InitialBackoffMs = 2000,
            MaxBackoffMs = 1000
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains("不能小于", result.FailureMessage);
    }

    #endregion
}
