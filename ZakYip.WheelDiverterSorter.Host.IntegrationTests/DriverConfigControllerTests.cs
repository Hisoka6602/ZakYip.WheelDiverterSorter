using System.Net;

namespace ZakYip.WheelDiverterSorter.Host.IntegrationTests;

/// <summary>
/// Integration tests for DriverConfigController
/// </summary>
public class DriverConfigControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public DriverConfigControllerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetDriverConfig_ReturnsSuccess()
    {
        // Act
        var response = await _client.GetAsync("/api/config/driver");

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetDriverConfig_ReturnsJsonContent()
    {
        // Act
        var response = await _client.GetAsync("/api/config/driver");

        // Assert
        Assert.Contains("application/json", response.Content.Headers.ContentType?.ToString() ?? "");
    }
}
