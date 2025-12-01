using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ZakYip.WheelDiverterSorter.Communication.Abstractions;
using ZakYip.WheelDiverterSorter.Communication.Adapters;
using ZakYip.WheelDiverterSorter.Communication.Clients;
using ZakYip.WheelDiverterSorter.Communication.Configuration;
using ZakYip.WheelDiverterSorter.Communication.Infrastructure;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Hardware.Devices;
using ZakYip.WheelDiverterSorter.Core.Hardware.IoLinkage;
using ZakYip.WheelDiverterSorter.Core.Hardware.Mappings;
using ZakYip.WheelDiverterSorter.Core.Hardware.Ports;
using ZakYip.WheelDiverterSorter.Core.Hardware.Providers;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Execution;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Ingress;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Upstream;
using ZakYip.WheelDiverterSorter.Core.Enums.Communication;

namespace ZakYip.WheelDiverterSorter.Communication;

/// <summary>
/// 通信服务注册扩展
/// </summary>
/// <remarks>
/// 提供低耦合的服务注册方式，便于扩展新的通信协议
/// PR-U1: 合并上游路由客户端接口，删除中间适配层
/// PR-UPSTREAM01: 移除 HTTP 协议支持，只支持 TCP/SignalR/MQTT
/// </remarks>
public static class CommunicationServiceExtensions
{
    /// <summary>
    /// 默认配置常量
    /// </summary>
    /// <remarks>
    /// PR-UPSTREAM01: 移除 HTTP 相关常量
    /// </remarks>
    private static class DefaultConfiguration
    {
        public const string TcpServer = "localhost:9000";
        public const string SignalRHub = "http://localhost:5001/ruleengine";
        public const string MqttBroker = "localhost";
    }
    /// <summary>
    /// 添加上游路由通信服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置</param>
    /// <returns>服务集合</returns>
    /// <remarks>
    /// <para><b>⚠️ 重要架构约束：</b></para>
    /// <list type="bullet">
    ///   <item>RuleEngine连接配置<b>必须从数据库读取</b>，不允许从 appsettings.json 配置</item>
    ///   <item><b>默认为正式环境</b>，除非在 appsettings.json 中明确设置 "IsTestEnvironment": true</item>
    ///   <item>正式环境启动时从 LiteDB 数据库加载持久化配置</item>
    ///   <item>测试环境可以使用 appsettings.json 中的配置（仅用于自动化测试）</item>
    /// </list>
    /// <para>PR-UPSTREAM01: 移除 HTTP 协议支持，只支持 TCP/SignalR/MQTT，默认使用 TCP。</para>
    /// </remarks>
    public static IServiceCollection AddRuleEngineCommunication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 检查是否为测试环境（默认 false = 正式环境）
        // Check if test environment (default false = production environment)
        var isTestMode = configuration.GetValue<bool>("IsTestEnvironment", false);

        if (!isTestMode)
        {
            Console.WriteLine("🏭 [环境检测] 正式环境模式 - RuleEngine 配置将从数据库加载");
        }
        else
        {
            Console.WriteLine("🧪 [环境检测] 测试环境模式 - RuleEngine 配置将从 appsettings.json 加载");
        }

        // ⚠️ 注册配置为延迟加载单例 - 从数据库读取而非 appsettings.json
        // Register configuration as lazy-loaded singleton - load from database not appsettings.json
        services.AddSingleton<RuleEngineConnectionOptions>(sp =>
        {
            if (isTestMode)
            {
                // 测试环境：使用配置文件中的配置（仅用于自动化测试）
                // Test environment: use configuration from appsettings.json (for automated tests only)
                var testOptions = new RuleEngineConnectionOptions();
                configuration.GetSection("RuleEngineConnection").Bind(testOptions);
                
                ValidateOptions(testOptions);
                
                Console.WriteLine($"🧪 [测试配置] Mode={testOptions.Mode}, Server={GetServerAddress(testOptions)}");
                
                return testOptions;
            }
            else
            {
                // 正式环境（默认）：从数据库加载配置
                // Production environment (default): load configuration from database
                var configRepository = sp.GetRequiredService<ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces.ICommunicationConfigurationRepository>();
                var dbConfig = configRepository.Get();
                
                // 将数据库配置映射到 RuleEngineConnectionOptions
                var options = MapFromDatabaseConfig(dbConfig);
                
                ValidateOptions(options);
                
                Console.WriteLine($"✅ [数据库配置] 已加载 RuleEngine 连接配置: Mode={options.Mode}, ConnectionMode={options.ConnectionMode}, Server={GetServerAddress(options)}");
                
                return options;
            }
        });

        // 注册上游契约映射器 - 用于领域对象与协议 DTO 之间的转换
        // Register upstream contract mapper - for conversion between domain objects and protocol DTOs
        services.AddSingleton<IUpstreamContractMapper, DefaultUpstreamContractMapper>();

        // PR-U1: 注册上游路由客户端工厂（替代原 IRuleEngineClientFactory）
        services.AddSingleton<IUpstreamRoutingClientFactory, UpstreamRoutingClientFactory>();

