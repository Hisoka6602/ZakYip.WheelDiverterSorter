using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ZakYip.WheelDiverterSorter.Host.IntegrationTests;

/// <summary>
/// Custom WebApplicationFactory for integration tests
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    /// <summary>
    /// Gets the JsonSerializerOptions configured for the application (with enum string conversion)
    /// </summary>
    public static JsonSerializerOptions JsonSerializerOptions { get; } = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: true) }
    };

    protected override IHost CreateHost(IHostBuilder builder)
    {
        // Configure test-specific services
        builder.ConfigureServices(services =>
        {
            // Add test-specific service overrides here if needed
        });

        return base.CreateHost(builder);
    }
}
