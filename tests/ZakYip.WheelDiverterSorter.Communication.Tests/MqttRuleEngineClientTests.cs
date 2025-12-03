using Microsoft.Extensions.Logging;
using Moq;
using ZakYip.WheelDiverterSorter.Communication.Clients;
using ZakYip.WheelDiverterSorter.Communication.Configuration;
using ZakYip.WheelDiverterSorter.Core.LineModel;
using ZakYip.WheelDiverterSorter.Core.Enums;

using ZakYip.WheelDiverterSorter.Core.LineModel.Chutes;namespace ZakYip.WheelDiverterSorter.Communication.Tests;
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
        // Arrange
        var options = new UpstreamConnectionOptions
        {
            MqttBroker = "mqtt://localhost:1883"
        };
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new MqttRuleEngineClient(null!, options));
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
        Assert.Throws<ArgumentNullException>(() => new MqttRuleEngineClient(_loggerMock.Object, null!));
    public void Constructor_WithEmptyMqttBroker_ThrowsArgumentException()
            MqttBroker = ""
        Assert.Throws<ArgumentException>(() => new MqttRuleEngineClient(_loggerMock.Object, options));
    public void Constructor_WithNullMqttBroker_ThrowsArgumentException()
            MqttBroker = null
    public void IsConnected_InitiallyReturnsFalse()
        using var client = new MqttRuleEngineClient(_loggerMock.Object, options);
        // Assert
        Assert.False(client.IsConnected);
    public async Task ConnectAsync_WithInvalidBroker_ReturnsFalse()
            MqttBroker = "mqtt://localhost:19999"
        // Act
        var result = await client.ConnectAsync(new CancellationTokenSource(1000).Token);
        Assert.False(result);
    public async Task DisconnectAsync_WhenNotConnected_CompletesSuccessfully()
        // Act & Assert (should not throw)
        await client.DisconnectAsync();
    public async Task NotifyParcelDetectedAsync_WithInvalidParcelId_ThrowsArgumentException()
        await Assert.ThrowsAsync<ArgumentException>(() => client.NotifyParcelDetectedAsync(0));
        await Assert.ThrowsAsync<ArgumentException>(() => client.NotifyParcelDetectedAsync(-1));
    public async Task NotifyParcelDetectedAsync_WhenNotConnected_ReturnsFalse()
        var parcelId = 123456789L;
        var result = await client.NotifyParcelDetectedAsync(parcelId, new CancellationTokenSource(1000).Token);
    public void ChuteAssigned_EventCanBeSubscribed()
        var eventRaised = false;
        client.ChuteAssigned += (sender, args) =>
            eventRaised = true;
        // Assert - just verify event can be subscribed without error
        Assert.False(eventRaised); // Event not raised yet
    [Theory]
    [InlineData(0)] // QoS 0: At most once
    [InlineData(1)] // QoS 1: At least once
    [InlineData(2)] // QoS 2: Exactly once
    public void Constructor_WithDifferentQoSLevels_InitializesSuccessfully(int qosLevel)
            MqttBroker = "mqtt://localhost:1883",
            Mqtt = new MqttOptions
            {
                QualityOfServiceLevel = qosLevel
            }
        Assert.NotNull(client);
    public void Constructor_ConfiguresCleanSession()
                CleanSession = true
    public void Constructor_ConfiguresSessionExpiry()
                SessionExpiryInterval = 3600
    public void Constructor_ConfiguresMessageExpiry()
                MessageExpiryInterval = 60
    public void Constructor_ConfiguresClientIdPrefix()
                ClientIdPrefix = "TestClient"
    public void Constructor_ConfiguresCustomTopic()
            MqttTopic = "custom/topic"
    public void Dispose_CanBeCalledMultipleTimes()
        var client = new MqttRuleEngineClient(_loggerMock.Object, options);
        client.Dispose();
    public void Constructor_ParsesBrokerUri_Correctly()
            MqttBroker = "mqtt://test-broker:1883"
}
