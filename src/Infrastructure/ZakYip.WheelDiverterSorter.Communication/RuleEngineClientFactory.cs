using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Communication.Abstractions;
using ZakYip.WheelDiverterSorter.Communication.Clients;
using ZakYip.WheelDiverterSorter.Communication.Configuration;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Core.Enums.Communication;

namespace ZakYip.WheelDiverterSorter.Communication;

/// <summary>
/// RuleEngine客户端工厂实现
/// </summary>
/// <remarks>
/// 根据配置的通信模式创建对应的客户端实例
/// 支持TCP、SignalR、MQTT、HTTP等多种协议
/// </remarks>
public class RuleEngineClientFactory : IRuleEngineClientFactory
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly RuleEngineConnectionOptions _options;
    private readonly ISystemClock _systemClock;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="loggerFactory">日志工厂</param>
    /// <param name="options">连接配置</param>
    /// <param name="systemClock">系统时钟</param>
    public RuleEngineClientFactory(
        ILoggerFactory loggerFactory,
        RuleEngineConnectionOptions options,
        ISystemClock systemClock)
    {
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _systemClock = systemClock ?? throw new ArgumentNullException(nameof(systemClock));
    }

    /// <summary>
    /// 创建RuleEngine客户端实例
    /// </summary>
    /// <returns>客户端实例</returns>
    /// <remarks>
    /// 对于不支持的通信模式，会记录警告并使用Http模式作为降级方案，确保程序不会崩溃
    /// </remarks>
    public IRuleEngineClient CreateClient()
    {
        try
        {
            return _options.Mode switch
            {
                CommunicationMode.Tcp => new TcpRuleEngineClient(
                    _loggerFactory.CreateLogger<TcpRuleEngineClient>(),
                    _options,
                    _systemClock),

                CommunicationMode.SignalR => new SignalRRuleEngineClient(
                    _loggerFactory.CreateLogger<SignalRRuleEngineClient>(),
                    _options,
                    _systemClock),

                CommunicationMode.Mqtt => new MqttRuleEngineClient(
                    _loggerFactory.CreateLogger<MqttRuleEngineClient>(),
                    _options,
                    _systemClock),

                CommunicationMode.Http => new HttpRuleEngineClient(
                    _loggerFactory.CreateLogger<HttpRuleEngineClient>(),
                    _options,
                    _systemClock),

                _ => CreateFallbackHttpClient()
            };
        }
        catch (Exception ex)
        {
            // 如果创建客户端失败，记录错误并使用降级方案
            var logger = _loggerFactory.CreateLogger<RuleEngineClientFactory>();
            logger.LogError(ex, "创建 {Mode} 模式的RuleEngine客户端失败，使用Http模式作为降级方案", _options.Mode);
            return CreateFallbackHttpClient();
        }
    }

    /// <summary>
    /// 创建降级用的Http客户端
    /// </summary>
    private IRuleEngineClient CreateFallbackHttpClient()
    {
        var logger = _loggerFactory.CreateLogger<RuleEngineClientFactory>();
        logger.LogWarning("使用Http模式作为降级方案，通信模式: {Mode}", _options.Mode);
        
        // 创建新的options实例用于降级，避免修改原始共享实例
        var fallbackOptions = new RuleEngineConnectionOptions
        {
            Mode = CommunicationMode.Http,
            HttpApi = string.IsNullOrWhiteSpace(_options.HttpApi) 
                ? "http://localhost:9999/api/ruleengine" 
                : _options.HttpApi,
            TimeoutMs = _options.TimeoutMs,
            RetryCount = _options.RetryCount,
            RetryDelayMs = _options.RetryDelayMs,
            Http = _options.Http
        };

        if (string.IsNullOrWhiteSpace(_options.HttpApi))
        {
            logger.LogWarning("HttpApi为空，使用默认值: {HttpApi}", fallbackOptions.HttpApi);
        }

        return new HttpRuleEngineClient(
            _loggerFactory.CreateLogger<HttpRuleEngineClient>(),
            fallbackOptions,
            _systemClock);
    }
}
