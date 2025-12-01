using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;
using ZakYip.WheelDiverterSorter.Configuration.Persistence.Repositories.LiteDb;
using ZakYip.WheelDiverterSorter.Core.Enums;

namespace ZakYip.WheelDiverterSorter.Host.IntegrationTests;

/// <summary>
/// Integration tests for LeadshineIoDriverConfigController (renamed from IoDriverConfigController)
/// </summary>
public class LeadshineIoDriverConfigControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public LeadshineIoDriverConfigControllerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetLeadshineIoDriverConfig_ReturnsSuccess()
    {
        // Act
        var response = await _client.GetAsync("/api/config/io-driver/leadshine");

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetLeadshineIoDriverConfig_ReturnsJsonContent()
    {
        // Act
        var response = await _client.GetAsync("/api/config/io-driver/leadshine");

        // Assert
        Assert.Contains("application/json", response.Content.Headers.ContentType?.ToString() ?? "");
    }

    [Fact]
    public async Task GetLeadshineIoDriverConfig_EnumsSerializedAsStrings()
    {
        // Act
        var response = await _client.GetAsync("/api/config/io-driver/leadshine");
        var json = await response.Content.ReadAsStringAsync();

        // Assert
        response.EnsureSuccessStatusCode();
        
        // Parse the JSON to verify enum is serialized as string
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        
        // VendorType should be a string (e.g., "Leadshine") not a number
        Assert.True(root.TryGetProperty("vendorType", out var vendorType));
        Assert.Equal(JsonValueKind.String, vendorType.ValueKind);
    }

    [Fact]
    public async Task UpdateLeadshineIoDriverConfig_AcceptsStringEnumValue()
    {
        // Arrange - JSON with string enum value
        // Note: While diverter mappings conceptually belong to wheeldiverter APIs,
        // the DriverConfiguration model still contains them for historical reasons
        var json = """
        {
            "useHardwareDriver": false,
            "vendorType": "Mock",
            "leadshine": {
                "cardNo": 0,
                "diverters": [
                    {
                        "diverterId": 1,
                        "diverterName": "D1",
                        "outputStartBit": 0,
                        "feedbackInputBit": 10
                    }
                ]
            }
        }
        """;

        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PutAsync("/api/config/io-driver/leadshine", content);

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        // Verify response contains the enum as string
        var responseJson = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"vendorType\":", responseJson);
        Assert.Contains("\"Mock\"", responseJson);
    }

    [Fact]
    public async Task UpdateLeadshineIoDriverConfig_AcceptsNumericEnumValue_ForBackwardCompatibility()
    {
        // Arrange - JSON with numeric enum value
        // Note: While diverter mappings conceptually belong to wheeldiverter APIs,
        // the DriverConfiguration model still contains them for historical reasons
        var json = """
        {
            "useHardwareDriver": false,
            "vendorType": 0,
            "leadshine": {
                "cardNo": 0,
                "diverters": [
                    {
                        "diverterId": 1,
                        "diverterName": "D1",
                        "outputStartBit": 0,
                        "feedbackInputBit": 10
                    }
                ]
            }
        }
        """;

        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PutAsync("/api/config/io-driver/leadshine", content);

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        // Verify response contains the enum as string (even though we sent number)
        var responseJson = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"vendorType\":", responseJson);
        Assert.Contains("\"Mock\"", responseJson); // 0 = Mock
    }

    [Fact]
    public async Task RestartLeadshineIoDriver_ReturnsSuccessOrBadRequest()
    {
        // Act - This tests the restart endpoint, which may return BadRequest if EMC controller is not available
        var response = await _client.PostAsync("/api/config/io-driver/leadshine/restart", null);

        // Assert - Should either succeed or return BadRequest (EMC not available in test environment)
        Assert.True(
            response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.BadRequest,
            $"Expected OK or BadRequest, but got {response.StatusCode}");
    }

    [Fact]
    public async Task ResetLeadshineIoDriverConfig_ReturnsSuccess()
    {
        // Act
        var response = await _client.PostAsync("/api/config/io-driver/leadshine/reset", null);

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetSensorIoConfig_ReturnsSuccess()
    {
        // Act
        var response = await _client.GetAsync("/api/config/io-driver/leadshine/sensors");

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
