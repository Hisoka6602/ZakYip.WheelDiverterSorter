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

            // Add mock RuleEngine client
            MockRuleEngineClient = new Mock<IRuleEngineClient>();
            services.AddSingleton(MockRuleEngineClient.Object);
        });

        builder.UseEnvironment("Test");

        return base.CreateHost(builder);
    }
}
