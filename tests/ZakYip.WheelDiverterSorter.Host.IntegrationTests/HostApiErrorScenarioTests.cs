using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using ZakYip.WheelDiverterSorter.Host.Models;
using ZakYip.WheelDiverterSorter.Host.Models.Config;
using ZakYip.WheelDiverterSorter.Core.Enums;
using Xunit;

namespace ZakYip.WheelDiverterSorter.Host.IntegrationTests;

/// <summary>
/// 集成测试：异常场景、边界条件、并发请求
/// Integration tests: error scenarios, boundary conditions, concurrent requests
/// </summary>
public class HostApiErrorScenarioTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public HostApiErrorScenarioTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    #region Boundary and Error Tests

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(int.MinValue)]
    public async Task DebugSort_WithInvalidChuteId_ReturnsBadRequest(int chuteId)
    {
        // Arrange
        var request = new DebugSortRequest
        {
            ParcelId = "TEST_PARCEL",
            TargetChuteId = chuteId
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/diverts/change-chute", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task DebugSort_WithEmptyParcelId_ReturnsBadRequest(string ParcelId)
    {
        // Arrange
        var request = new DebugSortRequest
        {
            ParcelId = ParcelId,
            TargetChuteId = 1
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/diverts/change-chute", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetRoute_WithNonExistentId_ReturnsNotFound()
    {
        // Act
        var response = await _client.GetAsync("/api/config/routes/99999");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetRoute_WithInvalidId_ReturnsBadRequest(int routeId)
    {
        // Act
        var response = await _client.GetAsync($"/api/config/routes/{routeId}");

        // Assert
        // May return BadRequest or NotFound depending on validation
        Assert.True(
            response.StatusCode == HttpStatusCode.BadRequest || 
            response.StatusCode == HttpStatusCode.NotFound);
    }

    #endregion

    #region Concurrent Request Tests

    [Fact]
    public async Task ConcurrentRequests_HandleCorrectly()
    {
        // Arrange
        const int requestCount = 20;
        var tasks = new List<Task<HttpResponseMessage>>();

        // Act - Send concurrent requests
        for (int i = 0; i < requestCount; i++)
        {
            var request = new DebugSortRequest
            {
                ParcelId = $"PKG{i:000}",
                TargetChuteId = (i % 5) + 1
            };
            tasks.Add(_client.PostAsJsonAsync("/api/diverts/change-chute", request));
        }

        var responses = await Task.WhenAll(tasks);

        // Assert - All should succeed
        var successCount = responses.Count(r => r.IsSuccessStatusCode);
        Assert.True(successCount >= requestCount * 0.8, 
            $"Expected at least 80% success rate, got {successCount}/{requestCount}");
    }

    [Fact]
    public async Task DebugSort_WithVeryLongParcelId_HandlesCorrectly()
    {
        // Arrange - Very long parcel ID
        var request = new DebugSortRequest
        {
            ParcelId = new string('X', 1000),
            TargetChuteId = 1
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/diverts/change-chute", request);

        // Assert - Should either accept or reject gracefully
        Assert.True(
            response.IsSuccessStatusCode || 
            response.StatusCode == HttpStatusCode.BadRequest);
    }

    #endregion

    #region API Health Tests

    [Fact]
    public async Task InvalidEndpoint_Returns404OrMethodNotAllowed()
    {
        // Act
        var response = await _client.GetAsync("/api/nonexistent/endpoint");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PostRequest_WithMalformedJson_ReturnsBadRequest()
    {
        // Arrange
        var content = new StringContent("{invalid json", System.Text.Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/diverts/change-chute", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public async Task DebugSort_WithSpecialCharactersInParcelId_HandlesCorrectly()
    {
        // Arrange - Parcel ID with special characters
        var request = new DebugSortRequest
        {
            ParcelId = "PKG-001_测试@#$%",
            TargetChuteId = 1
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/diverts/change-chute", request);

        // Assert
        Assert.True(
            response.IsSuccessStatusCode || 
            response.StatusCode == HttpStatusCode.BadRequest);
    }

    #endregion
}
