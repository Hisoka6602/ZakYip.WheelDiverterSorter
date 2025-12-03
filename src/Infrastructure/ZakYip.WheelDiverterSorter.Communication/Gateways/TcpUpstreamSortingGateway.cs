using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Sorting.Policies;
using ZakYip.WheelDiverterSorter.Core.Sorting.Contracts;
using ZakYip.WheelDiverterSorter.Core.Sorting.Policies;
using ZakYip.WheelDiverterSorter.Core.Sorting.Exceptions;
using ZakYip.WheelDiverterSorter.Core.Sorting.Policies;
using ZakYip.WheelDiverterSorter.Core.Sorting.Interfaces;
using ZakYip.WheelDiverterSorter.Core.Sorting.Policies;
using ZakYip.WheelDiverterSorter.Communication.Models;
using ZakYip.WheelDiverterSorter.Core.Sorting.Policies;
using ZakYip.WheelDiverterSorter.Core.Hardware.Devices;
using ZakYip.WheelDiverterSorter.Core.Sorting.Policies;
using ZakYip.WheelDiverterSorter.Core.Hardware.IoLinkage;
using ZakYip.WheelDiverterSorter.Core.Sorting.Policies;
using ZakYip.WheelDiverterSorter.Core.Hardware.Mappings;
using ZakYip.WheelDiverterSorter.Core.Sorting.Policies;
using ZakYip.WheelDiverterSorter.Core.Hardware.Ports;
using ZakYip.WheelDiverterSorter.Core.Sorting.Policies;
using ZakYip.WheelDiverterSorter.Core.Hardware.Providers;
using ZakYip.WheelDiverterSorter.Core.Sorting.Policies;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Execution;
using ZakYip.WheelDiverterSorter.Core.Sorting.Policies;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Ingress;
using ZakYip.WheelDiverterSorter.Core.Sorting.Policies;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Upstream;
using ZakYip.WheelDiverterSorter.Core.Sorting.Policies;

namespace ZakYip.WheelDiverterSorter.Communication.Gateways;

/// <summary>
/// TCP 协议的上游分拣网关实现
/// </summary>
/// <remarks>
/// <para>适配上游路由客户端，提供协议层编解码和基础重试。</para>
/// <para>使用 <see cref="IUpstreamContractMapper"/> 进行领域对象与协议 DTO 之间的转换，
/// 确保协议细节不渗透到领域层。</para>
/// PR-U1: 使用 IUpstreamRoutingClient 替代 IRuleEngineClient
/// </remarks>
public class TcpUpstreamSortingGateway : IUpstreamSortingGateway
{
    private readonly IUpstreamRoutingClient _client;
    private readonly IUpstreamContractMapper _mapper;
    private readonly ILogger<TcpUpstreamSortingGateway> _logger;
    private readonly UpstreamConnectionOptions _options;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="client">上游路由客户端</param>
    /// <param name="mapper">上游契约映射器</param>
    /// <param name="logger">日志记录器</param>
    /// <param name="options">连接选项</param>
    public TcpUpstreamSortingGateway(
        IUpstreamRoutingClient client,
        IUpstreamContractMapper mapper,
        ILogger<TcpUpstreamSortingGateway> logger,
        UpstreamConnectionOptions options)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// 请求分拣决策
    /// </summary>
    public async Task<SortingResponse> RequestSortingAsync(
        SortingRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        try
        {
            _logger.LogDebug(
                "通过 TCP 网关请求分拣决策: ParcelId={ParcelId}",
                request.ParcelId);

            // 确保连接已建立
            if (!_client.IsConnected)
            {
                var connected = await _client.ConnectAsync(cancellationToken);
                if (!connected)
                {
                    throw new UpstreamUnavailableException("无法连接到上游 TCP 服务器");
                }
            }

            // 使用 TaskCompletionSource 等待事件响应
            var tcs = new TaskCompletionSource<SortingResponse>();
            
            // 订阅事件处理响应
            // PR-UPSTREAM02: 从 ChuteAssignmentReceived 改为 ChuteAssigned
            EventHandler<ChuteAssignmentEventArgs>? handler = null;
            handler = (sender, eventArgs) =>
            {
                if (eventArgs.ParcelId == request.ParcelId)
                {
                    _client.ChuteAssigned -= handler;
                    
                    // 使用映射器将协议通知转换为领域层响应
                    var notification = new UpstreamChuteAssignmentNotification
                    {
                        ParcelId = eventArgs.ParcelId,
                        ChuteId = eventArgs.ChuteId,
                        NotificationTime = eventArgs.AssignedAt,
                        Source = "TcpUpstreamGateway"
                    };
                    var response = _mapper.MapFromUpstreamNotification(notification);
                    
                    tcs.TrySetResult(response);
                }
            };
            
            _client.ChuteAssigned += handler;

            try
            {
                // 发送通知
                var notified = await _client.NotifyParcelDetectedAsync(
                    request.ParcelId,
                    cancellationToken);

                if (!notified)
                {
                    _client.ChuteAssigned -= handler;
                    throw new UpstreamUnavailableException("发送包裹通知失败");
                }

                // 等待响应（带超时）
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(_options.TimeoutMs);
                
                var response = await tcs.Task.WaitAsync(timeoutCts.Token);

                if (!response.IsSuccess)
                {
                    _logger.LogWarning(
                        "上游返回失败响应: ParcelId={ParcelId}, Error={Error}",
                        request.ParcelId,
                        response.ErrorMessage);
                }

                return response;
            }
            catch
            {
                _client.ChuteAssigned -= handler;
                throw;
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning(
                "TCP 网关请求超时: ParcelId={ParcelId}",
                request.ParcelId);
            throw new UpstreamUnavailableException("请求超时", new OperationCanceledException());
        }
        catch (Exception ex) when (ex is not UpstreamUnavailableException && ex is not InvalidResponseException)
        {
            _logger.LogError(
                ex,
                "TCP 网关请求失败: ParcelId={ParcelId}",
                request.ParcelId);
            throw new UpstreamUnavailableException($"通信失败: {ex.Message}", ex);
        }
    }
}
