using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Sorting.Interfaces;
using ZakYip.WheelDiverterSorter.Communication.Abstractions;
using ZakYip.WheelDiverterSorter.Communication.Configuration;
using ZakYip.WheelDiverterSorter.Core.Enums.Communication;
using ZakYip.WheelDiverterSorter.Core.Enums.Conveyor;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Enums.Routing;
using ZakYip.WheelDiverterSorter.Core.Enums.Sensors;
using ZakYip.WheelDiverterSorter.Core.Enums.System;

namespace ZakYip.WheelDiverterSorter.Communication.Gateways;

/// <summary>
/// 上游分拣网关工厂
/// </summary>
/// <remarks>
/// 根据配置创建相应的网关实现
/// </remarks>
public class UpstreamSortingGatewayFactory
{
    private readonly IRuleEngineClient _ruleEngineClient;
    private readonly RuleEngineConnectionOptions _options;
    private readonly ILoggerFactory _loggerFactory;

    /// <summary>
    /// 构造函数
    /// </summary>
    public UpstreamSortingGatewayFactory(
        IRuleEngineClient ruleEngineClient,
        RuleEngineConnectionOptions options,
        ILoggerFactory loggerFactory)
    {
        _ruleEngineClient = ruleEngineClient ?? throw new ArgumentNullException(nameof(ruleEngineClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
    }

    /// <summary>
    /// 创建网关实例
    /// </summary>
    public IUpstreamSortingGateway CreateGateway()
    {
        return _options.Mode switch
        {
            CommunicationMode.Tcp => new TcpUpstreamSortingGateway(
                _ruleEngineClient,
                _loggerFactory.CreateLogger<TcpUpstreamSortingGateway>(),
                _options),

            CommunicationMode.SignalR => new SignalRUpstreamSortingGateway(
                _ruleEngineClient,
                _loggerFactory.CreateLogger<SignalRUpstreamSortingGateway>(),
                _options),

            CommunicationMode.Http => new HttpUpstreamSortingGateway(
                _ruleEngineClient,
                _loggerFactory.CreateLogger<HttpUpstreamSortingGateway>(),
                _options),

            _ => throw new NotSupportedException(
                $"不支持的通信模式: {_options.Mode}")
        };
    }
}
