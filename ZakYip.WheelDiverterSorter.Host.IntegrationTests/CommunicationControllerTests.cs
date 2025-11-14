using System.Net;

namespace ZakYip.WheelDiverterSorter.Host.IntegrationTests;

/// <summary>
/// Integration tests for CommunicationController
/// </summary>
public class CommunicationControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public CommunicationControllerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetConfiguration_ReturnsSuccess()
    {
        // Act
        var response = await _client.GetAsync("/api/communication/config");

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task TestConnection_ReturnsSuccess()
    {
        // Act
        var response = await _client.PostAsync("/api/communication/test", null);

        // Assert
        // Test endpoint returns 200 even on connection failure
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetStatus_ReturnsSuccess()
    {
        // Act
        var response = await _client.GetAsync("/api/communication/status");

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ResetStatistics_ReturnsSuccess()
    {
        // Act
        var response = await _client.PostAsync("/api/communication/reset-stats", null);

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetPersistedConfiguration_ReturnsSuccess()
    {
        // Act
        var response = await _client.GetAsync("/api/communication/config/persisted");

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
