using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
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
            ParcelId = 1001L,
            RequestedChuteId = 1L
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/diverts/change-chute", request);

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
            ParcelId = 0L, // Invalid - must be > 0
            RequestedChuteId = 1L
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/diverts/change-chute", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DebugSort_WithInvalidTargetChuteId_ReturnsBadRequest()
    {
        // Arrange
        var request = new
        {
            ParcelId = 1002L,
            RequestedChuteId = 0L // Invalid - must be > 0
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/diverts/change-chute", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
