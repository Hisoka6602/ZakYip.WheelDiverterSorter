using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Communication.Abstractions;
using ZakYip.WheelDiverterSorter.Communication.Clients;
using ZakYip.WheelDiverterSorter.Communication.Configuration;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Utilities;

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
    /// <exception cref="NotSupportedException">不支持的通信模式</exception>
    public IRuleEngineClient CreateClient()
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

            _ => throw new NotSupportedException($"不支持的通信模式: {_options.Mode}")
        };
    }
}
