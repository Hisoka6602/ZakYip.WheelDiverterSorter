using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using ZakYip.WheelDiverterSorter.Host.Models;

namespace ZakYip.WheelDiverterSorter.Host.IntegrationTests;

/// <summary>
/// 检测开关配置控制器集成测试
/// </summary>
public class DetectionSwitchesControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public DetectionSwitchesControllerTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetDetectionSwitches_ShouldReturnSuccess()
    {
        // Act
        var response = await _client.GetAsync("/api/sorting/detection-switches");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var content = await response.Content.ReadAsStringAsync();
        Assert.NotNull(content);
        Assert.NotEmpty(content);

        // Verify response contains expected fields
        var jsonDoc = JsonDocument.Parse(content);
        var dataElement = jsonDoc.RootElement.GetProperty("data");
        
        Assert.True(dataElement.TryGetProperty("enableInterferenceDetection", out _));
        Assert.True(dataElement.TryGetProperty("enableTimeoutDetection", out _));
        Assert.True(dataElement.TryGetProperty("enableParcelLossDetection", out _));
        Assert.True(dataElement.TryGetProperty("updatedAt", out _));
    }

    [Fact]
    public async Task UpdateDetectionSwitches_WithAllSwitches_ShouldReturnSuccess()
    {
        // Arrange
        var request = new UpdateDetectionSwitchesRequest
        {
            EnableInterferenceDetection = false,
            EnableParcelLossDetection = true
        };

        // Act
        var response = await _client.PutAsJsonAsync("/api/sorting/detection-switches", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var content = await response.Content.ReadAsStringAsync();
        Assert.NotNull(content);
        
        var jsonDoc = JsonDocument.Parse(content);
        var dataElement = jsonDoc.RootElement.GetProperty("data");
        
        Assert.False(dataElement.GetProperty("enableInterferenceDetection").GetBoolean());
        Assert.True(dataElement.GetProperty("enableParcelLossDetection").GetBoolean());
        // Note: We don't assert enableTimeoutDetection here because it depends on conveyor segment configuration
    }

    [Fact]
    public async Task UpdateDetectionSwitches_WithPartialUpdate_ShouldReturnSuccess()
    {
        // Arrange - Only update interference detection
        var request = new UpdateDetectionSwitchesRequest
        {
            EnableInterferenceDetection = true
        };

        // Act
        var response = await _client.PutAsJsonAsync("/api/sorting/detection-switches", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var content = await response.Content.ReadAsStringAsync();
        Assert.NotNull(content);
        
        var jsonDoc = JsonDocument.Parse(content);
        var dataElement = jsonDoc.RootElement.GetProperty("data");
        
        // Verify the updated field
        Assert.True(dataElement.GetProperty("enableInterferenceDetection").GetBoolean());
    }

    [Fact]
    public async Task UpdateDetectionSwitches_WithEmptyRequest_ShouldReturnBadRequest()
    {
        // Arrange - No switches provided
        var request = new UpdateDetectionSwitchesRequest();

        // Act
        var response = await _client.PutAsJsonAsync("/api/sorting/detection-switches", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateDetectionSwitches_WithNullRequest_ShouldReturnBadRequest()
    {
        // Act
        var content = new StringContent("", System.Text.Encoding.UTF8, "application/json");
        var response = await _client.PutAsync("/api/sorting/detection-switches", content);

        // Assert
        // Empty JSON is valid but will be deserialized to an empty request object
        // which should fail validation for having no switches to update
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetDetectionSwitches_AfterUpdate_ShouldReturnUpdatedValues()
    {
        // Arrange - Update switches to specific values (skip timeout detection as it depends on segment config)
        var updateRequest = new UpdateDetectionSwitchesRequest
        {
            EnableInterferenceDetection = true,
            EnableParcelLossDetection = false
        };

        // Act - Update
        var updateResponse = await _client.PutAsJsonAsync("/api/sorting/detection-switches", updateRequest);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        // Act - Get
        var getResponse = await _client.GetAsync("/api/sorting/detection-switches");

        // Assert
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        
        var content = await getResponse.Content.ReadAsStringAsync();
        var jsonDoc = JsonDocument.Parse(content);
        var dataElement = jsonDoc.RootElement.GetProperty("data");
        
        Assert.True(dataElement.GetProperty("enableInterferenceDetection").GetBoolean());
        Assert.False(dataElement.GetProperty("enableParcelLossDetection").GetBoolean());
        // Note: We don't assert enableTimeoutDetection here because it depends on conveyor segment configuration
    }

    [Fact]
    public async Task UpdateDetectionSwitches_MultiplePartialUpdates_ShouldMaintainOtherValues()
    {
        // Arrange - Set initial state (skip timeout detection as it depends on segment config)
        var initialRequest = new UpdateDetectionSwitchesRequest
        {
            EnableInterferenceDetection = false,
            EnableParcelLossDetection = true
        };
        await _client.PutAsJsonAsync("/api/sorting/detection-switches", initialRequest);

        // Act - Update only interference detection
        var partialRequest = new UpdateDetectionSwitchesRequest
        {
            EnableInterferenceDetection = true
        };
        var response = await _client.PutAsJsonAsync("/api/sorting/detection-switches", partialRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var content = await response.Content.ReadAsStringAsync();
        var jsonDoc = JsonDocument.Parse(content);
        var dataElement = jsonDoc.RootElement.GetProperty("data");
        
        // Interference detection should be updated
        Assert.True(dataElement.GetProperty("enableInterferenceDetection").GetBoolean());
        
        // Parcel loss detection should remain unchanged
        Assert.True(dataElement.GetProperty("enableParcelLossDetection").GetBoolean());
        // Note: We don't assert enableTimeoutDetection here because it depends on conveyor segment configuration
    }
}
