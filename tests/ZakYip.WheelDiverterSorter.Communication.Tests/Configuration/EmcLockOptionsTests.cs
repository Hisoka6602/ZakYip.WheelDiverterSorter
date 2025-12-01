using ZakYip.WheelDiverterSorter.Communication.Configuration;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Enums.Communication;

namespace ZakYip.WheelDiverterSorter.Communication.Tests.Configuration;

/// <summary>
/// Tests for EmcLockOptions
/// </summary>
public class EmcLockOptionsTests
{
    [Fact]
    public void EmcLockOptions_DefaultValues()
    {
        // Act
        var options = new EmcLockOptions();

        // Assert
        Assert.False(options.Enabled);
        Assert.Null(options.InstanceId);
        Assert.Equal(CommunicationMode.Tcp, options.CommunicationMode);
        Assert.Equal("localhost:9000", options.TcpServer);
        Assert.Equal("http://localhost:5001/emclock", options.SignalRHubUrl);
        Assert.Equal("localhost", options.MqttBroker);
        Assert.Equal(1883, options.MqttPort);
        Assert.Equal("emc/lock", options.MqttTopicPrefix);
        Assert.Equal(5000, options.DefaultTimeoutMs);
        Assert.Equal(3000, options.HeartbeatIntervalMs);
        Assert.True(options.AutoReconnect);
        Assert.Equal(5000, options.ReconnectIntervalMs);
    }

    [Fact]
    public void EmcLockOptions_CanSetAllProperties()
    {
        // Arrange & Act
        var options = new EmcLockOptions
        {
            Enabled = true,
            InstanceId = "test-instance",
            CommunicationMode = CommunicationMode.SignalR,
            TcpServer = "192.168.1.100:9000",
            SignalRHubUrl = "http://server:5000/hub",
            MqttBroker = "mqtt.example.com",
            MqttPort = 1884,
            MqttTopicPrefix = "custom/emc",
            DefaultTimeoutMs = 10000,
            HeartbeatIntervalMs = 5000,
            AutoReconnect = false,
            ReconnectIntervalMs = 10000
        };

        // Assert
        Assert.True(options.Enabled);
        Assert.Equal("test-instance", options.InstanceId);
        Assert.Equal(CommunicationMode.SignalR, options.CommunicationMode);
        Assert.Equal("192.168.1.100:9000", options.TcpServer);
        Assert.Equal("http://server:5000/hub", options.SignalRHubUrl);
        Assert.Equal("mqtt.example.com", options.MqttBroker);
        Assert.Equal(1884, options.MqttPort);
        Assert.Equal("custom/emc", options.MqttTopicPrefix);
        Assert.Equal(10000, options.DefaultTimeoutMs);
        Assert.Equal(5000, options.HeartbeatIntervalMs);
        Assert.False(options.AutoReconnect);
        Assert.Equal(10000, options.ReconnectIntervalMs);
    }

    /// <summary>
    /// PR-UPSTREAM01: 移除 HTTP 模式测试
    /// </summary>
    [Theory]
    [InlineData(CommunicationMode.Tcp)]
    [InlineData(CommunicationMode.SignalR)]
    [InlineData(CommunicationMode.Mqtt)]
    public void EmcLockOptions_SupportsDifferentCommunicationModes(CommunicationMode mode)
    {
        // Act
        var options = new EmcLockOptions
        {
            CommunicationMode = mode
        };

        // Assert
        Assert.Equal(mode, options.CommunicationMode);
    }
}
