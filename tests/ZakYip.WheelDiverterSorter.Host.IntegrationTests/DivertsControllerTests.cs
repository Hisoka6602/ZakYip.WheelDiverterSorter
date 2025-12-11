using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace ZakYip.WheelDiverterSorter.Host.IntegrationTests;

/// <summary>
/// 分拣控制器集成测试
/// </summary>
public class DivertsControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public DivertsControllerTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetCurrentDiverts_ShouldReturnSuccess()
    {
        // Act
        var response = await _client.GetAsync("/api/diverts");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var content = await response.Content.ReadAsStringAsync();
        Assert.NotNull(content);
    }

    [Fact]
    public async Task GetActiveParcels_ShouldReturnSuccess()
    {
        // Act
        var response = await _client.GetAsync("/api/diverts/active");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var content = await response.Content.ReadAsStringAsync();
        Assert.NotNull(content);
    }
}
