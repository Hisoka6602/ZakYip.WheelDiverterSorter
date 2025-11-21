using Microsoft.Extensions.Logging;
using Moq;
using ZakYip.WheelDiverterSorter.Communication.Clients;
using ZakYip.WheelDiverterSorter.Communication.Configuration;
using ZakYip.WheelDiverterSorter.Core.LineModel;
using ZakYip.WheelDiverterSorter.Core.Enums.Communication;
using ZakYip.WheelDiverterSorter.Core.Enums.Conveyor;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Enums.IoBinding;
using ZakYip.WheelDiverterSorter.Core.Enums.Routing;
using ZakYip.WheelDiverterSorter.Core.Enums.Sensors;
using ZakYip.WheelDiverterSorter.Core.Enums.Sorting;
using ZakYip.WheelDiverterSorter.Core.Enums.System;


using ZakYip.WheelDiverterSorter.Core.LineModel.Chutes;namespace ZakYip.WheelDiverterSorter.Communication.Tests;
using ZakYip.WheelDiverterSorter.Core.Enums.Communication;
using ZakYip.WheelDiverterSorter.Core.Enums.Conveyor;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Enums.IoBinding;
using ZakYip.WheelDiverterSorter.Core.Enums.Routing;
using ZakYip.WheelDiverterSorter.Core.Enums.Sensors;
using ZakYip.WheelDiverterSorter.Core.Enums.Sorting;
using ZakYip.WheelDiverterSorter.Core.Enums.System;

/// <summary>
/// MQTT客户端测试：发布、订阅、QoS级别
/// </summary>
public class MqttRuleEngineClientTests
{
    private readonly Mock<ILogger<MqttRuleEngineClient>> _loggerMock;

