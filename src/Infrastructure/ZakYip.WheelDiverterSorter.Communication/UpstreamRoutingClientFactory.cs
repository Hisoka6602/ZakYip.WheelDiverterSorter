using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Communication.Abstractions;
using ZakYip.WheelDiverterSorter.Communication.Clients;
using ZakYip.WheelDiverterSorter.Communication.Configuration;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Upstream;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Core.Enums.Communication;
using ZakYip.WheelDiverterSorter.Core.Sorting.Policies;

namespace ZakYip.WheelDiverterSorter.Communication;

/// <summary>
/// 上游路由客户端工厂实现
/// </summary>
/// <remarks>
/// <para>根据配置的通信模式创建对应的客户端实例。</para>
/// <para>PR-UPSTREAM01: 移除 HTTP 协议支持，只支持 TCP/SignalR/MQTT。</para>
/// <para>默认使用 TCP 模式作为降级方案。</para>
/// <para>PR-HOTRELOAD: 工厂从配置仓储动态读取最新配置，支持热更新。</para>
/// </remarks>
public class UpstreamRoutingClientFactory : IUpstreamRoutingClientFactory
{
    /// <summary>
    /// 默认 TCP 服务器地址（仅用于配置缺失时的降级方案）
    /// </summary>
    /// <remarks>
    /// PR-UPSTREAM01: 当 TcpServer 配置为空且需要降级到 TCP 模式时，使用此默认值。
    /// </remarks>
    private const string DefaultTcpServerFallback = "localhost:9000";

    private readonly ILoggerFactory _loggerFactory;
    private readonly Func<UpstreamConnectionOptions> _optionsProvider;
    private readonly ISystemClock _systemClock;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="loggerFactory">日志工厂</param>
    /// <param name="optionsProvider">配置提供者（支持动态获取最新配置）</param>
    /// <param name="systemClock">系统时钟</param>
    public UpstreamRoutingClientFactory(
        ILoggerFactory loggerFactory,
        Func<UpstreamConnectionOptions> optionsProvider,
        ISystemClock systemClock)
    {
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _optionsProvider = optionsProvider ?? throw new ArgumentNullException(nameof(optionsProvider));
        _systemClock = systemClock ?? throw new ArgumentNullException(nameof(systemClock));
    }

    /// <summary>
    /// 创建上游路由客户端实例
    /// </summary>
    /// <returns>客户端实例</returns>
    /// <remarks>
    /// PR-UPSTREAM01: 移除 HTTP 模式支持。
    /// PR-TOUCHSOCKET01: 使用 TouchSocket 实现的 TCP 客户端。
    /// PR-HOTRELOAD: 每次创建时获取最新配置，支持热更新。
    /// 对于不支持的通信模式，会记录警告并使用 TCP 模式作为降级方案，确保程序不会崩溃。
    /// </remarks>
    public IUpstreamRoutingClient CreateClient()
    {
        // PR-HOTRELOAD: 动态获取最新配置
        var options = _optionsProvider();
        
        try
        {
            return options.Mode switch
            {
                CommunicationMode.Tcp => new TouchSocketTcpRuleEngineClient(
                    _loggerFactory.CreateLogger<TouchSocketTcpRuleEngineClient>(),
                    options,
                    _systemClock),

                CommunicationMode.SignalR => new SignalRRuleEngineClient(
                    _loggerFactory.CreateLogger<SignalRRuleEngineClient>(),
                    options,
                    _systemClock),

                CommunicationMode.Mqtt => new MqttRuleEngineClient(
                    _loggerFactory.CreateLogger<MqttRuleEngineClient>(),
                    options,
                    _systemClock),

                _ => CreateFallbackTcpClient(options)
            };
        }
        catch (Exception ex)
        {
            // 如果创建客户端失败，记录错误并使用降级方案
            var logger = _loggerFactory.CreateLogger<UpstreamRoutingClientFactory>();
            logger.LogError(ex, "创建 {Mode} 模式的上游路由客户端失败，使用 TCP 模式作为降级方案", options.Mode);
            return CreateFallbackTcpClient(options);
        }
    }

    /// <summary>
    /// 创建降级用的 TCP 客户端
    /// </summary>
    /// <remarks>
    /// PR-UPSTREAM01: 降级方案从 HTTP 改为 TCP。
    /// PR-TOUCHSOCKET01: 使用 TouchSocket 实现。
    /// </remarks>
    private IUpstreamRoutingClient CreateFallbackTcpClient(UpstreamConnectionOptions options)
    {
        var logger = _loggerFactory.CreateLogger<UpstreamRoutingClientFactory>();
        logger.LogWarning("使用 TCP 模式作为降级方案，原通信模式: {Mode}", options.Mode);
        
        // 创建新的 options 实例用于降级，避免修改原始共享实例
        var fallbackOptions = new UpstreamConnectionOptions
        {
            Mode = CommunicationMode.Tcp,
            TcpServer = string.IsNullOrWhiteSpace(options.TcpServer) 
                ? DefaultTcpServerFallback 
                : options.TcpServer,
            TimeoutMs = options.TimeoutMs,
            InitialBackoffMs = options.InitialBackoffMs,
            MaxBackoffMs = options.MaxBackoffMs,
            EnableInfiniteRetry = options.EnableInfiniteRetry,
            Tcp = options.Tcp
        };

        if (string.IsNullOrWhiteSpace(options.TcpServer))
        {
            logger.LogWarning("TcpServer 为空，使用默认值: {TcpServer}", fallbackOptions.TcpServer);
        }

        return new TouchSocketTcpRuleEngineClient(
            _loggerFactory.CreateLogger<TouchSocketTcpRuleEngineClient>(),
            fallbackOptions,
            _systemClock);
    }
}
