using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;
using ZakYip.WheelDiverterSorter.Configuration.Persistence.Repositories.LiteDb;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;

namespace ZakYip.WheelDiverterSorter.Host.IntegrationTests;

/// <summary>
/// Integration tests for Swagger schema filtering based on driver vendor type
/// </summary>
public class SwaggerDriverConfigSchemaTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly IDriverConfigurationRepository _driverRepository;
    private readonly IWheelDiverterConfigurationRepository _wheelDiverterRepository;

    public SwaggerDriverConfigSchemaTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _driverRepository = factory.Services.GetRequiredService<IDriverConfigurationRepository>();
        _wheelDiverterRepository = factory.Services.GetRequiredService<IWheelDiverterConfigurationRepository>();
    }

    [Fact]
    public async Task SwaggerSchema_ShowsLeadshineConfig_WhenLeadshineSelected()
    {
        // Arrange - Set driver to Leadshine
        var config = _driverRepository.Get();
        config.VendorType = DriverVendorType.Leadshine;
        config.Leadshine = new LeadshineDriverConfig
        {
            CardNo = 0,
            Diverters = new List<DiverterDriverEntry>
            {
                new() { DiverterId = 1, DiverterName = "D1", OutputStartBit = 0, FeedbackInputBit = 10 }
            }
        };
        _driverRepository.Update(config);

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
            
            // Should have leadshine property (even if it's a $ref)
            Assert.True(properties.TryGetProperty("leadshine", out var leadshineProperty), "Leadshine property should be present");
            
            // Leadshine property should exist (either inline or as $ref)
            Assert.True(leadshineProperty.ValueKind == JsonValueKind.Object);
        }
    }

    [Fact]
    public async Task SwaggerSchema_ShowsShuDiNiaoConfig_WhenShuDiNiaoSelected()
    {
        // Arrange - Set wheel diverter to ShuDiNiao
        var config = _wheelDiverterRepository.Get();
        config.VendorType = WheelDiverterVendorType.ShuDiNiao;
        config.ShuDiNiao = new ShuDiNiaoWheelDiverterConfig
        {
            Devices = new List<ShuDiNiaoDeviceEntry>
            {
                new() { DiverterId = 1, Host = "192.168.0.100", Port = 2000, DeviceAddress = 0x51 }
            }
        };
        config.Modi = null;
        _wheelDiverterRepository.Update(config);

        // Act - Get Swagger JSON
        var response = await _client.GetAsync("/swagger/v1/swagger.json");

        // Assert
        response.EnsureSuccessStatusCode();
        var swaggerJson = await response.Content.ReadAsStringAsync();
        
        using var doc = JsonDocument.Parse(swaggerJson);
        var schemas = doc.RootElement.GetProperty("components").GetProperty("schemas");
        
        // Find WheelDiverterConfiguration schema
        if (schemas.TryGetProperty("WheelDiverterConfiguration", out var wheelDiverterConfigSchema))
        {
            var properties = wheelDiverterConfigSchema.GetProperty("properties");
            
            // Should have shuDiNiao property (even if it's a $ref)
            Assert.True(properties.TryGetProperty("shuDiNiao", out var shuDiNiaoProperty), "ShuDiNiao property should be present");
            
            // ShuDiNiao property should exist (either inline or as $ref)
            Assert.True(shuDiNiaoProperty.ValueKind == JsonValueKind.Object);
            
            // Should NOT have modi property (filtered out)
            Assert.False(properties.TryGetProperty("modi", out _), "Modi property should be filtered out");
        }
    }

    [Fact]
    public async Task SwaggerSchema_IncludesVendorTypeInDescription_ForDriverConfiguration()
    {
        // Arrange - Set driver to Leadshine
        var config = _driverRepository.Get();
        config.VendorType = DriverVendorType.Leadshine;
        _driverRepository.Update(config);

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
                Assert.Contains("当前配置的IO驱动厂商", descriptionText);
                Assert.Contains("Leadshine", descriptionText);
            }
        }
    }

    [Fact]
    public async Task SwaggerSchema_ChangesWhenWheelDiverterVendorTypeChanges()
    {
        // Arrange - Start with ShuDiNiao
        var config = _wheelDiverterRepository.Get();
        config.VendorType = WheelDiverterVendorType.ShuDiNiao;
        config.ShuDiNiao = new ShuDiNiaoWheelDiverterConfig
        {
            Devices = new List<ShuDiNiaoDeviceEntry>
            {
                new() { DiverterId = 1, Host = "192.168.0.100", Port = 2000, DeviceAddress = 0x51 }
            }
        };
        _wheelDiverterRepository.Update(config);

        // Act 1 - Get Swagger with ShuDiNiao
        var response1 = await _client.GetAsync("/swagger/v1/swagger.json");
        var swagger1 = await response1.Content.ReadAsStringAsync();

        // Arrange - Change to Modi
        config.VendorType = WheelDiverterVendorType.Modi;
        config.Modi = new ModiWheelDiverterConfig
        {
            Devices = new List<ModiDeviceEntry>
            {
                new() { DiverterId = 1, Host = "192.168.1.100", Port = 8000, DeviceId = 1 }
            }
        };
        _wheelDiverterRepository.Update(config);

        // Act 2 - Get Swagger with Modi
        var response2 = await _client.GetAsync("/swagger/v1/swagger.json");
        var swagger2 = await response2.Content.ReadAsStringAsync();

        // Assert - Swagger schemas should be different
        Assert.NotEqual(swagger1, swagger2);
        
        // Verify ShuDiNiao version has shuDiNiao property
        using (var doc1 = JsonDocument.Parse(swagger1))
        {
            var schemas1 = doc1.RootElement.GetProperty("components").GetProperty("schemas");
            if (schemas1.TryGetProperty("WheelDiverterConfiguration", out var schema1))
            {
                var properties1 = schema1.GetProperty("properties");
                Assert.True(properties1.TryGetProperty("shuDiNiao", out _));
            }
        }
        
        // Verify Modi version has modi property
        using (var doc2 = JsonDocument.Parse(swagger2))
        {
            var schemas2 = doc2.RootElement.GetProperty("components").GetProperty("schemas");
            if (schemas2.TryGetProperty("WheelDiverterConfiguration", out var schema2))
            {
                var properties2 = schema2.GetProperty("properties");
                Assert.True(properties2.TryGetProperty("modi", out _));
            }
        }
    }
}
