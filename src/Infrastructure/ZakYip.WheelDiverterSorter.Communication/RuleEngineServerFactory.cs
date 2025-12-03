using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Communication.Abstractions;
using ZakYip.WheelDiverterSorter.Communication.Configuration;
using ZakYip.WheelDiverterSorter.Communication.Servers;
using ZakYip.WheelDiverterSorter.Core.Enums.Communication;
using ZakYip.WheelDiverterSorter.Core.Utilities;

namespace ZakYip.WheelDiverterSorter.Communication;

/// <summary>
/// RuleEngine服务器工厂 - 根据配置创建相应的服务器实例
/// RuleEngine server factory - creates server instances based on configuration
/// </summary>
public class RuleEngineServerFactory
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly ISystemClock _systemClock;
    private readonly IHubContext<RuleEngineHub>? _hubContext;
    private readonly IRuleEngineHandler? _handler;

    public RuleEngineServerFactory(
        ILoggerFactory loggerFactory,
        ISystemClock systemClock,
        IHubContext<RuleEngineHub>? hubContext = null,
        IRuleEngineHandler? handler = null)
    {
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _systemClock = systemClock ?? throw new ArgumentNullException(nameof(systemClock));
        _hubContext = hubContext;
        _handler = handler;
    }

    /// <summary>
    /// 根据配置创建服务器实例
    /// Create server instance based on configuration
    /// </summary>
    /// <param name="options">连接配置选项 / Connection options</param>
    /// <returns>服务器实例 / Server instance</returns>
    /// <exception cref="ArgumentException">当配置无效时抛出 / Thrown when configuration is invalid</exception>
    public IRuleEngineServer CreateServer(RuleEngineConnectionOptions options)
    {
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        if (options.ConnectionMode != ConnectionMode.Server)
        {
            throw new ArgumentException(
                $"服务器工厂只能用于Server模式，当前模式: {options.ConnectionMode}",
                nameof(options));
        }

        return options.Mode switch
        {
            CommunicationMode.Tcp => CreateTcpServer(options),
            CommunicationMode.Mqtt => CreateMqttServer(options),
            CommunicationMode.SignalR => CreateSignalRServer(options),
            _ => throw new ArgumentException(
                $"不支持的通信模式: {options.Mode}。Server模式仅支持 TCP, MQTT, SignalR",
                nameof(options))
        };
    }

    private IRuleEngineServer CreateTcpServer(RuleEngineConnectionOptions options)
    {
        var logger = _loggerFactory.CreateLogger<TcpRuleEngineServer>();
        return new TcpRuleEngineServer(logger, options, _systemClock, _handler);
    }

    private IRuleEngineServer CreateMqttServer(RuleEngineConnectionOptions options)
    {
        var logger = _loggerFactory.CreateLogger<MqttRuleEngineServer>();
        return new MqttRuleEngineServer(logger, options, _systemClock, _handler);
    }

    private IRuleEngineServer CreateSignalRServer(RuleEngineConnectionOptions options)
    {
        var logger = _loggerFactory.CreateLogger<SignalRRuleEngineServer>();
        return new SignalRRuleEngineServer(logger, options, _systemClock, _hubContext, _handler);
    }
}
