using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Communication.Abstractions;
using ZakYip.WheelDiverterSorter.Communication.Clients;
using ZakYip.WheelDiverterSorter.Communication.Configuration;
using ZakYip.WheelDiverterSorter.Communication.Infrastructure;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Upstream;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Core.Enums.Communication;
using ZakYip.WheelDiverterSorter.Core.Sorting.Policies;
using ZakYip.WheelDiverterSorter.Observability.Utilities;

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
    private readonly UpstreamServerBackgroundService? _serverBackgroundService;
    private readonly ISafeExecutionService _safeExecutor;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="loggerFactory">日志工厂</param>
    /// <param name="optionsProvider">配置提供者（支持动态获取最新配置）</param>
    /// <param name="systemClock">系统时钟</param>
    /// <param name="safeExecutor">安全执行服务</param>
    /// <param name="serverBackgroundService">服务器后台服务（Server模式下使用）</param>
    /// <remarks>
    /// PR-DUAL-INSTANCE-FIX: 添加 serverBackgroundService 参数，用于 Server 模式下引用统一的服务器实例
    /// PR-UPSTREAM-SERVER-FIX: 添加 safeExecutor 参数，用于 ServerModeClientAdapter 的安全执行服务
    /// </remarks>
    public UpstreamRoutingClientFactory(
        ILoggerFactory loggerFactory,
        Func<UpstreamConnectionOptions> optionsProvider,
        ISystemClock systemClock,
        ISafeExecutionService safeExecutor,
        UpstreamServerBackgroundService? serverBackgroundService = null)
    {
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _optionsProvider = optionsProvider ?? throw new ArgumentNullException(nameof(optionsProvider));
        _systemClock = systemClock ?? throw new ArgumentNullException(nameof(systemClock));
        _safeExecutor = safeExecutor ?? throw new ArgumentNullException(nameof(safeExecutor));
        _serverBackgroundService = serverBackgroundService;
    }

    /// <summary>
    /// 创建上游路由客户端实例
    /// </summary>
    /// <returns>客户端实例</returns>
    /// <remarks>
    /// PR-UPSTREAM01: 移除 HTTP 模式支持。
    /// PR-TOUCHSOCKET01: 使用 TouchSocket 实现的 TCP 客户端。
    /// PR-HOTRELOAD: 每次创建时获取最新配置，支持热更新。
    /// PR-SERVER-ADAPTER: 在Server模式下，返回ServerModeClientAdapter包装IRuleEngineServer。
    /// 对于不支持的通信模式，会记录警告并使用 TCP 模式作为降级方案，确保程序不会崩溃。
    /// </remarks>
    public IUpstreamRoutingClient CreateClient()
    {
        // PR-HOTRELOAD: 动态获取最新配置
        var options = _optionsProvider();
        
        try
        {
            // PR-SERVER-ADAPTER: 如果是服务端模式，创建适配器包装服务器
            if (options.ConnectionMode == ConnectionMode.Server)
            {
                return CreateServerModeAdapter(options);
            }

            // 客户端模式：根据通信协议创建对应客户端
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

    /// <summary>
    /// 创建服务端模式适配器
    /// </summary>
    /// <remarks>
    /// PR-SERVER-ADAPTER: 在Server模式下，创建ServerModeClientAdapter包装IRuleEngineServer。
    /// 适配器将IUpstreamRoutingClient接口调用转换为IRuleEngineServer的广播调用。
    /// 
    /// PR-DUAL-INSTANCE-FIX: 不再创建新的服务器实例，而是引用 UpstreamServerBackgroundService.CurrentServer
    /// 这确保整个系统只有一个服务器实例在运行。
    /// </remarks>
    private IUpstreamRoutingClient CreateServerModeAdapter(UpstreamConnectionOptions options)
    {
        var logger = _loggerFactory.CreateLogger<UpstreamRoutingClientFactory>();
        logger.LogInformation("创建服务端模式适配器，通信模式: {Mode}", options.Mode);

        if (_serverBackgroundService == null)
        {
            throw new InvalidOperationException(
                "Server 模式下需要 UpstreamServerBackgroundService，但未提供。" +
                "请确保在 DI 容器中正确注册了 UpstreamServerBackgroundService。");
        }

        // PR-DUAL-INSTANCE-FIX: 使用 UpstreamServerBackgroundService，不再创建新的服务器实例
        return new Adapters.ServerModeClientAdapter(
            _serverBackgroundService,
            _loggerFactory.CreateLogger<Adapters.ServerModeClientAdapter>(),
            _systemClock,
            _safeExecutor);
    }
}
