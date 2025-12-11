using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace ZakYip.WheelDiverterSorter.Host.IntegrationTests;

/// <summary>
/// 仿真控制器集成测试
/// </summary>
public class SimulationControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public SimulationControllerTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetSimulationStatus_ShouldReturnSuccess()
    {
        // Act
        var response = await _client.GetAsync("/api/simulation/status");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var content = await response.Content.ReadAsStringAsync();
        Assert.NotNull(content);
    }

    [Fact]
    public async Task GetScenarios_ShouldReturnSuccess()
    {
        // Act
        var response = await _client.GetAsync("/api/simulation/scenarios");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var content = await response.Content.ReadAsStringAsync();
        Assert.NotNull(content);
    }
}
