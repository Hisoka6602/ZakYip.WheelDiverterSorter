using System.Text.Json;
using System.Text.Json.Serialization;

namespace ZakYip.WheelDiverterSorter.Host.IntegrationTests;

/// <summary>
/// Shared JSON serializer options for integration tests
/// </summary>
/// <remarks>
/// This ensures that test clients use the same JSON serialization settings as the API
/// </remarks>
public static class TestJsonOptions
{
    /// <summary>
    /// Get JSON serializer options configured to match the API's settings
    /// </summary>
    public static JsonSerializerOptions GetOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        
        // Configure enum serialization to match Program.cs settings
        options.Converters.Add(new JsonStringEnumConverter(
            namingPolicy: null, 
            allowIntegerValues: true));
        
        return options;
    }
}
