using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using ZakYip.WheelDiverterSorter.Communication.Abstractions;
using ZakYip.WheelDiverterSorter.Communication.Configuration;
using ZakYip.WheelDiverterSorter.Core.Enums.Communication;

namespace ZakYip.WheelDiverterSorter.Host.IntegrationTests;

/// <summary>
/// Custom WebApplicationFactory for integration tests
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    /// <summary>
    /// Gets the JsonSerializerOptions configured for the application (with enum string conversion)
    /// </summary>
    public static JsonSerializerOptions JsonSerializerOptions => TestJsonOptions.GetOptions();

    public Mock<IRuleEngineClient>? MockRuleEngineClient { get; private set; }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        // Set environment to Testing to avoid production constraints
        builder.UseEnvironment("Testing");
        
        // Configure test-specific settings - MUST be done before Program.Main runs
        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Clear existing configuration sources to ensure our test config takes precedence
            config.Sources.Clear();
            
            // Add minimal valid configuration for RuleEngine to pass validation
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Configure RuleEngine communication with minimal valid settings
                // This prevents validation errors in AddRuleEngineCommunication
                ["RuleEngineConnection:Mode"] = "Http",
                ["RuleEngineConnection:HttpApi"] = "http://localhost:9999/test",
                ["RuleEngineConnection:TimeoutMs"] = "5000",
                
                // Configure simulation mode
                ["IsSimulationMode"] = "true",
                
                // Configure driver settings
                ["Driver:UseHardwareDriver"] = "false",
                ["Driver:VendorId"] = "Simulated",
                
                // Configure routes database
                ["RouteConfiguration:DatabasePath"] = "Data/routes_test.db",
                
                // Configure logging
                ["Logging:LogLevel:Default"] = "Warning",
                ["Logging:LogLevel:Microsoft.Hosting.Lifetime"] = "Warning"
            });
        });
        
        // Configure test-specific services - this runs after AddRuleEngineCommunication
        builder.ConfigureServices(services =>
        {
            // Remove existing RuleEngine client if registered and replace with mock
            var ruleEngineClientDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IRuleEngineClient));
            if (ruleEngineClientDescriptor != null)
            {
                services.Remove(ruleEngineClientDescriptor);
            }

            // Add mock RuleEngine client
            MockRuleEngineClient = new Mock<IRuleEngineClient>(MockBehavior.Loose);
            
            // Set up default behaviors
            MockRuleEngineClient
                .Setup(x => x.ConnectAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            
            MockRuleEngineClient
                .Setup(x => x.NotifyParcelDetectedAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            
            MockRuleEngineClient
                .Setup(x => x.DisconnectAsync())
                .Returns(Task.CompletedTask);
            
            MockRuleEngineClient
                .Setup(x => x.IsConnected)
                .Returns(true);
            
            services.AddSingleton(MockRuleEngineClient.Object);
        });

        return base.CreateHost(builder);
    }
}
