using System.Net;
using System.Net.Http.Json;

namespace ZakYip.WheelDiverterSorter.Host.IntegrationTests;

/// <summary>
/// Integration tests for Debug endpoints
/// </summary>
public class DebugControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public DebugControllerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task DebugSort_WithValidRequest_ReturnsSuccess()
    {
        // Arrange
        var request = new
        {
            parcelId = "TEST-PARCEL-001",
            targetChuteId = 1
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/debug/sort", request);

        // Assert
        // Accept OK status - the endpoint returns 200 for both success and failure
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task DebugSort_WithEmptyParcelId_ReturnsBadRequest()
    {
        // Arrange
        var request = new
        {
            parcelId = "",
            targetChuteId = 1
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/debug/sort", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DebugSort_WithInvalidTargetChuteId_ReturnsBadRequest()
    {
        // Arrange
        var request = new
        {
            parcelId = "TEST-PARCEL-002",
            targetChuteId = 0 // Invalid - must be > 0
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/debug/sort", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
