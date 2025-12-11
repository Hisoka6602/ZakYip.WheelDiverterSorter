using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace ZakYip.WheelDiverterSorter.Host.IntegrationTests;

/// <summary>
/// 格口分配超时控制器集成测试
/// </summary>
public class ChuteAssignmentTimeoutControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public ChuteAssignmentTimeoutControllerTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetChuteAssignmentTimeout_ShouldReturnSuccess()
    {
        // Act
        var response = await _client.GetAsync("/api/config/chute-assignment-timeout");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var content = await response.Content.ReadAsStringAsync();
        Assert.NotNull(content);
        Assert.NotEmpty(content);
    }
}