        // PR-U1: 直接注册 IUpstreamRoutingClient（使用工厂创建，不再需要 Adapter）
        services.AddSingleton<IUpstreamRoutingClient>(sp =>
        {
            var factory = sp.GetRequiredService<IUpstreamRoutingClientFactory>();
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

        // PR-U1: 注册 UpstreamConnectionManager（用于Client模式），使用 IUpstreamRoutingClient
        services.AddSingleton<IUpstreamConnectionManager>(sp =>
        {
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<UpstreamConnectionManager>>();
            var systemClock = sp.GetRequiredService<ZakYip.WheelDiverterSorter.Core.Utilities.ISystemClock>();
            var logDeduplicator = sp.GetRequiredService<ZakYip.WheelDiverterSorter.Observability.Utilities.ILogDeduplicator>();
            var safeExecutor = sp.GetRequiredService<ZakYip.WheelDiverterSorter.Observability.Utilities.ISafeExecutionService>();
            var client = sp.GetRequiredService<IUpstreamRoutingClient>();
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
    /// <para>无论任何情况下都不会抛出异常导致程序崩溃，只记录警告信息。</para>
    /// <para>PR-UPSTREAM01: 移除 HTTP 模式验证，不支持的模式降级为 TCP。</para>
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

            default:
                // PR-UPSTREAM01: 不支持的通信模式，使用默认的 TCP 模式
                Console.WriteLine($"⚠️ [配置警告] 不支持的通信模式: {options.Mode}，已切换为 TCP 模式");
                options.Mode = CommunicationMode.Tcp;
                if (string.IsNullOrWhiteSpace(options.TcpServer))
                {
                    options.TcpServer = DefaultConfiguration.TcpServer;
                    Console.WriteLine($"⚠️ [配置警告] TCP模式下，TcpServer配置为空，已使用默认值: {options.TcpServer}");
                }
                break;
        }
    }

    /// <summary>
    /// 将数据库配置映射到 RuleEngineConnectionOptions
    /// Map database configuration to RuleEngineConnectionOptions
    /// </summary>
    /// <remarks>
    /// PR-UPSTREAM01: 移除 HTTP 配置映射。
    /// </remarks>
    private static RuleEngineConnectionOptions MapFromDatabaseConfig(ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models.CommunicationConfiguration dbConfig)
    {
        return new RuleEngineConnectionOptions
        {
            Mode = dbConfig.Mode,
            ConnectionMode = dbConfig.ConnectionMode,
            TcpServer = dbConfig.TcpServer,
            SignalRHub = dbConfig.SignalRHub,
            MqttBroker = dbConfig.MqttBroker,
            MqttTopic = dbConfig.MqttTopic,
            TimeoutMs = dbConfig.TimeoutMs,
            RetryCount = dbConfig.RetryCount,
            RetryDelayMs = dbConfig.RetryDelayMs,
            EnableAutoReconnect = dbConfig.EnableAutoReconnect,
            InitialBackoffMs = dbConfig.InitialBackoffMs,
            MaxBackoffMs = dbConfig.MaxBackoffMs,
            EnableInfiniteRetry = dbConfig.EnableInfiniteRetry,
            Tcp = new TcpOptions
            {
                ReceiveBufferSize = dbConfig.Tcp.ReceiveBufferSize,
                SendBufferSize = dbConfig.Tcp.SendBufferSize,
                NoDelay = dbConfig.Tcp.NoDelay
            },
            Mqtt = new MqttOptions
            {
                QualityOfServiceLevel = dbConfig.Mqtt.QualityOfServiceLevel,
                CleanSession = dbConfig.Mqtt.CleanSession,
                SessionExpiryInterval = dbConfig.Mqtt.SessionExpiryInterval,
                MessageExpiryInterval = dbConfig.Mqtt.MessageExpiryInterval,
                ClientIdPrefix = dbConfig.Mqtt.ClientIdPrefix
            },
            SignalR = new SignalROptions
            {
                HandshakeTimeout = dbConfig.SignalR.HandshakeTimeout,
                KeepAliveInterval = dbConfig.SignalR.KeepAliveInterval,
                ServerTimeout = dbConfig.SignalR.ServerTimeout,
                SkipNegotiation = dbConfig.SignalR.SkipNegotiation
            }
        };
    }

    /// <summary>
    /// 获取服务器地址（用于日志）
    /// Get server address (for logging)
    /// </summary>
    /// <remarks>
    /// PR-UPSTREAM01: 移除 HTTP 地址获取。
    /// </remarks>
    private static string GetServerAddress(RuleEngineConnectionOptions options)
    {
        return options.Mode switch
        {
            CommunicationMode.Tcp => options.TcpServer ?? "未配置",
            CommunicationMode.SignalR => options.SignalRHub ?? "未配置",
            CommunicationMode.Mqtt => options.MqttBroker ?? "未配置",
            _ => "未知模式"
        };
    }
}
