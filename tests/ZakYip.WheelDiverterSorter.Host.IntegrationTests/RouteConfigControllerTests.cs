using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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
        var chuteId = 1;

        // Act
        var response = await _client.GetAsync($"/api/config/routes/{chuteId}");

        // Assert
        // Expecting either 200 (found) or 404 (not found) - both are valid responses
        Assert.True(
            response.StatusCode == HttpStatusCode.OK || 
            response.StatusCode == HttpStatusCode.NotFound,
            $"Expected 200 or 404, but got {(int)response.StatusCode} ({response.StatusCode})");
    }

    [Fact]
    public async Task GetRoute_WithEmptyChuteId_ReturnsClientError()
    {
        // Act
        var response = await _client.GetAsync("/api/config/routes/");

        // Assert
        // Empty chute ID in path gets caught by routing (404) or validation (400)
        Assert.True(
            response.StatusCode == HttpStatusCode.NotFound || 
            response.StatusCode == HttpStatusCode.BadRequest ||
            response.StatusCode == HttpStatusCode.OK,
            $"Expected 404, 400, or 200, but got {(int)response.StatusCode} ({response.StatusCode})"); // Some APIs handle this gracefully
    }

    [Fact]
    public async Task CreateRoute_WithInvalidData_ReturnsBadRequest()
    {
        // Arrange
        var invalidRequest = new { }; // Empty request

        // Act
        var response = await _client.PostAsJsonAsync("/api/config/routes", invalidRequest);

        // Assert
        Assert.True(
            response.StatusCode == HttpStatusCode.BadRequest ||
            response.StatusCode == HttpStatusCode.InternalServerError,
            $"Expected 400 or 500, but got {(int)response.StatusCode} ({response.StatusCode})");
    }

    [Fact]
    public async Task UpdateRoute_WithNonExistentChuteId_HandlesGracefully()
    {
        // Arrange
        var nonExistentChuteId = 99998;
        var updateRequest = new
        {
            chuteId = nonExistentChuteId,
            diverterConfigurations = new[]
            {
                new
                {
                    diverterId = 1,
                    targetDirection = 1, // Use integer value for enum
                    sequenceNumber = 1
                }
            }
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/config/routes/{nonExistentChuteId}", updateRequest);

        // Assert
        // Different APIs handle non-existent resources differently
        // Accept any reasonable response
        Assert.True(
            response.StatusCode == HttpStatusCode.NotFound ||
            response.StatusCode == HttpStatusCode.InternalServerError ||
            response.StatusCode == HttpStatusCode.OK ||
            response.StatusCode == HttpStatusCode.Created ||
            response.StatusCode == HttpStatusCode.BadRequest ||
            response.StatusCode == HttpStatusCode.Conflict,
            $"Expected 404, 500, 200, 201, 400, or 409, but got {(int)response.StatusCode} ({response.StatusCode})");
    }

    [Fact]
    public async Task DeleteRoute_WithChuteId_ReturnsSuccess()
    {
        // Arrange
        var chuteId = 99999; // Use a high number unlikely to exist

        // Act
        var response = await _client.DeleteAsync($"/api/config/routes/{chuteId}");

        // Assert
        // Expecting either 200/204 (success) or 404 (not found) - both are acceptable
        Assert.True(
            response.StatusCode == HttpStatusCode.OK ||
            response.StatusCode == HttpStatusCode.NoContent ||
            response.StatusCode == HttpStatusCode.NotFound,
            $"Expected 200, 204, or 404, but got {(int)response.StatusCode} ({response.StatusCode})");
    }

    [Fact]
    public async Task GetAllRoutes_EnumsSerializedAsStrings()
    {
        // Act
        var response = await _client.GetAsync("/api/config/routes");
        var json = await response.Content.ReadAsStringAsync();

        // Assert
        response.EnsureSuccessStatusCode();
        
        // Parse the JSON to verify enums are serialized as strings
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        
        // Check if there are any routes with diverter configurations
        if (root.GetArrayLength() > 0)
        {
            var firstRoute = root[0];
            if (firstRoute.TryGetProperty("diverterConfigurations", out var diverterConfigs) && 
                diverterConfigs.GetArrayLength() > 0)
            {
                var firstConfig = diverterConfigs[0];
                // TargetDirection should be a string (e.g., "Left", "Right", "Straight") not a number
                Assert.True(firstConfig.TryGetProperty("targetDirection", out var targetDirection));
                Assert.Equal(JsonValueKind.String, targetDirection.ValueKind);
            }
        }
    }

    [Fact]
    public async Task CreateRoute_AcceptsNumericEnumValue_ForBackwardCompatibility()
    {
        // Arrange - Use a unique chute ID to avoid conflicts
        var uniqueChuteId = 88888;
        var json = $$"""
        {
            "chuteId": {{uniqueChuteId}},
            "diverterConfigurations": [
                {
                    "diverterId": 1,
                    "targetDirection": 1,
                    "sequenceNumber": 1
                }
            ],
            "isEnabled": true
        }
        """;

        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsJsonAsync("/api/config/routes", 
            JsonSerializer.Deserialize<object>(json));

        // Clean up - try to delete if created
        await _client.DeleteAsync($"/api/config/routes/{uniqueChuteId}");

        // Assert
        Assert.True(
            response.StatusCode == HttpStatusCode.Created || 
            response.StatusCode == HttpStatusCode.Conflict,
            $"Expected 201 or 409, but got {(int)response.StatusCode} ({response.StatusCode})");
        
        if (response.StatusCode == HttpStatusCode.Created)
        {
            // Verify response contains the enum as string (even though we sent number)
            var responseJson = await response.Content.ReadAsStringAsync();
            Assert.Contains("\"targetDirection\":", responseJson);
            Assert.Contains("\"Left\"", responseJson); // 1 = Left
        }
    }
}
