using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ZakYip.WheelDiverterSorter.Communication.Abstractions;
using ZakYip.WheelDiverterSorter.Communication.Clients;
using ZakYip.WheelDiverterSorter.Communication.Configuration;
using ZakYip.WheelDiverterSorter.Communication.Infrastructure;
using ZakYip.WheelDiverterSorter.Core.Enums;

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
    /// 默认配置常量
    /// </summary>
    private static class DefaultConfiguration
    {
        public const string TcpServer = "localhost:9000";
        public const string SignalRHub = "http://localhost:5001/ruleengine";
        public const string MqttBroker = "localhost";
        public const string HttpApi = "http://localhost:9999/api/ruleengine";
    }
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

        // 检查是否为测试环境（通过配置标记）
        var isTestMode = configuration.GetValue<bool>("IsTestEnvironment", false);
        
        // 在测试环境下，如果配置为空，提供默认测试配置
        if (isTestMode && string.IsNullOrWhiteSpace(options.HttpApi) && options.Mode == CommunicationMode.Http)
        {
            options.HttpApi = "http://localhost:9999/test-stub";
        }

        // 验证配置（测试环境的默认配置也需要通过验证）
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
    /// 添加EMC资源锁服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddEmcResourceLock(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 绑定配置
        var emcLockOptions = new EmcLockOptions();
        configuration.GetSection("EmcLock").Bind(emcLockOptions);
        services.AddSingleton(Options.Create(emcLockOptions));

        // 注册各种实现
        services.AddSingleton<TcpEmcResourceLockManager>();
        services.AddSingleton<SignalREmcResourceLockManager>();
        services.AddSingleton<MqttEmcResourceLockManager>();

        // 注册工厂
        services.AddSingleton<EmcResourceLockManagerFactory>();

        // 注册锁管理器（使用工厂创建）
        services.AddSingleton<IEmcResourceLockManager>(sp =>
        {
            var factory = sp.GetRequiredService<EmcResourceLockManagerFactory>();
            return factory.CreateLockManager();
        });

        return services;
    }

    /// <summary>
    /// 添加上游连接管理服务
    /// Add upstream connection management service
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddUpstreamConnectionManagement(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 从DI容器获取已注册的配置（由AddRuleEngineCommunication注册）
        // 如果还未注册，则读取并注册
        RuleEngineConnectionOptions? options = null;
        
        // 尝试从已构建的服务提供者获取配置
        var serviceProvider = services.BuildServiceProvider();
        try
        {
            options = serviceProvider.GetService<RuleEngineConnectionOptions>();
        }
        catch
        {
            // 如果获取失败，则从配置中读取
        }
        finally
        {
            serviceProvider.Dispose();
        }

        // 如果无法从DI获取，则从配置文件读取
        if (options == null)
        {
            options = new RuleEngineConnectionOptions();
            configuration.GetSection("RuleEngineConnection").Bind(options);
        }

        // 注册 UpstreamConnectionManager（用于Client模式）
        services.AddSingleton<IUpstreamConnectionManager>(sp =>
        {
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<UpstreamConnectionManager>>();
            var systemClock = sp.GetRequiredService<ZakYip.WheelDiverterSorter.Core.Utilities.ISystemClock>();
            var logDeduplicator = sp.GetRequiredService<ZakYip.WheelDiverterSorter.Observability.Utilities.ILogDeduplicator>();
            var safeExecutor = sp.GetRequiredService<ZakYip.WheelDiverterSorter.Observability.Utilities.ISafeExecutionService>();
            var client = sp.GetRequiredService<IRuleEngineClient>();
            // 从DI容器获取已注册的配置，确保使用相同的配置实例
            var connectionOptions = sp.GetRequiredService<RuleEngineConnectionOptions>();

            return new UpstreamConnectionManager(
                logger,
                systemClock,
                logDeduplicator,
                safeExecutor,
                client,
                connectionOptions);
        });

        // 注册 RuleEngineServerFactory（用于Server模式）
        services.AddSingleton<RuleEngineServerFactory>();

        // 始终注册两个后台服务，但它们会在启动时检查配置决定是否真正启动
        // Always register both background services, but they check configuration at startup
        services.AddHostedService<UpstreamConnectionBackgroundService>();
        services.AddHostedService<UpstreamServerBackgroundService>();

        return services;
    }

    /// <summary>
    /// 验证配置有效性，如果配置为空则提供默认值
    /// </summary>
    /// <param name="options">连接配置</param>
    /// <remarks>
    /// 无论任何情况下都不会抛出异常导致程序崩溃，只记录警告信息
    /// </remarks>
    private static void ValidateOptions(RuleEngineConnectionOptions options)
    {
        switch (options.Mode)
        {
            case CommunicationMode.Tcp:
                if (string.IsNullOrWhiteSpace(options.TcpServer))
                {
                    options.TcpServer = DefaultConfiguration.TcpServer;
                    Console.WriteLine($"⚠️ [配置警告] TCP模式下，TcpServer配置为空，已使用默认值: {options.TcpServer}");
                }
                break;

            case CommunicationMode.SignalR:
                if (string.IsNullOrWhiteSpace(options.SignalRHub))
                {
                    options.SignalRHub = DefaultConfiguration.SignalRHub;
                    Console.WriteLine($"⚠️ [配置警告] SignalR模式下，SignalRHub配置为空，已使用默认值: {options.SignalRHub}");
                }
                break;

            case CommunicationMode.Mqtt:
                if (string.IsNullOrWhiteSpace(options.MqttBroker))
                {
                    options.MqttBroker = DefaultConfiguration.MqttBroker;
                    Console.WriteLine($"⚠️ [配置警告] MQTT模式下，MqttBroker配置为空，已使用默认值: {options.MqttBroker}");
                }
                break;

            case CommunicationMode.Http:
                if (string.IsNullOrWhiteSpace(options.HttpApi))
                {
                    options.HttpApi = DefaultConfiguration.HttpApi;
                    Console.WriteLine($"⚠️ [配置警告] HTTP模式下，HttpApi配置为空，已使用默认值: {options.HttpApi}");
                }
                break;

            default:
                // 不支持的通信模式，使用默认的Http模式
                Console.WriteLine($"⚠️ [配置警告] 不支持的通信模式: {options.Mode}，已切换为Http模式");
                options.Mode = CommunicationMode.Http;
                if (string.IsNullOrWhiteSpace(options.HttpApi))
                {
                    options.HttpApi = DefaultConfiguration.HttpApi;
                    Console.WriteLine($"⚠️ [配置警告] HTTP模式下，HttpApi配置为空，已使用默认值: {options.HttpApi}");
                }
                break;
        }
    }
}
