using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using ZakYip.WheelDiverterSorter.Host.Models;

namespace ZakYip.WheelDiverterSorter.Host.IntegrationTests;

/// <summary>
/// Integration tests for the system status endpoint
/// </summary>
public class SystemStatusEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public SystemStatusEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetSystemStatus_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/api/system/status");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var statusResponse = await response.Content.ReadFromJsonAsync<SystemStatusResponse>(TestJsonOptions.GetOptions());
        Assert.NotNull(statusResponse);
        Assert.NotNull(statusResponse.SystemState);
        Assert.NotNull(statusResponse.EnvironmentMode);
        Assert.Contains(statusResponse.EnvironmentMode, new[] { "Production", "Simulation" });
    }

    [Fact]
    public async Task GetSystemStatus_ReturnsValidSystemState()
    {
        // Act
        var response = await _client.GetAsync("/api/system/status");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var statusResponse = await response.Content.ReadFromJsonAsync<SystemStatusResponse>(TestJsonOptions.GetOptions());
        Assert.NotNull(statusResponse);
        
        // System state should be one of the valid enum values
        var validStates = new[] { "Booting", "Ready", "Running", "Paused", "Faulted", "EmergencyStop" };
        Assert.Contains(statusResponse.SystemState, validStates);
    }

    [Fact]
    public async Task GetSystemStatus_SupportsHighConcurrency()
    {
        // Arrange - simulate high concurrency (multiple parallel requests)
        var tasks = new List<Task<HttpResponseMessage>>();
        
        // Act - send 10 concurrent requests
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(_client.GetAsync("/api/system/status"));
        }
        
        var responses = await Task.WhenAll(tasks);

        // Assert - all requests should succeed
        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        
        // Verify all responses have valid data
        foreach (var response in responses)
        {
            var statusResponse = await response.Content.ReadFromJsonAsync<SystemStatusResponse>(TestJsonOptions.GetOptions());
            Assert.NotNull(statusResponse);
            Assert.NotNull(statusResponse.SystemState);
            Assert.NotNull(statusResponse.EnvironmentMode);
        }
    }
}
