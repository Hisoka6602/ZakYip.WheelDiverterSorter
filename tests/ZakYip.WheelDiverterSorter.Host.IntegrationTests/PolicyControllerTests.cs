using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace ZakYip.WheelDiverterSorter.Host.IntegrationTests;

/// <summary>
/// 策略控制器集成测试
/// </summary>
public class PolicyControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public PolicyControllerTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetCurrentPolicy_ShouldReturnSuccess()
    {
        // Act
        var response = await _client.GetAsync("/api/policy");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var content = await response.Content.ReadAsStringAsync();
        Assert.NotNull(content);
        Assert.NotEmpty(content);
    }

    [Fact]
    public async Task GetDefaultPolicy_ShouldReturnSuccess()
    {
        // Act
        var response = await _client.GetAsync("/api/policy/default");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var content = await response.Content.ReadAsStringAsync();
        Assert.NotNull(content);
        Assert.NotEmpty(content);
    }
}
