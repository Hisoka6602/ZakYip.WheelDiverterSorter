using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using ZakYip.WheelDiverterSorter.Host.Models;

namespace ZakYip.WheelDiverterSorter.Host.IntegrationTests;

/// <summary>
/// 硬件配置控制器集成测试
/// </summary>
public class HardwareConfigControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public HardwareConfigControllerTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetLeadshineConfig_ShouldReturnSuccess()
    {
        // Act
        var response = await _client.GetAsync("/api/hardware/leadshine");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var content = await response.Content.ReadAsStringAsync();
        Assert.NotNull(content);
        Assert.NotEmpty(content);
    }

    [Fact]
    public async Task GetShuDiNiaoConfig_ShouldReturnSuccess()
    {
        // Act
        var response = await _client.GetAsync("/api/hardware/shudiniao");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var content = await response.Content.ReadAsStringAsync();
        Assert.NotNull(content);
        Assert.NotEmpty(content);
    }

    [Fact]
    public async Task GetAllShuDiNiaoDevices_ShouldReturnSuccess()
    {
        // Act
        var response = await _client.GetAsync("/api/hardware/shudiniao/all");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
