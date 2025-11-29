using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Sorting.Interfaces;
using ZakYip.WheelDiverterSorter.Communication.Configuration;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Drivers;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Execution;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Ingress;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Upstream;
using ZakYip.WheelDiverterSorter.Core.Enums.Communication;

namespace ZakYip.WheelDiverterSorter.Communication.Gateways;

/// <summary>
/// 上游分拣网关工厂
/// </summary>
/// <remarks>
/// <para>根据配置创建相应的网关实现。</para>
/// <para>网关使用 <see cref="IUpstreamContractMapper"/> 进行领域对象与协议 DTO 之间的转换，
/// 确保协议细节不渗透到领域层。</para>
/// PR-U1: 使用 IUpstreamRoutingClient 替代 IRuleEngineClient
/// </remarks>
public class UpstreamSortingGatewayFactory
{
    private readonly IUpstreamRoutingClient _client;
    private readonly IUpstreamContractMapper _mapper;
    private readonly RuleEngineConnectionOptions _options;
    private readonly ILoggerFactory _loggerFactory;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="client">上游路由客户端</param>
    /// <param name="mapper">上游契约映射器</param>
    /// <param name="options">连接选项</param>
    /// <param name="loggerFactory">日志工厂</param>
    public UpstreamSortingGatewayFactory(
        IUpstreamRoutingClient client,
        IUpstreamContractMapper mapper,
        RuleEngineConnectionOptions options,
        ILoggerFactory loggerFactory)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
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
                _client,
                _mapper,
                _loggerFactory.CreateLogger<TcpUpstreamSortingGateway>(),
                _options),

            CommunicationMode.SignalR => new SignalRUpstreamSortingGateway(
                _client,
                _mapper,
                _loggerFactory.CreateLogger<SignalRUpstreamSortingGateway>(),
                _options),

            CommunicationMode.Http => new HttpUpstreamSortingGateway(
                _client,
                _mapper,
                _loggerFactory.CreateLogger<HttpUpstreamSortingGateway>(),
                _options),

            _ => throw new NotSupportedException(
                $"不支持的通信模式: {_options.Mode}")
        };
    }
}
