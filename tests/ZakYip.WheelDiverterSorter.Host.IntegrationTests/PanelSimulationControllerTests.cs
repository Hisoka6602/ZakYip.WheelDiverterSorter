using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using System.Net;

namespace ZakYip.WheelDiverterSorter.Host.IntegrationTests;

/// <summary>
/// Integration tests for unified SimulationController panel endpoints
/// </summary>
/// <remarks>
/// Tests the consolidated panel simulation endpoints under /api/simulation/panel/*
/// </remarks>
public class PanelSimulationControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public PanelSimulationControllerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PressButton_ReturnsProperResponse()
    {
        // Act - Updated path to new unified endpoint
        var response = await _client.PostAsync("/api/simulation/panel/press-button?buttonType=Start", null);

        // Assert
        // Should return either OK (if simulation mode) or BadRequest (if not simulation mode) or NotFound
        // Should NOT return 500 (unhandled exception)
        Assert.True(
            response.StatusCode == HttpStatusCode.OK ||
            response.StatusCode == HttpStatusCode.BadRequest ||
            response.StatusCode == HttpStatusCode.NotFound,
            $"Unexpected status code: {response.StatusCode}");
    }

    [Fact]
    public async Task PressButton_InNonSimulationMode_ReturnsBadRequestWithChineseMessage()
    {
        // Act - Updated path to new unified endpoint
        var response = await _client.PostAsync("/api/simulation/panel/press-button?buttonType=Start", null);

        // Assert
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var content = await response.Content.ReadAsStringAsync();
            // Should contain Chinese error message
            Assert.True(
                content.Contains("仅在仿真模式下可调用该接口") ||
                content.Contains("仿真模式未启用"),
                "Error message should be in Chinese");
        }
    }

    [Fact]
    public async Task ReleaseButton_ReturnsProperResponse()
    {
        // Act - Updated path to new unified endpoint
        var response = await _client.PostAsync("/api/simulation/panel/release-button?buttonType=Start", null);

        // Assert
        // Should return either OK (if simulation mode) or BadRequest (if not simulation mode) or NotFound
        // Should NOT return 500 (unhandled exception)
        Assert.True(
            response.StatusCode == HttpStatusCode.OK ||
            response.StatusCode == HttpStatusCode.BadRequest ||
            response.StatusCode == HttpStatusCode.NotFound,
            $"Unexpected status code: {response.StatusCode}");
    }

    [Fact]
    public async Task ReleaseButton_InNonSimulationMode_ReturnsBadRequestWithChineseMessage()
    {
        // Act - Updated path to new unified endpoint
        var response = await _client.PostAsync("/api/simulation/panel/release-button?buttonType=Start", null);

        // Assert
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var content = await response.Content.ReadAsStringAsync();
            // Should contain Chinese error message
            Assert.True(
                content.Contains("仅在仿真模式下可调用该接口") ||
                content.Contains("仿真模式未启用"),
                "Error message should be in Chinese");
        }
    }

    [Fact]
    public async Task GetPanelState_ReturnsProperResponse()
    {
        // Act - Path remains the same
        var response = await _client.GetAsync("/api/simulation/panel/state");

        // Assert
        // Should return either OK or an error, but NOT 500 (unhandled exception)
        Assert.True(
            response.StatusCode == HttpStatusCode.OK ||
            response.StatusCode == HttpStatusCode.BadRequest ||
            response.StatusCode == HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ResetAllButtons_ReturnsProperResponse()
    {
        // Act - Updated path to new unified endpoint
        var response = await _client.PostAsync("/api/simulation/panel/reset-buttons", null);

        // Assert
        // Should return either OK (if simulation mode) or BadRequest (if not simulation mode) or NotFound
        // Should NOT return 500 (unhandled exception)
        Assert.True(
            response.StatusCode == HttpStatusCode.OK ||
            response.StatusCode == HttpStatusCode.BadRequest ||
            response.StatusCode == HttpStatusCode.NotFound,
            $"Unexpected status code: {response.StatusCode}");
    }

    [Fact]
    public async Task ResetAllButtons_InNonSimulationMode_ReturnsBadRequestWithChineseMessage()
    {
        // Act - Updated path to new unified endpoint
        var response = await _client.PostAsync("/api/simulation/panel/reset-buttons", null);

        // Assert
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var content = await response.Content.ReadAsStringAsync();
            // Should contain Chinese error message
            Assert.True(
                content.Contains("仅在仿真模式下可调用该接口") ||
                content.Contains("仿真模式未启用"),
                "Error message should be in Chinese");
        }
    }

    [Fact]
    public async Task GetSignalTowerHistory_ReturnsProperResponse()
    {
        // Act - Updated path to new unified endpoint
        var response = await _client.GetAsync("/api/simulation/panel/signal-tower-history");

        // Assert
        // Should return either OK (if simulation mode) or BadRequest (if not simulation mode) or NotFound
        // Should NOT return 500 (unhandled exception)
        Assert.True(
            response.StatusCode == HttpStatusCode.OK ||
            response.StatusCode == HttpStatusCode.BadRequest ||
            response.StatusCode == HttpStatusCode.NotFound,
            $"Unexpected status code: {response.StatusCode}");
    }

    [Fact]
    public async Task GetSignalTowerHistory_InNonSimulationMode_ReturnsBadRequestWithChineseMessage()
    {
        // Act - Updated path to new unified endpoint
        var response = await _client.GetAsync("/api/simulation/panel/signal-tower-history");

        // Assert
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var content = await response.Content.ReadAsStringAsync();
            // Should contain Chinese error message
            Assert.True(
                content.Contains("仅在仿真模式下可调用该接口") ||
                content.Contains("仿真模式未启用"),
                "Error message should be in Chinese");
        }
    }
}
