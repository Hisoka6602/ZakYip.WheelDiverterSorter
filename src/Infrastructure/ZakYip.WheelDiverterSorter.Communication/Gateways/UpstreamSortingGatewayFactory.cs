using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Sorting.Interfaces;
using ZakYip.WheelDiverterSorter.Communication.Abstractions;
using ZakYip.WheelDiverterSorter.Communication.Adapters;
using ZakYip.WheelDiverterSorter.Communication.Configuration;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Execution.Abstractions;

namespace ZakYip.WheelDiverterSorter.Communication.Gateways;

/// <summary>
/// 上游分拣网关工厂
/// </summary>
/// <remarks>
/// <para>根据配置创建相应的网关实现。</para>
/// <para>网关使用 <see cref="IUpstreamContractMapper"/> 进行领域对象与协议 DTO 之间的转换，
/// 确保协议细节不渗透到领域层。</para>
/// </remarks>
public class UpstreamSortingGatewayFactory
{
    private readonly IRuleEngineClient _ruleEngineClient;
    private readonly IUpstreamContractMapper _mapper;
    private readonly RuleEngineConnectionOptions _options;
    private readonly ILoggerFactory _loggerFactory;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="ruleEngineClient">规则引擎客户端</param>
    /// <param name="mapper">上游契约映射器</param>
    /// <param name="options">连接选项</param>
    /// <param name="loggerFactory">日志工厂</param>
    public UpstreamSortingGatewayFactory(
        IRuleEngineClient ruleEngineClient,
        IUpstreamContractMapper mapper,
        RuleEngineConnectionOptions options,
        ILoggerFactory loggerFactory)
    {
        _ruleEngineClient = ruleEngineClient ?? throw new ArgumentNullException(nameof(ruleEngineClient));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
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
                _mapper,
                _loggerFactory.CreateLogger<TcpUpstreamSortingGateway>(),
                _options),

            CommunicationMode.SignalR => new SignalRUpstreamSortingGateway(
                _ruleEngineClient,
                _mapper,
                _loggerFactory.CreateLogger<SignalRUpstreamSortingGateway>(),
                _options),

            CommunicationMode.Http => new HttpUpstreamSortingGateway(
                _ruleEngineClient,
                _mapper,
                _loggerFactory.CreateLogger<HttpUpstreamSortingGateway>(),
                _options),

            _ => throw new NotSupportedException(
                $"不支持的通信模式: {_options.Mode}")
        };
    }
}
