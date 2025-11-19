using Xunit;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;

namespace ZakYip.WheelDiverterSorter.Core.Tests;

public class SystemConfigurationTests
{
    [Fact]
    public void GetDefault_ReturnsValidConfiguration()
    {
        // Act
        var config = SystemConfiguration.GetDefault();

        // Assert
        Assert.Equal("system", config.ConfigName);
        Assert.Equal(999, config.ExceptionChuteId);
        Assert.Equal(1883, config.MqttDefaultPort);
        Assert.Equal(8888, config.TcpDefaultPort);
        Assert.Equal(10000, config.ChuteAssignmentTimeoutMs);
        Assert.Equal(5000, config.RequestTimeoutMs);
        Assert.Equal(3, config.RetryCount);
        Assert.Equal(1000, config.RetryDelayMs);
        Assert.True(config.EnableAutoReconnect);
        Assert.Equal(1, config.Version);
    }

    [Fact]
    public void Validate_WithValidConfiguration_ReturnsTrue()
    {
        // Arrange
        var config = SystemConfiguration.GetDefault();

        // Act
        var (isValid, errorMessage) = config.Validate();

        // Assert
        Assert.True(isValid);
        Assert.Null(errorMessage);
    }

    [Theory]
    [InlineData(0, "异常格口ID必须大于0")]
    [InlineData(-1, "异常格口ID必须大于0")]
    public void Validate_WithInvalidExceptionChuteId_ReturnsFalse(int chuteId, string expectedError)
    {
        // Arrange
        var config = SystemConfiguration.GetDefault();
        config.ExceptionChuteId = chuteId;

        // Act
        var (isValid, errorMessage) = config.Validate();

        // Assert
        Assert.False(isValid);
        Assert.Equal(expectedError, errorMessage);
    }

    [Theory]
    [InlineData(0, "MQTT默认端口必须在1-65535之间")]
    [InlineData(-1, "MQTT默认端口必须在1-65535之间")]
    [InlineData(65536, "MQTT默认端口必须在1-65535之间")]
    public void Validate_WithInvalidMqttPort_ReturnsFalse(int port, string expectedError)
    {
        // Arrange
        var config = SystemConfiguration.GetDefault();
        config.MqttDefaultPort = port;

        // Act
        var (isValid, errorMessage) = config.Validate();

        // Assert
        Assert.False(isValid);
        Assert.Equal(expectedError, errorMessage);
    }

    [Theory]
    [InlineData(0, "TCP默认端口必须在1-65535之间")]
    [InlineData(-1, "TCP默认端口必须在1-65535之间")]
    [InlineData(65536, "TCP默认端口必须在1-65535之间")]
    public void Validate_WithInvalidTcpPort_ReturnsFalse(int port, string expectedError)
    {
        // Arrange
        var config = SystemConfiguration.GetDefault();
        config.TcpDefaultPort = port;

        // Act
        var (isValid, errorMessage) = config.Validate();

        // Assert
        Assert.False(isValid);
        Assert.Equal(expectedError, errorMessage);
    }

    [Theory]
    [InlineData(999, "格口分配超时时间必须在1000-60000毫秒之间")]
    [InlineData(60001, "格口分配超时时间必须在1000-60000毫秒之间")]
    public void Validate_WithInvalidChuteAssignmentTimeout_ReturnsFalse(int timeout, string expectedError)
    {
        // Arrange
        var config = SystemConfiguration.GetDefault();
        config.ChuteAssignmentTimeoutMs = timeout;

        // Act
        var (isValid, errorMessage) = config.Validate();

        // Assert
        Assert.False(isValid);
        Assert.Equal(expectedError, errorMessage);
    }

    [Theory]
    [InlineData(999, "请求超时时间必须在1000-60000毫秒之间")]
    [InlineData(60001, "请求超时时间必须在1000-60000毫秒之间")]
    public void Validate_WithInvalidRequestTimeout_ReturnsFalse(int timeout, string expectedError)
    {
        // Arrange
        var config = SystemConfiguration.GetDefault();
        config.RequestTimeoutMs = timeout;

        // Act
        var (isValid, errorMessage) = config.Validate();

        // Assert
        Assert.False(isValid);
        Assert.Equal(expectedError, errorMessage);
    }

    [Theory]
    [InlineData(-1, "重试次数必须在0-10之间")]
    [InlineData(11, "重试次数必须在0-10之间")]
    public void Validate_WithInvalidRetryCount_ReturnsFalse(int retryCount, string expectedError)
    {
        // Arrange
        var config = SystemConfiguration.GetDefault();
        config.RetryCount = retryCount;

        // Act
        var (isValid, errorMessage) = config.Validate();

        // Assert
        Assert.False(isValid);
        Assert.Equal(expectedError, errorMessage);
    }

    [Theory]
    [InlineData(99, "重试延迟必须在100-10000毫秒之间")]
    [InlineData(10001, "重试延迟必须在100-10000毫秒之间")]
    public void Validate_WithInvalidRetryDelay_ReturnsFalse(int retryDelay, string expectedError)
    {
        // Arrange
        var config = SystemConfiguration.GetDefault();
        config.RetryDelayMs = retryDelay;

        // Act
        var (isValid, errorMessage) = config.Validate();

        // Assert
        Assert.False(isValid);
        Assert.Equal(expectedError, errorMessage);
    }
}
