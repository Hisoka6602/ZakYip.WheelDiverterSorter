using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;
using ZakYip.WheelDiverterSorter.Communication;
using ZakYip.WheelDiverterSorter.Communication.Configuration;
using ZakYip.WheelDiverterSorter.Communication.Infrastructure;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Enums.Communication;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Observability.Utilities;

namespace ZakYip.WheelDiverterSorter.Communication.Tests.Infrastructure;

/// <summary>
/// Tests for UpstreamServerBackgroundService and service registration
/// </summary>
public class UpstreamServerBackgroundServiceTests
{
    [Fact]
    public void AddUpstreamConnectionManagement_WithClientMode_RegistersClientBackgroundService()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["IsTestEnvironment"] = "true",  // Enable test mode to use configuration instead of database
                ["RuleEngineConnection:Mode"] = "Tcp",
                ["RuleEngineConnection:ConnectionMode"] = "Client",
                ["RuleEngineConnection:TcpServer"] = "localhost:9000",
                ["RuleEngineConnection:EnableInfiniteRetry"] = "true",
                ["RuleEngineConnection:MaxBackoffMs"] = "2000"
            })
            .Build();

        // Add required dependencies
        services.AddLogging();
        services.AddInfrastructureServices();
        services.AddRuleEngineCommunication(configuration);

        // Act
        services.AddUpstreamConnectionManagement(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var hostedServices = serviceProvider.GetServices<IHostedService>().ToList();
        Assert.Contains(hostedServices, s => s.GetType() == typeof(UpstreamConnectionBackgroundService));
    }

    [Fact]
    public void AddUpstreamConnectionManagement_WithServerMode_RegistersServerBackgroundService()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["IsTestEnvironment"] = "true",  // Enable test mode to use configuration instead of database
                ["RuleEngineConnection:Mode"] = "Tcp",
                ["RuleEngineConnection:ConnectionMode"] = "Server",
                ["RuleEngineConnection:TcpServer"] = "localhost:9000"
            })
            .Build();

        // Add required dependencies
        services.AddLogging();
        services.AddInfrastructureServices();
        services.AddRuleEngineCommunication(configuration);

        // Act
        services.AddUpstreamConnectionManagement(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var hostedServices = serviceProvider.GetServices<IHostedService>().ToList();
        Assert.Contains(hostedServices, s => s.GetType() == typeof(UpstreamServerBackgroundService));
    }

    [Fact]
    public void AddUpstreamConnectionManagement_AlwaysRegistersBothBackgroundServices()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["IsTestEnvironment"] = "true",  // Enable test mode to use configuration instead of database
                ["RuleEngineConnection:Mode"] = "Tcp",
                ["RuleEngineConnection:ConnectionMode"] = "Client",
                ["RuleEngineConnection:TcpServer"] = "localhost:9000"
            })
            .Build();

        // Add required dependencies
        services.AddLogging();
        services.AddInfrastructureServices();
        services.AddRuleEngineCommunication(configuration);

        // Act
        services.AddUpstreamConnectionManagement(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert - Both services are registered
        var hostedServices = serviceProvider.GetServices<IHostedService>().ToList();
        Assert.Contains(hostedServices, s => s.GetType() == typeof(UpstreamConnectionBackgroundService));
        Assert.Contains(hostedServices, s => s.GetType() == typeof(UpstreamServerBackgroundService));
    }

    [Fact]
    public void AddUpstreamConnectionManagement_RegistersBothServices_RegardlessOfMode()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["IsTestEnvironment"] = "true",  // Enable test mode to use configuration instead of database
                ["RuleEngineConnection:Mode"] = "Tcp",
                ["RuleEngineConnection:ConnectionMode"] = "Server",
                ["RuleEngineConnection:TcpServer"] = "localhost:9000"
            })
            .Build();

        // Add required dependencies
        services.AddLogging();
        services.AddInfrastructureServices();
        services.AddRuleEngineCommunication(configuration);

        // Act
        services.AddUpstreamConnectionManagement(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert - Both services are registered (they check mode at runtime)
        var hostedServices = serviceProvider.GetServices<IHostedService>().ToList();
        Assert.Contains(hostedServices, s => s.GetType() == typeof(UpstreamConnectionBackgroundService));
        Assert.Contains(hostedServices, s => s.GetType() == typeof(UpstreamServerBackgroundService));
    }

    [Fact]
    public void AddUpstreamConnectionManagement_AlwaysRegistersConnectionManager()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["IsTestEnvironment"] = "true",  // Enable test mode to use configuration instead of database
                ["RuleEngineConnection:Mode"] = "Tcp",
                ["RuleEngineConnection:ConnectionMode"] = "Client",
                ["RuleEngineConnection:TcpServer"] = "localhost:9000"
            })
            .Build();

        // Add required dependencies
        services.AddLogging();
        services.AddInfrastructureServices();
        services.AddRuleEngineCommunication(configuration);

        // Act
        services.AddUpstreamConnectionManagement(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var connectionManager = serviceProvider.GetService<Abstractions.IUpstreamConnectionManager>();
        Assert.NotNull(connectionManager);
    }

    [Fact]
    public void AddUpstreamConnectionManagement_AlwaysRegistersServerFactory()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RuleEngineConnection:Mode"] = "Tcp",
                ["RuleEngineConnection:ConnectionMode"] = "Server",
                ["RuleEngineConnection:TcpServer"] = "localhost:9000"
            })
            .Build();

        // Add required dependencies
        services.AddLogging();
        services.AddInfrastructureServices();
        services.AddRuleEngineCommunication(configuration);

        // Act
        services.AddUpstreamConnectionManagement(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var serverFactory = serviceProvider.GetService<RuleEngineServerFactory>();
        Assert.NotNull(serverFactory);
    }
}
