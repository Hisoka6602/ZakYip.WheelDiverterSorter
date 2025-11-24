using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;

namespace ZakYip.WheelDiverterSorter.Host.IntegrationTests;

/// <summary>
/// Integration tests for Swagger schema filtering based on driver vendor type
/// </summary>
public class SwaggerDriverConfigSchemaTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly IDriverConfigurationRepository _repository;

    public SwaggerDriverConfigSchemaTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _repository = factory.Services.GetRequiredService<IDriverConfigurationRepository>();
    }

    [Fact]
    public async Task SwaggerSchema_ShowsOnlyLeadshineConfig_WhenLeadshineSelected()
    {
        // Arrange - Set driver to Leadshine
        var config = _repository.Get();
        config.VendorType = DriverVendorType.Leadshine;
        config.Leadshine = new LeadshineDriverConfig
        {
            CardNo = 0,
            Diverters = new List<DiverterDriverEntry>
            {
                new() { DiverterId = 1, DiverterName = "D1", OutputStartBit = 0, FeedbackInputBit = 10 }
            }
        };
        config.ShuDiNiao = null;
        _repository.Update(config);

        // Act - Get Swagger JSON
        var response = await _client.GetAsync("/swagger/v1/swagger.json");

        // Assert
        response.EnsureSuccessStatusCode();
        var swaggerJson = await response.Content.ReadAsStringAsync();
        
        using var doc = JsonDocument.Parse(swaggerJson);
        var schemas = doc.RootElement.GetProperty("components").GetProperty("schemas");
        
        // Find DriverConfiguration schema
        if (schemas.TryGetProperty("DriverConfiguration", out var driverConfigSchema))
        {
            var properties = driverConfigSchema.GetProperty("properties");
            
            // Should have leadshine property
            Assert.True(properties.TryGetProperty("leadshine", out _), "Leadshine property should be present");
            
            // Should NOT have shuDiNiao property (filtered out)
            Assert.False(properties.TryGetProperty("shuDiNiao", out _), "ShuDiNiao property should be filtered out");
        }
    }

    [Fact]
    public async Task SwaggerSchema_ShowsOnlyShuDiNiaoConfig_WhenShuDiNiaoSelected()
    {
        // Arrange - Set driver to ShuDiNiao
        var config = _repository.Get();
        config.VendorType = DriverVendorType.ShuDiNiao;
        config.ShuDiNiao = new ShuDiNiaoDriverConfig
        {
            Devices = new List<ShuDiNiaoDeviceEntry>
            {
                new() { DiverterId = "D1", Host = "192.168.0.100", Port = 2000, DeviceAddress = 0x51 }
            }
        };
        config.Leadshine = null;
        _repository.Update(config);

        // Act - Get Swagger JSON
        var response = await _client.GetAsync("/swagger/v1/swagger.json");

        // Assert
        response.EnsureSuccessStatusCode();
        var swaggerJson = await response.Content.ReadAsStringAsync();
        
        using var doc = JsonDocument.Parse(swaggerJson);
        var schemas = doc.RootElement.GetProperty("components").GetProperty("schemas");
        
        // Find DriverConfiguration schema
        if (schemas.TryGetProperty("DriverConfiguration", out var driverConfigSchema))
        {
            var properties = driverConfigSchema.GetProperty("properties");
            
            // Should have shuDiNiao property
            Assert.True(properties.TryGetProperty("shuDiNiao", out _), "ShuDiNiao property should be present");
            
            // Should NOT have leadshine property (filtered out)
            Assert.False(properties.TryGetProperty("leadshine", out _), "Leadshine property should be filtered out");
        }
    }

    [Fact]
    public async Task SwaggerSchema_IncludesVendorTypeInDescription()
    {
        // Arrange - Set driver to Leadshine
        var config = _repository.Get();
        config.VendorType = DriverVendorType.Leadshine;
        _repository.Update(config);

        // Act - Get Swagger JSON
        var response = await _client.GetAsync("/swagger/v1/swagger.json");

        // Assert
        response.EnsureSuccessStatusCode();
        var swaggerJson = await response.Content.ReadAsStringAsync();
        
        using var doc = JsonDocument.Parse(swaggerJson);
        var schemas = doc.RootElement.GetProperty("components").GetProperty("schemas");
        
        // Find DriverConfiguration schema
        if (schemas.TryGetProperty("DriverConfiguration", out var driverConfigSchema))
        {
            // Check if description includes vendor type information
            if (driverConfigSchema.TryGetProperty("description", out var description))
            {
                var descriptionText = description.GetString() ?? "";
                Assert.Contains("当前配置的驱动厂商", descriptionText);
                Assert.Contains("Leadshine", descriptionText);
            }
        }
    }

    [Fact]
    public async Task SwaggerSchema_ChangesWhenVendorTypeChanges()
    {
        // Arrange - Start with Leadshine
        var config = _repository.Get();
        config.VendorType = DriverVendorType.Leadshine;
        config.Leadshine = new LeadshineDriverConfig
        {
            CardNo = 0,
            Diverters = new List<DiverterDriverEntry>
            {
                new() { DiverterId = 1, DiverterName = "D1", OutputStartBit = 0, FeedbackInputBit = 10 }
            }
        };
        _repository.Update(config);

        // Act 1 - Get Swagger with Leadshine
        var response1 = await _client.GetAsync("/swagger/v1/swagger.json");
        var swagger1 = await response1.Content.ReadAsStringAsync();

        // Arrange - Change to ShuDiNiao
        config.VendorType = DriverVendorType.ShuDiNiao;
        config.ShuDiNiao = new ShuDiNiaoDriverConfig
        {
            Devices = new List<ShuDiNiaoDeviceEntry>
            {
                new() { DiverterId = "D1", Host = "192.168.0.100", Port = 2000, DeviceAddress = 0x51 }
            }
        };
        _repository.Update(config);

        // Act 2 - Get Swagger with ShuDiNiao
        var response2 = await _client.GetAsync("/swagger/v1/swagger.json");
        var swagger2 = await response2.Content.ReadAsStringAsync();

        // Assert - Swagger schemas should be different
        Assert.NotEqual(swagger1, swagger2);
        
        // Verify Leadshine version has leadshine property
        using (var doc1 = JsonDocument.Parse(swagger1))
        {
            var schemas1 = doc1.RootElement.GetProperty("components").GetProperty("schemas");
            if (schemas1.TryGetProperty("DriverConfiguration", out var schema1))
            {
                var properties1 = schema1.GetProperty("properties");
                Assert.True(properties1.TryGetProperty("leadshine", out _));
            }
        }
        
        // Verify ShuDiNiao version has shuDiNiao property
        using (var doc2 = JsonDocument.Parse(swagger2))
        {
            var schemas2 = doc2.RootElement.GetProperty("components").GetProperty("schemas");
            if (schemas2.TryGetProperty("DriverConfiguration", out var schema2))
            {
                var properties2 = schema2.GetProperty("properties");
                Assert.True(properties2.TryGetProperty("shuDiNiao", out _));
            }
        }
    }
}
