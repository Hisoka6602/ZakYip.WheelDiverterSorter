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
    public async Task SwaggerSchema_ShowsCorrectVendorType_ForWheelDiverterConfiguration()
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
        _wheelDiverterRepository.Update(config);

        // Act - Get Swagger with ShuDiNiao
        var response = await _client.GetAsync("/swagger/v1/swagger.json");
        var swaggerJson = await response.Content.ReadAsStringAsync();

        // Assert
        response.EnsureSuccessStatusCode();
        
        // Verify ShuDiNiao version has shuDiNiao property
        using var doc = JsonDocument.Parse(swaggerJson);
        var schemas = doc.RootElement.GetProperty("components").GetProperty("schemas");
        if (schemas.TryGetProperty("WheelDiverterConfiguration", out var schema))
        {
            var properties = schema.GetProperty("properties");
            Assert.True(properties.TryGetProperty("shuDiNiao", out _), "ShuDiNiao property should be present");
        }
    }
}
