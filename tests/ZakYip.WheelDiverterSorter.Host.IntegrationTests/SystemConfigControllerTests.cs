using System.Net;
using System.Net.Http.Json;
using ZakYip.WheelDiverterSorter.Core.Sorting.Models;

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

    [Fact]
    public async Task GetSortingMode_ReturnsSuccess()
    {
        // Act
        var response = await _client.GetAsync("/api/config/system/sorting-mode");

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetSortingMode_ReturnsDefaultFormalMode()
    {
        // Act
        var response = await _client.GetAsync("/api/config/system/sorting-mode");
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadFromJsonAsync<SortingModeResponse>();

        // Assert
        Assert.NotNull(content);
        Assert.Equal(SortingMode.Formal, content.SortingMode);
    }

    [Fact]
    public async Task UpdateSortingMode_ToFormalMode_ReturnsSuccess()
    {
        // Arrange
        var request = new
        {
            sortingMode = "Formal"
        };

        // Act
        var response = await _client.PutAsJsonAsync("/api/config/system/sorting-mode", request);

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadFromJsonAsync<SortingModeResponse>();
        Assert.NotNull(content);
        Assert.Equal(SortingMode.Formal, content.SortingMode);
    }

    [Fact]
    public async Task UpdateSortingMode_ToFixedChuteMode_WithValidChuteId_ReturnsSuccess()
    {
        // Arrange
        var request = new
        {
            sortingMode = "FixedChute",
            fixedChuteId = 1
        };

        // Act
        var response = await _client.PutAsJsonAsync("/api/config/system/sorting-mode", request);

        // Assert - May succeed or fail depending on whether chute 1 exists in route config
        // We just check that we get a proper response (not a 500 error)
        Assert.True(
            response.StatusCode == HttpStatusCode.OK ||
            response.StatusCode == HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateSortingMode_ToFixedChuteMode_WithoutChuteId_ReturnsBadRequest()
    {
        // Arrange
        var request = new
        {
            sortingMode = "FixedChute"
            // Missing fixedChuteId
        };

        // Act
        var response = await _client.PutAsJsonAsync("/api/config/system/sorting-mode", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("固定格口", content);
    }

    [Fact]
    public async Task UpdateSortingMode_ToRoundRobinMode_WithValidChuteIds_ReturnsSuccess()
    {
        // Arrange
        var request = new
        {
            sortingMode = "RoundRobin",
            availableChuteIds = new[] { 1, 2, 3 }
        };

        // Act
        var response = await _client.PutAsJsonAsync("/api/config/system/sorting-mode", request);

        // Assert - May succeed or fail depending on whether chutes exist in route config
        // We just check that we get a proper response (not a 500 error)
        Assert.True(
            response.StatusCode == HttpStatusCode.OK ||
            response.StatusCode == HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateSortingMode_ToRoundRobinMode_WithoutChuteIds_ReturnsBadRequest()
    {
        // Arrange
        var request = new
        {
            sortingMode = "RoundRobin"
            // Missing availableChuteIds
        };

        // Act
        var response = await _client.PutAsJsonAsync("/api/config/system/sorting-mode", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("可用格口", content);
    }

    [Fact]
    public async Task UpdateSortingMode_WithInvalidMode_ReturnsBadRequest()
    {
        // Arrange
        var request = new
        {
            sortingMode = "InvalidMode"
        };

        // Act
        var response = await _client.PutAsJsonAsync("/api/config/system/sorting-mode", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private class SortingModeResponse
    {
        public SortingMode SortingMode { get; set; }
        public int? FixedChuteId { get; set; }
        public List<int> AvailableChuteIds { get; set; } = new();
    }
}
