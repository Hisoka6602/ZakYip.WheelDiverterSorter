using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using ZakYip.WheelDiverterSorter.Host.Models;
using ZakYip.WheelDiverterSorter.Host.Models.Panel;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;

namespace ZakYip.WheelDiverterSorter.Host.IntegrationTests;

/// <summary>
/// 面板配置控制器集成测试
/// </summary>
public class PanelConfigControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public PanelConfigControllerTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetPanelConfig_ShouldReturnSuccess()
    {
        // Act
        var response = await _client.GetAsync("/api/config/panel");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var content = await response.Content.ReadAsStringAsync();
        Assert.NotNull(content);
        Assert.NotEmpty(content);
    }

    [Fact]
    public async Task GetPanelTemplate_ShouldReturnSuccess()
    {
        // Act
        var response = await _client.GetAsync("/api/config/panel/template");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var content = await response.Content.ReadAsStringAsync();
        Assert.NotNull(content);
        Assert.NotEmpty(content);
    }

    [Fact]
    public async Task GetPanelConfig_ShouldBeAccessible()
    {
        // Act
        var response = await _client.GetAsync("/api/config/panel");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var content = await response.Content.ReadAsStringAsync();
        Assert.NotNull(content);
        Assert.NotEmpty(content);
    }

    [Fact]
    public async Task ResetPanelConfig_ShouldSucceed()
    {
        // Act
        var response = await _client.PostAsync("/api/config/panel/reset", null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var content = await response.Content.ReadAsStringAsync();
        Assert.NotNull(content);
    }

    [Fact]
    public async Task UpdatePanelConfig_WithValidEmergencyStopBuzzer_ShouldSucceed()
    {
        // Arrange
        var request = new PanelConfigRequest
        {
            Enabled = true,
            PollingIntervalMs = 100,
            DebounceMs = 50,
            EmergencyStopBuzzer = new EmergencyStopBuzzerConfigDto
            {
                DurationSeconds = 10,
                OutputBit = 100,
                OutputLevel = TriggerLevel.ActiveHigh
            }
        };

        // Act
        var response = await _client.PutAsJsonAsync("/api/config/panel", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PanelConfigResponse>>();
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.NotNull(result.Data.EmergencyStopBuzzer);
        Assert.Equal(10, result.Data.EmergencyStopBuzzer.DurationSeconds);
        Assert.Equal(100, result.Data.EmergencyStopBuzzer.OutputBit);
        Assert.Equal(TriggerLevel.ActiveHigh, result.Data.EmergencyStopBuzzer.OutputLevel);
    }

    [Fact]
    public async Task UpdatePanelConfig_WithInvalidEmergencyStopBuzzerDuration_ShouldFail()
    {
        // Arrange - duration > 60
        var request = new PanelConfigRequest
        {
            Enabled = true,
            PollingIntervalMs = 100,
            DebounceMs = 50,
            EmergencyStopBuzzer = new EmergencyStopBuzzerConfigDto
            {
                DurationSeconds = 65, // Invalid: > 60
                OutputBit = 100,
                OutputLevel = TriggerLevel.ActiveHigh
            }
        };

        // Act
        var response = await _client.PutAsJsonAsync("/api/config/panel", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdatePanelConfig_WithInvalidEmergencyStopBuzzerOutputBit_ShouldFail()
    {
        // Arrange - outputBit > 1023
        var request = new PanelConfigRequest
        {
            Enabled = true,
            PollingIntervalMs = 100,
            DebounceMs = 50,
            EmergencyStopBuzzer = new EmergencyStopBuzzerConfigDto
            {
                DurationSeconds = 10,
                OutputBit = 1500, // Invalid: > 1023
                OutputLevel = TriggerLevel.ActiveHigh
            }
        };

        // Act
        var response = await _client.PutAsJsonAsync("/api/config/panel", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdatePanelConfig_WithNullEmergencyStopBuzzer_ShouldSucceed()
    {
        // Arrange - EmergencyStopBuzzer is optional
        var request = new PanelConfigRequest
        {
            Enabled = true,
            PollingIntervalMs = 100,
            DebounceMs = 50,
            EmergencyStopBuzzer = null
        };

        // Act
        var response = await _client.PutAsJsonAsync("/api/config/panel", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UpdatePanelConfig_WithZeroDurationEmergencyStopBuzzer_ShouldSucceed()
    {
        // Arrange - 0 seconds is valid (disables buzzer)
        var request = new PanelConfigRequest
        {
            Enabled = true,
            PollingIntervalMs = 100,
            DebounceMs = 50,
            EmergencyStopBuzzer = new EmergencyStopBuzzerConfigDto
            {
                DurationSeconds = 0, // Valid: 0 to disable
                OutputBit = 100,
                OutputLevel = TriggerLevel.ActiveLow
            }
        };

        // Act
        var response = await _client.PutAsJsonAsync("/api/config/panel", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PanelConfigResponse>>();
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.NotNull(result.Data.EmergencyStopBuzzer);
        Assert.Equal(0, result.Data.EmergencyStopBuzzer.DurationSeconds);
    }
}
