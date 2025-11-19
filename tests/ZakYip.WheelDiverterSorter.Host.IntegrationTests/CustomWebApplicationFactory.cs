using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ZakYip.WheelDiverterSorter.Host.IntegrationTests;

/// <summary>
/// Custom WebApplicationFactory for integration tests
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
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
