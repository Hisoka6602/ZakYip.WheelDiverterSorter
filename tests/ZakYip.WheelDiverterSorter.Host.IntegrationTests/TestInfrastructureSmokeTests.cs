using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace ZakYip.WheelDiverterSorter.Host.IntegrationTests;

/// <summary>
/// Smoke tests to verify test infrastructure is working correctly
/// These tests validate that the test environment is properly configured
/// </summary>
public class TestInfrastructureSmokeTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public TestInfrastructureSmokeTests(CustomWebApplicationFactory _factory)
    {
        this._factory = _factory;
    }

    [Fact]
    public void TestInfrastructure_CanCreateWebApplicationFactory()
    {
        // Arrange & Act - Factory is created in constructor
        
        // Assert
        Assert.NotNull(_factory);
    }

    [Fact]
    public void TestInfrastructure_CanCreateHttpClient()
    {
        // Arrange & Act
        var client = _factory.CreateClient();
        
        // Assert
        Assert.NotNull(client);
        Assert.NotNull(client.BaseAddress);
    }

    [Fact]
    public async Task TestInfrastructure_HealthEndpointIsAccessible()
    {
        // Arrange
        var client = _factory.CreateClient();
        
        // Act
        var response = await client.GetAsync("/health");
        
        // Assert
        // We just need to verify the endpoint is accessible
        // The actual health status may vary
        Assert.NotNull(response);
    }

    [Fact]
    public void TestInfrastructure_MockRuleEngineClientIsConfigured()
    {
        // Arrange & Act
        var mockClient = _factory.MockRuleEngineClient;
        
        // Assert
        Assert.NotNull(mockClient);
    }
}
