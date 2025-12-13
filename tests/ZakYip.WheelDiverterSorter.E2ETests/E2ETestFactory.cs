using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Upstream;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;

namespace ZakYip.WheelDiverterSorter.E2ETests;

/// <summary>
/// 用于E2E测试的自定义WebApplicationFactory，包含模拟依赖项
/// </summary>
public class E2ETestFactory : WebApplicationFactory<Program>
{
    private VendorId _vendorId = VendorId.Simulated;
    
    public Mock<IUpstreamRoutingClient>? MockRuleEngineClient { get; private set; }

    /// <summary>
    /// 设置要使用的厂商驱动
    /// </summary>
    /// <param name="vendorId">厂商ID</param>
    public void SetVendorId(VendorId vendorId)
    {
        _vendorId = vendorId;
    }

    /// <summary>
    /// 重置模拟调用记录，确保测试之间互不干扰
    /// </summary>
    public void ResetMockInvocations()
    {
        MockRuleEngineClient?.Invocations.Clear();
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        // 配置厂商驱动选择
        builder.ConfigureAppConfiguration((context, config) =>
        {
            // 添加内存配置来覆盖厂商设置
            var vendorConfig = new Dictionary<string, string>
            {
                ["Driver:UseHardwareDriver"] = (_vendorId != VendorId.Simulated).ToString(),
                ["Driver:VendorId"] = _vendorId.ToString()
            };
            config.AddInMemoryCollection(vendorConfig!);
        });

        builder.ConfigureServices(services =>
        {
            // 移除现有的RuleEngine客户端（如果已注册）
            var ruleEngineClientDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IUpstreamRoutingClient));
            if (ruleEngineClientDescriptor != null)
            {
                services.Remove(ruleEngineClientDescriptor);
            }

            // 添加模拟RuleEngine客户端，使用Loose模式允许未显式设置的调用
            MockRuleEngineClient = new Mock<IUpstreamRoutingClient>(MockBehavior.Loose);
            
            // 为具有可选参数的方法设置默认行为
            // 这确保在未显式传递CancellationToken时方法也能正常工作
            MockRuleEngineClient
                .Setup(x => x.ConnectAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            
            MockRuleEngineClient
                .Setup(x => x.SendAsync(It.IsAny<IUpstreamMessage>(), It.IsAny<CancellationToken>()))
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
