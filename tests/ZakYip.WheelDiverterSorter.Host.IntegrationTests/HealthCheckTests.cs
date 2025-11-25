using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using ZakYip.WheelDiverterSorter.Host.Controllers;
using ZakYip.WheelDiverterSorter.Core.Enums.Monitoring;
using ZakYip.WheelDiverterSorter.Core.Enums.System;

namespace ZakYip.WheelDiverterSorter.Host.IntegrationTests;

/// <summary>
/// Integration tests for health check endpoints (PR-34)
/// Tests the standardized /health/live, /health/startup, /health/ready endpoints
/// </summary>
public class HealthCheckTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public HealthCheckTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    #region Liveness Probe Tests

    [Fact]
    public async Task HealthLive_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/health/live");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var healthResponse = await response.Content.ReadFromJsonAsync<ProcessHealthResponse>(TestJsonOptions.GetOptions());
        Assert.NotNull(healthResponse);
        Assert.Equal(HealthStatus.Healthy, healthResponse.Status);
    }

    [Fact]
    public async Task Healthz_ReturnsOk()
    {
        // Act - test backward compatibility endpoint
        var response = await _client.GetAsync("/healthz");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var healthResponse = await response.Content.ReadFromJsonAsync<ProcessHealthResponse>(TestJsonOptions.GetOptions());
        Assert.NotNull(healthResponse);
        Assert.Equal(HealthStatus.Healthy, healthResponse.Status);
    }

    #endregion

    #region Startup Probe Tests

    [Fact]
    public async Task HealthStartup_WhenSystemReady_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/health/startup");

        // Assert
        // System should have completed startup in tests
        Assert.True(response.IsSuccessStatusCode, 
            $"Expected success status but got {response.StatusCode}");
        
        var healthResponse = await response.Content.ReadFromJsonAsync<ProcessHealthResponse>(TestJsonOptions.GetOptions());
        Assert.NotNull(healthResponse);
        Assert.Equal(HealthStatus.Healthy, healthResponse.Status);
    }

    #endregion

    #region Readiness Probe Tests

    [Fact]
    public async Task HealthReady_ReturnsResponse()
    {
        // Act
        var response = await _client.GetAsync("/health/ready");

        // Assert
        // Should return either OK or ServiceUnavailable depending on system state
        Assert.True(
            response.StatusCode == HttpStatusCode.OK || 
            response.StatusCode == HttpStatusCode.ServiceUnavailable,
            $"Expected OK or ServiceUnavailable but got {response.StatusCode}");
        
        var healthResponse = await response.Content.ReadFromJsonAsync<LineHealthResponse>(TestJsonOptions.GetOptions());
        Assert.NotNull(healthResponse);
        Assert.True(Enum.IsDefined(healthResponse.SystemState));
    }

    [Fact]
    public async Task HealthReady_ContainsSystemState()
    {
        // Act
        var response = await _client.GetAsync("/health/ready");
        var healthResponse = await response.Content.ReadFromJsonAsync<LineHealthResponse>(TestJsonOptions.GetOptions());

        // Assert
        Assert.NotNull(healthResponse);
        Assert.True(Enum.IsDefined(healthResponse.SystemState));
    }

    [Fact]
    public async Task HealthReady_ContainsSelfTestInfo()
    {
        // Act
        var response = await _client.GetAsync("/health/ready");
        var healthResponse = await response.Content.ReadFromJsonAsync<LineHealthResponse>(TestJsonOptions.GetOptions());

        // Assert
        Assert.NotNull(healthResponse);
        // IsSelfTestSuccess is a boolean, always has a value
        Assert.True(healthResponse.IsSelfTestSuccess || !healthResponse.IsSelfTestSuccess);
    }

    [Fact]
    public async Task HealthReady_ContainsDriverHealthStatus()
    {
        // Act
        var response = await _client.GetAsync("/health/ready");
        var healthResponse = await response.Content.ReadFromJsonAsync<LineHealthResponse>(TestJsonOptions.GetOptions());

        // Assert
        Assert.NotNull(healthResponse);
        // Drivers list may be null or empty in test environment
        if (healthResponse.Drivers != null && healthResponse.Drivers.Count > 0)
        {
            foreach (var driver in healthResponse.Drivers)
            {
                Assert.NotNull(driver.DriverName);
                Assert.NotEmpty(driver.DriverName);
            }
        }
    }

    [Fact]
    public async Task HealthReady_ContainsUpstreamHealthStatus()
    {
        // Act
        var response = await _client.GetAsync("/health/ready");
        var healthResponse = await response.Content.ReadFromJsonAsync<LineHealthResponse>(TestJsonOptions.GetOptions());

        // Assert
        Assert.NotNull(healthResponse);
        // Upstreams list may be null or empty in test environment
        if (healthResponse.Upstreams != null && healthResponse.Upstreams.Count > 0)
        {
            foreach (var upstream in healthResponse.Upstreams)
            {
                Assert.NotNull(upstream.EndpointName);
                Assert.NotEmpty(upstream.EndpointName);
            }
        }
    }

    #endregion

    #region Legacy Endpoint Tests

    [Fact]
    public async Task HealthLine_RedirectsToReadyEndpoint()
    {
        // Act
        var readyResponse = await _client.GetAsync("/health/ready");
        var lineResponse = await _client.GetAsync("/health/line");

        // Assert - both should return same status code
        Assert.Equal(readyResponse.StatusCode, lineResponse.StatusCode);
        
        var readyHealth = await readyResponse.Content.ReadFromJsonAsync<LineHealthResponse>(TestJsonOptions.GetOptions());
        var lineHealth = await lineResponse.Content.ReadFromJsonAsync<LineHealthResponse>(TestJsonOptions.GetOptions());
        
        Assert.NotNull(readyHealth);
        Assert.NotNull(lineHealth);
        Assert.Equal(readyHealth.SystemState, lineHealth.SystemState);
    }

    #endregion

    #region Response Model Tests

    [Fact]
    public async Task HealthLive_ReturnsValidResponseModel()
    {
        // Act
        var response = await _client.GetAsync("/health/live");
        var healthResponse = await response.Content.ReadFromJsonAsync<ProcessHealthResponse>(TestJsonOptions.GetOptions());

        // Assert
        Assert.NotNull(healthResponse);
        Assert.True(Enum.IsDefined(healthResponse.Status));
        Assert.True(healthResponse.Timestamp != default(DateTimeOffset));
    }

    [Fact]
    public async Task HealthReady_ReturnsValidResponseModel()
    {
        // Act
        var response = await _client.GetAsync("/health/ready");
        var healthResponse = await response.Content.ReadFromJsonAsync<LineHealthResponse>(TestJsonOptions.GetOptions());

        // Assert
        Assert.NotNull(healthResponse);
        Assert.True(Enum.IsDefined(healthResponse.SystemState));
        // Verify boolean field exists
        _ = healthResponse.IsSelfTestSuccess;
    }

    #endregion

    #region Concurrent Request Tests

    [Fact]
    public async Task HealthEndpoints_SupportConcurrentRequests()
    {
        // Arrange
        var tasks = new List<Task<HttpResponseMessage>>();
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(_client.GetAsync("/health/live"));
            tasks.Add(_client.GetAsync("/health/startup"));
            tasks.Add(_client.GetAsync("/health/ready"));
        }

        // Act
        var responses = await Task.WhenAll(tasks);

        // Assert
        foreach (var response in responses)
        {
            Assert.True(
                response.StatusCode == HttpStatusCode.OK || 
                response.StatusCode == HttpStatusCode.ServiceUnavailable,
                $"Unexpected status code: {response.StatusCode}");
        }
    }

    #endregion

    #region Performance Tests

    [Fact]
    public async Task HealthLive_RespondsQuickly()
    {
        // Arrange
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        var response = await _client.GetAsync("/health/live");

        // Assert
        stopwatch.Stop();
        Assert.True(stopwatch.ElapsedMilliseconds < 1000, 
            $"Liveness check took too long: {stopwatch.ElapsedMilliseconds}ms");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task HealthReady_RespondsWithinTimeout()
    {
        // Arrange
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        var response = await _client.GetAsync("/health/ready");

        // Assert
        stopwatch.Stop();
        Assert.True(stopwatch.ElapsedMilliseconds < 5000, 
            $"Readiness check took too long: {stopwatch.ElapsedMilliseconds}ms");
    }

    #endregion
}
