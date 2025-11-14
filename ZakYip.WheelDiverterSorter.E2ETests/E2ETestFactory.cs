using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using ZakYip.WheelDiverterSorter.Communication.Abstractions;

namespace ZakYip.WheelDiverterSorter.E2ETests;

/// <summary>
/// Custom WebApplicationFactory for E2E tests with mock dependencies
/// </summary>
public class E2ETestFactory : WebApplicationFactory<Program>
{
    public Mock<IRuleEngineClient>? MockRuleEngineClient { get; private set; }

    /// <summary>
    /// Reset mock invocations to ensure tests don't interfere with each other
    /// </summary>
    public void ResetMockInvocations()
    {
        MockRuleEngineClient?.Invocations.Clear();
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove existing RuleEngine client if registered
            var ruleEngineClientDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IRuleEngineClient));
            if (ruleEngineClientDescriptor != null)
            {
                services.Remove(ruleEngineClientDescriptor);
            }

            // Add mock RuleEngine client with default behavior for optional parameters
            // Using MockBehavior.Loose to allow calls without explicit setups
            MockRuleEngineClient = new Mock<IRuleEngineClient>(MockBehavior.Loose);
            
            // Setup default behaviors for methods with optional parameters
            // This ensures compatibility when methods are called without explicit CancellationToken
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

        builder.UseEnvironment("Test");

        return base.CreateHost(builder);
    }
}
