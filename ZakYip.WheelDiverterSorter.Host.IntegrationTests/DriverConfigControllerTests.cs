using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;
using ZakYip.WheelDiverterSorter.Core.LineModel.Enums;

namespace ZakYip.WheelDiverterSorter.Host.IntegrationTests;

/// <summary>
/// Integration tests for DriverConfigController
/// </summary>
public class DriverConfigControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public DriverConfigControllerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetDriverConfig_ReturnsSuccess()
    {
        // Act
        var response = await _client.GetAsync("/api/config/driver");

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetDriverConfig_ReturnsJsonContent()
    {
        // Act
        var response = await _client.GetAsync("/api/config/driver");

        // Assert
        Assert.Contains("application/json", response.Content.Headers.ContentType?.ToString() ?? "");
    }

    [Fact]
    public async Task GetDriverConfig_EnumsSerializedAsStrings()
    {
        // Act
        var response = await _client.GetAsync("/api/config/driver");
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
    public async Task UpdateDriverConfig_AcceptsStringEnumValue()
    {
        // Arrange - JSON with string enum value
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
        var response = await _client.PutAsync("/api/config/driver", content);

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        // Verify response contains the enum as string
        var responseJson = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"vendorType\":", responseJson);
        Assert.Contains("\"Mock\"", responseJson);
    }

    [Fact]
    public async Task UpdateDriverConfig_AcceptsNumericEnumValue_ForBackwardCompatibility()
    {
        // Arrange - JSON with numeric enum value
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
        var response = await _client.PutAsync("/api/config/driver", content);

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        // Verify response contains the enum as string (even though we sent number)
        var responseJson = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"vendorType\":", responseJson);
        Assert.Contains("\"Mock\"", responseJson); // 0 = Mock
    }
}
