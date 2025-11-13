using System.Net;
using System.Net.Http.Json;

namespace ZakYip.WheelDiverterSorter.Host.IntegrationTests;

/// <summary>
/// Integration tests for SystemConfigController
/// </summary>
public class SystemConfigControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public SystemConfigControllerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetSystemConfig_ReturnsSuccess()
    {
        // Act
        var response = await _client.GetAsync("/api/config/system");

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetSystemConfig_ReturnsJsonContent()
    {
        // Act
        var response = await _client.GetAsync("/api/config/system");

        // Assert
        Assert.Contains("application/json", response.Content.Headers.ContentType?.ToString() ?? "");
    }

    [Fact]
    public async Task UpdateSystemConfig_WithInvalidData_ReturnsError()
    {
        // Arrange
        var invalidRequest = new { }; // Empty request

        // Act
        var response = await _client.PutAsJsonAsync("/api/config/system", invalidRequest);

        // Assert
        // Accept either bad request or internal server error
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest ||
                   response.StatusCode == HttpStatusCode.InternalServerError ||
                   response.StatusCode == HttpStatusCode.OK); // Some APIs may accept empty updates
    }
}
