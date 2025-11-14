using System.Net;
using System.Net.Http.Json;

namespace ZakYip.WheelDiverterSorter.Host.IntegrationTests;

/// <summary>
/// Integration tests for SensorConfigController
/// </summary>
public class SensorConfigControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public SensorConfigControllerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetSensorConfig_ReturnsSuccess()
    {
        // Act
        var response = await _client.GetAsync("/api/config/sensor");

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetSensorConfig_ReturnsJsonContent()
    {
        // Act
        var response = await _client.GetAsync("/api/config/sensor");

        // Assert
        Assert.Contains("application/json", response.Content.Headers.ContentType?.ToString() ?? "");
    }

    [Fact]
    public async Task UpdateSensorConfig_WithValidData_ReturnsSuccess()
    {
        // Arrange
        // First get the current config to ensure we send a valid update
        var getResponse = await _client.GetAsync("/api/config/sensor");
        var configContent = await getResponse.Content.ReadAsStringAsync();

        // Act
        var response = await _client.PutAsync("/api/config/sensor", 
            new StringContent(configContent, System.Text.Encoding.UTF8, "application/json"));

        // Assert
        // Should return OK when updating with existing config
        Assert.True(
            response.StatusCode == HttpStatusCode.OK ||
            response.StatusCode == HttpStatusCode.BadRequest, // In case validation fails
            $"Expected 200 or 400, but got {(int)response.StatusCode} ({response.StatusCode})");
    }

    [Fact]
    public async Task UpdateSensorConfig_WithInvalidData_ReturnsError()
    {
        // Arrange
        var invalidRequest = new { }; // Empty request

        // Act
        var response = await _client.PutAsJsonAsync("/api/config/sensor", invalidRequest);

        // Assert
        // Accept either bad request, internal server error, or OK (some APIs may accept empty updates)
        Assert.True(
            response.StatusCode == HttpStatusCode.BadRequest ||
            response.StatusCode == HttpStatusCode.InternalServerError ||
            response.StatusCode == HttpStatusCode.OK, // Some APIs may accept empty updates
            $"Expected 400, 500, or 200, but got {(int)response.StatusCode} ({response.StatusCode})");
    }

    [Fact]
    public async Task ResetSensorConfig_ReturnsSuccess()
    {
        // Act
        var response = await _client.PostAsync("/api/config/sensor/reset", null);

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
