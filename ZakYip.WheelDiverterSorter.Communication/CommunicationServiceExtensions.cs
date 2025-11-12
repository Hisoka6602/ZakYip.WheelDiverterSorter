using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ZakYip.WheelDiverterSorter.Communication.Abstractions;
using ZakYip.WheelDiverterSorter.Communication.Configuration;

namespace ZakYip.WheelDiverterSorter.Communication;

/// <summary>
/// 通信服务注册扩展
/// </summary>
/// <remarks>
/// 提供低耦合的服务注册方式，便于扩展新的通信协议
/// </remarks>
public static class CommunicationServiceExtensions
{
    /// <summary>
    /// 添加RuleEngine通信服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddRuleEngineCommunication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 绑定配置
        var options = new RuleEngineConnectionOptions();
        configuration.GetSection("RuleEngineConnection").Bind(options);

        // 验证配置
        ValidateOptions(options);

        // 注册配置为单例
        services.AddSingleton(options);

        // 注册客户端工厂
        services.AddSingleton<IRuleEngineClientFactory, RuleEngineClientFactory>();

        // 注册客户端（使用工厂创建）
        services.AddSingleton<IRuleEngineClient>(sp =>
        {
            var factory = sp.GetRequiredService<IRuleEngineClientFactory>();
            return factory.CreateClient();
        });

        return services;
    }

    /// <summary>
    /// 验证配置有效性
    /// </summary>
    /// <param name="options">连接配置</param>
    /// <exception cref="InvalidOperationException">配置无效</exception>
    private static void ValidateOptions(RuleEngineConnectionOptions options)
    {
        switch (options.Mode)
        {
            case CommunicationMode.Tcp:
                if (string.IsNullOrWhiteSpace(options.TcpServer))
                {
                    throw new InvalidOperationException("TCP模式下，TcpServer配置不能为空");
                }
                break;

            case CommunicationMode.SignalR:
                if (string.IsNullOrWhiteSpace(options.SignalRHub))
                {
                    throw new InvalidOperationException("SignalR模式下，SignalRHub配置不能为空");
                }
                break;

            case CommunicationMode.Mqtt:
                if (string.IsNullOrWhiteSpace(options.MqttBroker))
                {
                    throw new InvalidOperationException("MQTT模式下，MqttBroker配置不能为空");
                }
                break;

            case CommunicationMode.Http:
                if (string.IsNullOrWhiteSpace(options.HttpApi))
                {
                    throw new InvalidOperationException("HTTP模式下，HttpApi配置不能为空");
                }
                break;

            default:
                throw new NotSupportedException($"不支持的通信模式: {options.Mode}");
        }
    }
}
