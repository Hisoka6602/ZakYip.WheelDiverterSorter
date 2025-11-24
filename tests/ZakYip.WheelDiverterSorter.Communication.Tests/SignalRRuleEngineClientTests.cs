using Microsoft.Extensions.Logging;
using Moq;
using ZakYip.WheelDiverterSorter.Communication.Clients;
using ZakYip.WheelDiverterSorter.Communication.Configuration;
using ZakYip.WheelDiverterSorter.Core.LineModel;
using ZakYip.WheelDiverterSorter.Core.Enums;

using ZakYip.WheelDiverterSorter.Core.LineModel.Chutes;namespace ZakYip.WheelDiverterSorter.Communication.Tests;
/// <summary>
/// SignalR客户端测试：Hub连接、方法调用、回调
/// </summary>
public class SignalRRuleEngineClientTests
{
    private readonly Mock<ILogger<SignalRRuleEngineClient>> _loggerMock;
    public SignalRRuleEngineClientTests()
    {
        _loggerMock = new Mock<ILogger<SignalRRuleEngineClient>>();
    }
    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        // Arrange
        var options = new RuleEngineConnectionOptions
        {
            SignalRHub = "http://localhost:5000/sorterhub"
        };
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new SignalRRuleEngineClient(null!, options));
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
        Assert.Throws<ArgumentNullException>(() => new SignalRRuleEngineClient(_loggerMock.Object, null!));
    public void Constructor_WithEmptySignalRHub_ThrowsArgumentException()
            SignalRHub = ""
        Assert.Throws<ArgumentException>(() => new SignalRRuleEngineClient(_loggerMock.Object, options));
    public void Constructor_WithNullSignalRHub_ThrowsArgumentException()
            SignalRHub = null
    public void IsConnected_InitiallyReturnsFalse()
        using var client = new SignalRRuleEngineClient(_loggerMock.Object, options);
        // Assert
        Assert.False(client.IsConnected);
    public async Task ConnectAsync_WithInvalidHub_ReturnsFalse()
            SignalRHub = "http://localhost:19999/invalidhub",
            SignalR = new SignalROptions
            {
                HandshakeTimeout = 1
            }
        // Act
        var result = await client.ConnectAsync();
        Assert.False(result);
    public async Task DisconnectAsync_WhenNotConnected_CompletesSuccessfully()
        // Act & Assert (should not throw)
        await client.DisconnectAsync();
    public async Task NotifyParcelDetectedAsync_WithInvalidParcelId_ThrowsArgumentException()
        await Assert.ThrowsAsync<ArgumentException>(() => client.NotifyParcelDetectedAsync(0));
        await Assert.ThrowsAsync<ArgumentException>(() => client.NotifyParcelDetectedAsync(-1));
    public async Task NotifyParcelDetectedAsync_WhenNotConnected_ReturnsFalse()
        var parcelId = 123456789L;
        var result = await client.NotifyParcelDetectedAsync(parcelId);
    public void ChuteAssignmentReceived_EventCanBeSubscribed()
        var eventRaised = false;
        client.ChuteAssignmentReceived += (sender, args) =>
            eventRaised = true;
        // Assert - just verify event can be subscribed without error
        Assert.False(eventRaised); // Event not raised yet
    public void Constructor_ConfiguresAutoReconnect_WhenEnabled()
            SignalRHub = "http://localhost:5000/sorterhub",
            EnableAutoReconnect = true,
                ReconnectIntervals = new[] { 1000, 2000, 3000 }
        Assert.NotNull(client);
    public void Constructor_ConfiguresTimeouts()
                HandshakeTimeout = 5,
                KeepAliveInterval = 10,
                ServerTimeout = 20
    public void Dispose_CanBeCalledMultipleTimes()
        var client = new SignalRRuleEngineClient(_loggerMock.Object, options);
        client.Dispose();
}
