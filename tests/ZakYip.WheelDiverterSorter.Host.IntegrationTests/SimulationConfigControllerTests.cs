using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using ZakYip.WheelDiverterSorter.Host.Models;
using ZakYip.WheelDiverterSorter.Host.Models.Config;

namespace ZakYip.WheelDiverterSorter.Host.IntegrationTests;

/// <summary>
/// 仿真配置控制器集成测试
/// </summary>
public class SimulationConfigControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public SimulationConfigControllerTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetSimulationConfig_ShouldReturnSuccess()
    {
        // Act
        var response = await _client.GetAsync("/api/config/simulation");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<SimulationConfigResponse>>();
        Assert.NotNull(result);
        Assert.True(result.Success);
    }

    [Fact]
    public async Task GetSimulationTemplate_ShouldReturnSuccess()
    {
        // Act
        var response = await _client.GetAsync("/api/config/simulation/template");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<SimulationConfigResponse>>();
        Assert.NotNull(result);
        Assert.True(result.Success);
    }
}
