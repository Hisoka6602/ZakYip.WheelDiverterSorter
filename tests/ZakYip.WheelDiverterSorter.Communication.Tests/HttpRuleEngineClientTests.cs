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
/// <summary>
/// HTTP客户端测试：请求、响应、错误处理
/// </summary>
public class HttpRuleEngineClientTests
{
    private readonly Mock<ILogger<HttpRuleEngineClient>> _loggerMock;
    public HttpRuleEngineClientTests()
    {
        _loggerMock = new Mock<ILogger<HttpRuleEngineClient>>();
    }
    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        // Arrange
        var options = new RuleEngineConnectionOptions
        {
            HttpApi = "http://localhost:5000/api/chute"
        };
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new HttpRuleEngineClient(null!, options));
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
        Assert.Throws<ArgumentNullException>(() => new HttpRuleEngineClient(_loggerMock.Object, null!));
    public void Constructor_WithEmptyHttpApi_ThrowsArgumentException()
            HttpApi = ""
        Assert.Throws<ArgumentException>(() => new HttpRuleEngineClient(_loggerMock.Object, options));
    public void Constructor_WithNullHttpApi_ThrowsArgumentException()
            HttpApi = null
    public void IsConnected_AlwaysReturnsTrue()
        using var client = new HttpRuleEngineClient(_loggerMock.Object, options);
        // Assert - HTTP is stateless, so IsConnected is always true
        Assert.True(client.IsConnected);
    public async Task ConnectAsync_AlwaysReturnsTrue()
        // Act
        var result = await client.ConnectAsync();
        // Assert - HTTP is stateless, so ConnectAsync always succeeds
        Assert.True(result);
    public async Task DisconnectAsync_CompletesSuccessfully()
        // Act & Assert (should not throw)
        await client.DisconnectAsync();
        Assert.True(client.IsConnected); // Still "connected" because HTTP is stateless
    public async Task NotifyParcelDetectedAsync_WithInvalidParcelId_ThrowsArgumentException()
        await Assert.ThrowsAsync<ArgumentException>(() => client.NotifyParcelDetectedAsync(0));
        await Assert.ThrowsAsync<ArgumentException>(() => client.NotifyParcelDetectedAsync(-1));
    public async Task NotifyParcelDetectedAsync_WithInvalidUrl_ReturnsFalse()
            HttpApi = "http://localhost:19999/api/chute",
            TimeoutMs = 1000,
            RetryCount = 0
        var parcelId = 123456789L;
        var result = await client.NotifyParcelDetectedAsync(parcelId);
        // Assert
        Assert.False(result);
    public void ChuteAssignmentReceived_EventCanBeSubscribed()
        var eventRaised = false;
        client.ChuteAssignmentReceived += (sender, args) =>
            eventRaised = true;
        // Assert - just verify event can be subscribed without error
        Assert.False(eventRaised); // Event not raised yet
    public void Constructor_ConfiguresTimeout()
            HttpApi = "http://localhost:5000/api/chute",
            TimeoutMs = 10000
        Assert.NotNull(client);
    public void Constructor_ConfiguresRetry()
            RetryCount = 5,
            RetryDelayMs = 500
    public void Constructor_ConfiguresHttp2()
            Http = new HttpOptions
            {
                UseHttp2 = true
            }
    public void Constructor_ConfiguresConnectionPool()
                MaxConnectionsPerServer = 10,
                PooledConnectionIdleTimeout = 30,
                PooledConnectionLifetime = 300
    public void Dispose_CanBeCalledMultipleTimes()
        var client = new HttpRuleEngineClient(_loggerMock.Object, options);
        client.Dispose();
    public void Constructor_LogsWarningAboutProductionUsage()
        // Assert - verify warning was logged
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("仅用于测试")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    [Theory]
    [InlineData("http://localhost:5000/api/chute")]
    [InlineData("https://api.example.com/chute")]
    [InlineData("http://192.168.1.100:8080/api/v1/chute")]
    public void Constructor_WithVariousUrls_InitializesSuccessfully(string apiUrl)
            HttpApi = apiUrl
}
