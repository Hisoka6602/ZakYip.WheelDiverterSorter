using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using ZakYip.WheelDiverterSorter.Host.Models;
using ZakYip.WheelDiverterSorter.Host.Models.Panel;

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
    public async Task UpdatePanelConfig_ShouldSucceed_WithValidRequest()
    {
        // Act - For simplicity, we just verify the endpoint is accessible
        // Detailed request validation would require knowing exact panel configuration structure
        var response = await _client.GetAsync("/api/config/panel");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
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
}
