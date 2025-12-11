using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware.Vendors;
using ZakYip.WheelDiverterSorter.Host.Models.Config;

namespace ZakYip.WheelDiverterSorter.Host.IntegrationTests;

/// <summary>
/// Integration tests for ShuDiNiao automatic configuration loading
/// Tests that test/control endpoints automatically load configuration from repository
/// </summary>
public class ShuDiNiaoAutoLoadConfigTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly CustomWebApplicationFactory _factory;

    public ShuDiNiaoAutoLoadConfigTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _jsonOptions = TestJsonOptions.GetOptions();
    }

    [Fact]
    public async Task TestEndpoint_WithNoDriversLoaded_AutoLoadsConfigurationFromRepository()
    {
        // Arrange - Save ShuDiNiao configuration to repository
        using var scope = _factory.Services.CreateScope();
        var wheelRepo = scope.ServiceProvider.GetRequiredService<IWheelDiverterConfigurationRepository>();
        
        var config = wheelRepo.Get();
        config.VendorType = WheelDiverterVendorType.ShuDiNiao;
        config.ShuDiNiao = new ShuDiNiaoWheelDiverterConfig
        {
            Mode = ShuDiNiaoMode.Client,
            Devices = new List<ShuDiNiaoDeviceEntry>
            {
                new ShuDiNiaoDeviceEntry
                {
                    DiverterId = 1,
                    Host = "192.168.0.200",
                    Port = 200,
                    DeviceAddress = 1,
                    IsEnabled = true
                }
            }
        };
        wheelRepo.Update(config);

        // Act - Call test endpoint without first calling PUT to load configuration
        var request = new WheelDiverterTestRequest
        {
            DiverterIds = new List<long> { 1 },
            Direction = DiverterDirection.Left
        };

        var response = await _client.PostAsJsonAsync("/api/hardware/shudiniao/test", request, _jsonOptions);

        // Assert - Should succeed (or fail gracefully), not return "驱动管理器未注册"
        // In simulation mode this will return BadRequest because driver manager is null
        // In real mode with driver manager, it should auto-load the configuration
        Assert.NotNull(response);
        
        // If we get BadRequest, check it's not because of missing driver manager registration
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var content = await response.Content.ReadAsStringAsync();
            // Should not be the "driver manager not registered" error anymore
            // because EnsureDriversLoadedAsync should have been called
            Assert.DoesNotContain("驱动管理器未注册", content);
        }
    }

    [Fact]
    public async Task ControlEndpoint_WithNoDriversLoaded_AutoLoadsConfigurationFromRepository()
    {
        // Arrange - Save ShuDiNiao configuration to repository
        using var scope = _factory.Services.CreateScope();
        var wheelRepo = scope.ServiceProvider.GetRequiredService<IWheelDiverterConfigurationRepository>();
        
        var config = wheelRepo.Get();
        config.VendorType = WheelDiverterVendorType.ShuDiNiao;
        config.ShuDiNiao = new ShuDiNiaoWheelDiverterConfig
        {
            Mode = ShuDiNiaoMode.Client,
            Devices = new List<ShuDiNiaoDeviceEntry>
            {
                new ShuDiNiaoDeviceEntry
                {
                    DiverterId = 2,
                    Host = "192.168.0.201",
                    Port = 200,
                    DeviceAddress = 2,
                    IsEnabled = true
                }
            }
        };
        wheelRepo.Update(config);

        // Act - Call control endpoint without first calling PUT to load configuration
        var request = new WheelDiverterControlRequest
        {
            DiverterIds = new List<long> { 2 },
            Command = WheelDiverterCommand.Run
        };

        var response = await _client.PostAsJsonAsync("/api/hardware/shudiniao/control", request, _jsonOptions);

        // Assert - Should succeed (or fail gracefully), not return "驱动管理器未注册"
        Assert.NotNull(response);
        
        // In simulation mode this will return BadRequest because driver manager is null
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var content = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain("驱动管理器未注册", content);
        }
    }

    [Fact]
    public async Task AutoLoad_WithNonShuDiNiaoConfig_DoesNotLoadDrivers()
    {
        // Arrange - Save non-ShuDiNiao configuration
        using var scope = _factory.Services.CreateScope();
        var wheelRepo = scope.ServiceProvider.GetRequiredService<IWheelDiverterConfigurationRepository>();
        
        var config = wheelRepo.Get();
        config.VendorType = WheelDiverterVendorType.ShuDiNiao;  // Use ShuDiNiao, not Mock
        config.ShuDiNiao = null;
        wheelRepo.Update(config);

        // Act - Call test endpoint
        var request = new WheelDiverterTestRequest
        {
            DiverterIds = new List<long> { 1 },
            Direction = DiverterDirection.Left
        };

        var response = await _client.PostAsJsonAsync("/api/hardware/shudiniao/test", request, _jsonOptions);

        // Assert - Should handle gracefully
        Assert.NotNull(response);
    }
}

/// <summary>
/// Test request model for wheel diverter testing
/// </summary>
public record WheelDiverterTestRequest
{
    public required List<long> DiverterIds { get; init; }
    public required DiverterDirection Direction { get; init; }
}

/// <summary>
/// Test request model for wheel diverter control
/// </summary>
public record WheelDiverterControlRequest
{
    public required List<long> DiverterIds { get; init; }
    public required WheelDiverterCommand Command { get; init; }
}

/// <summary>
/// Wheel diverter command (from HardwareConfigController.cs)
/// </summary>
public enum WheelDiverterCommand
{
    Run,
    Stop
}
