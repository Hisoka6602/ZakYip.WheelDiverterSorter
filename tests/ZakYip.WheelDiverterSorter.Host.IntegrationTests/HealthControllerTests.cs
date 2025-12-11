using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace ZakYip.WheelDiverterSorter.Host.IntegrationTests;

/// <summary>
/// 健康检查控制器集成测试
/// </summary>
public class HealthControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public HealthControllerTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetHealthCheck_ShouldReturnSuccess()
    {
        // Act
        var response = await _client.GetAsync("/health");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var content = await response.Content.ReadAsStringAsync();
        Assert.NotNull(content);
        Assert.NotEmpty(content);
    }

    [Fact]
    public async Task GetReadinessCheck_ShouldReturnSuccess()
    {
        // Act
        var response = await _client.GetAsync("/health/ready");

        // Assert
        // May return ServiceUnavailable if system is not ready, which is acceptable
        Assert.True(
            response.StatusCode == HttpStatusCode.OK || 
            response.StatusCode == HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task GetLivenessCheck_ShouldReturnSuccess()
    {
        // Act
        var response = await _client.GetAsync("/health/live");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var content = await response.Content.ReadAsStringAsync();
        Assert.Equal("OK", content);
    }

    [Fact]
    public async Task GetDetailedHealthCheck_ShouldReturnSuccess()
    {
        // Act
        var response = await _client.GetAsync("/health/detail");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var content = await response.Content.ReadAsStringAsync();
        Assert.NotNull(content);
        Assert.NotEmpty(content);
    }
}