    public MqttRuleEngineClientTests()
    {
        _loggerMock = new Mock<ILogger<MqttRuleEngineClient>>();
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var options = new RuleEngineConnectionOptions
        {
            MqttBroker = "mqtt://localhost:1883"
        };

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new MqttRuleEngineClient(null!, options));
    }

    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new MqttRuleEngineClient(_loggerMock.Object, null!));
    }

    [Fact]
    public void Constructor_WithEmptyMqttBroker_ThrowsArgumentException()
    {
        // Arrange
        var options = new RuleEngineConnectionOptions
        {
            MqttBroker = ""
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => new MqttRuleEngineClient(_loggerMock.Object, options));
    }

    [Fact]
    public void Constructor_WithNullMqttBroker_ThrowsArgumentException()
    {
        // Arrange
        var options = new RuleEngineConnectionOptions
        {
            MqttBroker = null
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => new MqttRuleEngineClient(_loggerMock.Object, options));
    }

    [Fact]
    public void IsConnected_InitiallyReturnsFalse()
    {
        // Arrange
        var options = new RuleEngineConnectionOptions
        {
            MqttBroker = "mqtt://localhost:1883"
        };
        using var client = new MqttRuleEngineClient(_loggerMock.Object, options);

        // Assert
        Assert.False(client.IsConnected);
    }

    [Fact]
    public async Task ConnectAsync_WithInvalidBroker_ReturnsFalse()
    {
        // Arrange
        var options = new RuleEngineConnectionOptions
        {
            MqttBroker = "mqtt://localhost:19999"
        };
        using var client = new MqttRuleEngineClient(_loggerMock.Object, options);

        // Act
        var result = await client.ConnectAsync(new CancellationTokenSource(1000).Token);

        // Assert
        Assert.False(result);
        Assert.False(client.IsConnected);
    }

    [Fact]
    public async Task DisconnectAsync_WhenNotConnected_CompletesSuccessfully()
    {
        // Arrange
        var options = new RuleEngineConnectionOptions
        {
            MqttBroker = "mqtt://localhost:1883"
        };
        using var client = new MqttRuleEngineClient(_loggerMock.Object, options);

        // Act & Assert (should not throw)
        await client.DisconnectAsync();
        Assert.False(client.IsConnected);
    }

    [Fact]
    public async Task NotifyParcelDetectedAsync_WithInvalidParcelId_ThrowsArgumentException()
    {
        // Arrange
        var options = new RuleEngineConnectionOptions
        {
            MqttBroker = "mqtt://localhost:1883"
        };
        using var client = new MqttRuleEngineClient(_loggerMock.Object, options);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => client.NotifyParcelDetectedAsync(0));
        await Assert.ThrowsAsync<ArgumentException>(() => client.NotifyParcelDetectedAsync(-1));
    }

    [Fact]
    public async Task NotifyParcelDetectedAsync_WhenNotConnected_ReturnsFalse()
    {
        // Arrange
        var options = new RuleEngineConnectionOptions
        {
            MqttBroker = "mqtt://localhost:19999"
        };
        using var client = new MqttRuleEngineClient(_loggerMock.Object, options);
        var parcelId = 123456789L;

        // Act
        var result = await client.NotifyParcelDetectedAsync(parcelId, new CancellationTokenSource(1000).Token);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ChuteAssignmentReceived_EventCanBeSubscribed()
    {
        // Arrange
        var options = new RuleEngineConnectionOptions
        {
            MqttBroker = "mqtt://localhost:1883"
        };
        using var client = new MqttRuleEngineClient(_loggerMock.Object, options);
        var eventRaised = false;

        // Act
        client.ChuteAssignmentReceived += (sender, args) =>
        {
            eventRaised = true;
        };

        // Assert - just verify event can be subscribed without error
        Assert.False(eventRaised); // Event not raised yet
    }

    [Theory]
    [InlineData(0)] // QoS 0: At most once
    [InlineData(1)] // QoS 1: At least once
    [InlineData(2)] // QoS 2: Exactly once
    public void Constructor_WithDifferentQoSLevels_InitializesSuccessfully(int qosLevel)
    {
        // Arrange
        var options = new RuleEngineConnectionOptions
        {
            MqttBroker = "mqtt://localhost:1883",
            Mqtt = new MqttOptions
            {
                QualityOfServiceLevel = qosLevel
            }
        };

        // Act & Assert (should not throw)
        using var client = new MqttRuleEngineClient(_loggerMock.Object, options);
        Assert.NotNull(client);
    }

    [Fact]
    public void Constructor_ConfiguresCleanSession()
    {
        // Arrange
        var options = new RuleEngineConnectionOptions
        {
            MqttBroker = "mqtt://localhost:1883",
            Mqtt = new MqttOptions
            {
                CleanSession = true
            }
        };

        // Act & Assert (should not throw)
        using var client = new MqttRuleEngineClient(_loggerMock.Object, options);
        Assert.NotNull(client);
    }

    [Fact]
    public void Constructor_ConfiguresSessionExpiry()
    {
        // Arrange
        var options = new RuleEngineConnectionOptions
        {
            MqttBroker = "mqtt://localhost:1883",
            Mqtt = new MqttOptions
            {
                SessionExpiryInterval = 3600
            }
        };

        // Act & Assert (should not throw)
        using var client = new MqttRuleEngineClient(_loggerMock.Object, options);
        Assert.NotNull(client);
    }

    [Fact]
    public void Constructor_ConfiguresMessageExpiry()
    {
        // Arrange
        var options = new RuleEngineConnectionOptions
        {
            MqttBroker = "mqtt://localhost:1883",
            Mqtt = new MqttOptions
            {
                MessageExpiryInterval = 60
            }
        };

        // Act & Assert (should not throw)
        using var client = new MqttRuleEngineClient(_loggerMock.Object, options);
        Assert.NotNull(client);
    }

    [Fact]
    public void Constructor_ConfiguresClientIdPrefix()
    {
        // Arrange
        var options = new RuleEngineConnectionOptions
        {
            MqttBroker = "mqtt://localhost:1883",
            Mqtt = new MqttOptions
            {
                ClientIdPrefix = "TestClient"
            }
        };

        // Act & Assert (should not throw)
        using var client = new MqttRuleEngineClient(_loggerMock.Object, options);
        Assert.NotNull(client);
    }

    [Fact]
    public void Constructor_ConfiguresCustomTopic()
    {
        // Arrange
        var options = new RuleEngineConnectionOptions
        {
            MqttBroker = "mqtt://localhost:1883",
            MqttTopic = "custom/topic"
        };

        // Act & Assert (should not throw)
        using var client = new MqttRuleEngineClient(_loggerMock.Object, options);
        Assert.NotNull(client);
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        // Arrange
        var options = new RuleEngineConnectionOptions
        {
            MqttBroker = "mqtt://localhost:1883"
        };
        var client = new MqttRuleEngineClient(_loggerMock.Object, options);

        // Act & Assert (should not throw)
        client.Dispose();
        client.Dispose();
    }

    [Fact]
    public void Constructor_ParsesBrokerUri_Correctly()
    {
        // Arrange
        var options = new RuleEngineConnectionOptions
        {
            MqttBroker = "mqtt://test-broker:1883"
        };

        // Act & Assert (should not throw)
        using var client = new MqttRuleEngineClient(_loggerMock.Object, options);
        Assert.NotNull(client);
    }
}
