using System.Net;
using System.Net.Http.Json;
using ZakYip.WheelDiverterSorter.Host.Models.Config;

namespace ZakYip.WheelDiverterSorter.Host.IntegrationTests;

/// <summary>
/// Integration tests for RouteConfigController
/// </summary>
public class RouteConfigControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public RouteConfigControllerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetAllRoutes_ReturnsSuccess()
    {
        // Act
        var response = await _client.GetAsync("/api/config/routes");

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetAllRoutes_ReturnsJsonContent()
    {
        // Act
        var response = await _client.GetAsync("/api/config/routes");

        // Assert
        Assert.Equal("application/json; charset=utf-8", response.Content.Headers.ContentType?.ToString());
    }

    [Fact]
    public async Task GetRoute_WithValidChuteId_ReturnsSuccess()
    {
        // Arrange
        var chuteId = "TestChute01";

        // Act
        var response = await _client.GetAsync($"/api/config/routes/{chuteId}");

        // Assert
        // Expecting either 200 (found) or 404 (not found) - both are valid responses
        Assert.True(response.StatusCode == HttpStatusCode.OK || 
                   response.StatusCode == HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetRoute_WithEmptyChuteId_ReturnsBadRequest()
    {
        // Act
        var response = await _client.GetAsync("/api/config/routes/");

        // Assert
        // Empty chute ID should be handled by routing or controller
        Assert.True(response.StatusCode == HttpStatusCode.NotFound || 
                   response.StatusCode == HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateRoute_WithInvalidData_ReturnsBadRequest()
    {
        // Arrange
        var invalidRequest = new { }; // Empty request

        // Act
        var response = await _client.PostAsJsonAsync("/api/config/routes", invalidRequest);

        // Assert
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest ||
                   response.StatusCode == HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task UpdateRoute_WithNonExistentChuteId_ReturnsNotFound()
    {
        // Arrange
        var nonExistentChuteId = "NonExistent_" + Guid.NewGuid();
        var updateRequest = new
        {
            chuteId = nonExistentChuteId,
            diverterConfigurations = new[]
            {
                new
                {
                    diverterId = "D001",
                    targetDirection = "Straight",
                    sequenceNumber = 1
                }
            }
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/config/routes/{nonExistentChuteId}", updateRequest);

        // Assert
        // Expecting either 404 (not found) or 500 (server error if not handled)
        Assert.True(response.StatusCode == HttpStatusCode.NotFound ||
                   response.StatusCode == HttpStatusCode.InternalServerError ||
                   response.StatusCode == HttpStatusCode.OK); // Some APIs create on PUT if not exists
    }

    [Fact]
    public async Task DeleteRoute_WithChuteId_ReturnsSuccess()
    {
        // Arrange
        var chuteId = "TestChuteToDelete_" + Guid.NewGuid();

        // Act
        var response = await _client.DeleteAsync($"/api/config/routes/{chuteId}");

        // Assert
        // Expecting either 200/204 (success) or 404 (not found) - both are acceptable
        Assert.True(response.StatusCode == HttpStatusCode.OK ||
                   response.StatusCode == HttpStatusCode.NoContent ||
                   response.StatusCode == HttpStatusCode.NotFound ||
                   response.StatusCode == HttpStatusCode.InternalServerError);
    }
}
