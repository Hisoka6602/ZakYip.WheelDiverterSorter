using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using ZakYip.WheelDiverterSorter.Host.Models;
using ZakYip.WheelDiverterSorter.Host.Models.Config;
using ZakYip.WheelDiverterSorter.Host.Models.Communication;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;
using ZakYip.WheelDiverterSorter.Configuration.Persistence.Repositories.LiteDb;
using ZakYip.WheelDiverterSorter.Core.Enums;
using Xunit;

namespace ZakYip.WheelDiverterSorter.Host.IntegrationTests;

/// <summary>
/// Comprehensive API endpoint tests to ensure all endpoints are accessible and functional.
/// This test class covers ALL API endpoints to prevent regression.
/// </summary>
public class AllApiEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public AllApiEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    #region Debug API Tests

    [Fact]
    public async Task DebugSort_WithValidRequest_ReturnsSuccess()
    {
        // Arrange
        var request = new ChuteChangeRequest
        {
            ParcelId = 1001L,
            RequestedChuteId = 1L
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/diverts/change-chute", request);

        // Assert
        Assert.True(response.IsSuccessStatusCode, 
            $"Expected success status code but got {response.StatusCode}");
        
        var result = await response.Content.ReadFromJsonAsync<ChuteChangeResponse>(TestJsonOptions.GetOptions());
        Assert.NotNull(result);
        Assert.Equal(request.ParcelId, result.ParcelId);
    }

    [Fact]
    public async Task DebugSort_WithInvalidChuteId_ReturnsBadRequest()
    {
        // Arrange
        var request = new ChuteChangeRequest
        {
            ParcelId = 1002L,
            RequestedChuteId = 0L // Invalid: must be > 0
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/diverts/change-chute", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    #endregion

    #region IO Driver Config API Tests (Leadshine)

    [Fact]
    public async Task GetLeadshineIoDriverConfig_ReturnsSuccess()
    {
        // Act
        var response = await _client.GetAsync("/api/hardware/leadshine");

        // Assert
        Assert.True(response.IsSuccessStatusCode,
            $"Expected success status code but got {response.StatusCode}");
    }

    [Fact]
    public async Task ResetLeadshineIoDriverConfig_ReturnsSuccess()
    {
        // Act
        var response = await _client.PostAsync("/api/hardware/leadshine/reset", null);

        // Assert
        Assert.True(response.IsSuccessStatusCode,
            $"Expected success status code but got {response.StatusCode}");
    }

    #endregion

    #region Sensor Config API Tests

    [Fact]
    public async Task GetSensorConfig_ReturnsSuccess()
    {
        // Act
        var response = await _client.GetAsync("/api/hardware/leadshine/sensors");

        // Assert
        Assert.True(response.IsSuccessStatusCode,
            $"Expected success status code but got {response.StatusCode}");
        
        var config = await response.Content.ReadFromJsonAsync<SensorConfiguration>(TestJsonOptions.GetOptions());
        Assert.NotNull(config);
    }

    [Fact]
    public async Task ResetSensorConfig_ReturnsSuccess()
    {
        // Act
        var response = await _client.PostAsync("/api/hardware/leadshine/sensors/reset", null);

        // Assert
        Assert.True(response.IsSuccessStatusCode,
            $"Expected success status code but got {response.StatusCode}");
        
        var config = await response.Content.ReadFromJsonAsync<SensorConfiguration>(TestJsonOptions.GetOptions());
        Assert.NotNull(config);
    }

    #endregion

    #region System Config API Tests

    [Fact]
    public async Task GetSystemConfig_ReturnsSuccess()
    {
        // Act
        var response = await _client.GetAsync("/api/config/system");

        // Assert
        Assert.True(response.IsSuccessStatusCode,
            $"Expected success status code but got {response.StatusCode}");
        
        var config = await response.Content.ReadFromJsonAsync<SystemConfigResponse>(TestJsonOptions.GetOptions());
        Assert.NotNull(config);
    }

    [Fact]
    public async Task GetSystemConfigTemplate_ReturnsSuccess()
    {
        // Act
        var response = await _client.GetAsync("/api/config/system/template");

        // Assert
        Assert.True(response.IsSuccessStatusCode,
            $"Expected success status code but got {response.StatusCode}");
        
        var template = await response.Content.ReadFromJsonAsync<SystemConfigRequest>(TestJsonOptions.GetOptions());
        Assert.NotNull(template);
    }

    [Fact]
    public async Task ResetSystemConfig_ReturnsSuccess()
    {
        // Act
        var response = await _client.PostAsync("/api/config/system/reset", null);

        // Assert
        Assert.True(response.IsSuccessStatusCode,
            $"Expected success status code but got {response.StatusCode}");
        
        var config = await response.Content.ReadFromJsonAsync<SystemConfigResponse>(TestJsonOptions.GetOptions());
        Assert.NotNull(config);
    }

    #endregion

    #region Communication API Tests

    [Fact]
    public async Task GetCommunicationConfig_ReturnsSuccess()
    {
        // Act
        var response = await _client.GetAsync("/api/communication/config");

        // Assert
        Assert.True(response.IsSuccessStatusCode,
            $"Expected success status code but got {response.StatusCode}");
    }

    [Fact]
    public async Task GetPersistedCommunicationConfig_ReturnsSuccess()
    {
        // Act
        var response = await _client.GetAsync("/api/communication/config/persisted");

        // Assert
        Assert.True(response.IsSuccessStatusCode,
            $"Expected success status code but got {response.StatusCode}");
        
        var config = await response.Content.ReadFromJsonAsync<CommunicationConfiguration>(TestJsonOptions.GetOptions());
        Assert.NotNull(config);
    }

    [Fact]
    public async Task ResetPersistedCommunicationConfig_ReturnsSuccess()
    {
        // Act
        var response = await _client.PostAsync("/api/communication/config/persisted/reset", null);

        // Assert
        Assert.True(response.IsSuccessStatusCode,
            $"Expected success status code but got {response.StatusCode}");
        
        var config = await response.Content.ReadFromJsonAsync<CommunicationConfiguration>(TestJsonOptions.GetOptions());
        Assert.NotNull(config);
    }

    [Fact]
    public async Task GetCommunicationStatus_ReturnsSuccess()
    {
        // Act
        var response = await _client.GetAsync("/api/communication/status");

        // Assert
        Assert.True(response.IsSuccessStatusCode,
            $"Expected success status code but got {response.StatusCode}");
        
        var status = await response.Content.ReadFromJsonAsync<CommunicationStatusResponse>(TestJsonOptions.GetOptions());
        Assert.NotNull(status);
        Assert.NotNull(status.Mode);
    }

    [Fact]
    public async Task ResetCommunicationStats_ReturnsSuccess()
    {
        // Act
        var response = await _client.PostAsync("/api/communication/reset-stats", null);

        // Assert
        Assert.True(response.IsSuccessStatusCode,
            $"Expected success status code but got {response.StatusCode}");
    }

    [Fact]
    public async Task TestCommunication_ReturnsSuccess()
    {
        // Act
        var response = await _client.PostAsync("/api/communication/test", null);

        // Assert
        // Note: This test will always return 200 OK, but Success field may be false
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<ConnectionTestResponse>(TestJsonOptions.GetOptions());
        Assert.NotNull(result);
        // Connection may fail if RuleEngine is not running, which is expected
    }

    #endregion
}
