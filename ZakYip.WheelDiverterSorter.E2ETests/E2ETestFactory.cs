using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using ZakYip.WheelDiverterSorter.Communication.Abstractions;

namespace ZakYip.WheelDiverterSorter.E2ETests;

/// <summary>
/// 用于E2E测试的自定义WebApplicationFactory，包含模拟依赖项
/// </summary>
public class E2ETestFactory : WebApplicationFactory<Program>
{
    public Mock<IRuleEngineClient>? MockRuleEngineClient { get; private set; }

    /// <summary>
    /// 重置模拟调用记录，确保测试之间互不干扰
    /// </summary>
    public void ResetMockInvocations()
    {
        MockRuleEngineClient?.Invocations.Clear();
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // 移除现有的RuleEngine客户端（如果已注册）
            var ruleEngineClientDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IRuleEngineClient));
            if (ruleEngineClientDescriptor != null)
            {
                services.Remove(ruleEngineClientDescriptor);
            }

            // 添加模拟RuleEngine客户端，使用Loose模式允许未显式设置的调用
            MockRuleEngineClient = new Mock<IRuleEngineClient>(MockBehavior.Loose);
            
            // 为具有可选参数的方法设置默认行为
            // 这确保在未显式传递CancellationToken时方法也能正常工作
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
