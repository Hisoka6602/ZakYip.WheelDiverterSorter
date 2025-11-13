using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using ZakYip.WheelDiverterSorter.Core;
using ZakYip.WheelDiverterSorter.Core.Configuration;
using ZakYip.WheelDiverterSorter.Execution;

namespace ZakYip.WheelDiverterSorter.E2ETests;

/// <summary>
/// Base class for E2E tests with common setup and utilities
/// </summary>
public class E2ETestBase : IClassFixture<E2ETestFactory>, IDisposable
{
    protected readonly E2ETestFactory Factory;
    protected readonly HttpClient Client;
    protected readonly IServiceScope Scope;

    // Common services
    protected readonly ISwitchingPathGenerator PathGenerator;
    protected readonly ISwitchingPathExecutor PathExecutor;
    protected readonly IRouteConfigurationRepository RouteRepository;
    protected readonly ISystemConfigurationRepository SystemRepository;

    public E2ETestBase(E2ETestFactory factory)
    {
        Factory = factory;
        Client = factory.CreateClient();
        Scope = factory.Services.CreateScope();

        // Get common services
        PathGenerator = Scope.ServiceProvider.GetRequiredService<ISwitchingPathGenerator>();
        PathExecutor = Scope.ServiceProvider.GetRequiredService<ISwitchingPathExecutor>();
        RouteRepository = Scope.ServiceProvider.GetRequiredService<IRouteConfigurationRepository>();
        SystemRepository = Scope.ServiceProvider.GetRequiredService<ISystemConfigurationRepository>();
    }

    /// <summary>
    /// Helper method to setup default route configurations for testing
    /// </summary>
    protected void SetupDefaultRouteConfiguration()
    {
        // Clear existing configurations
        var allConfigs = RouteRepository.GetAllEnabled();
        foreach (var config in allConfigs)
        {
            RouteRepository.Delete(config.ChuteId);
        }

        // Add test configurations
        RouteRepository.InitializeDefaultData();
    }

    /// <summary>
    /// Helper method to create a test route configuration
    /// </summary>
    protected ChuteRouteConfiguration CreateTestRouteConfig(int chuteId, params (int diverterId, Core.Enums.DiverterDirection direction, int sequence)[] diverters)
    {
        var config = new ChuteRouteConfiguration
        {
            ChuteId = chuteId,
            ChuteName = $"Test Chute {chuteId}",
            DiverterConfigurations = diverters.Select(d => new DiverterConfigurationEntry
            {
                DiverterId = d.diverterId,
                TargetDirection = d.direction,
                SequenceNumber = d.sequence
            }).ToList(),
            BeltSpeedMeterPerSecond = 1.0,
            BeltLengthMeter = 10.0,
            ToleranceTimeMs = 2000,
            IsEnabled = true
        };

        RouteRepository.Upsert(config);
        return config;
    }

    public virtual void Dispose()
    {
        Scope?.Dispose();
        Client?.Dispose();
        GC.SuppressFinalize(this);
    }
}
