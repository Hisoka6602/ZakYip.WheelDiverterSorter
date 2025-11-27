using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Sorting.Contracts;
using ZakYip.WheelDiverterSorter.Core.Sorting.Exceptions;
using ZakYip.WheelDiverterSorter.Core.Sorting.Interfaces;
using ZakYip.WheelDiverterSorter.Communication.Abstractions;
using ZakYip.WheelDiverterSorter.Communication.Configuration;
using ZakYip.WheelDiverterSorter.Communication.Models;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Drivers;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Execution;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Ingress;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Upstream;

namespace ZakYip.WheelDiverterSorter.Communication.Gateways;

/// <summary>
/// SignalR 协议的上游分拣网关实现
/// </summary>
/// <remarks>
/// <para>适配 SignalRRuleEngineClient，提供协议层编解码和基础重试。</para>
/// <para>使用 <see cref="IUpstreamContractMapper"/> 进行领域对象与协议 DTO 之间的转换，
/// 确保协议细节不渗透到领域层。</para>
/// <para>推荐生产环境使用</para>
/// </remarks>
public class SignalRUpstreamSortingGateway : IUpstreamSortingGateway
{
    private readonly IRuleEngineClient _signalRClient;
    private readonly IUpstreamContractMapper _mapper;
    private readonly ILogger<SignalRUpstreamSortingGateway> _logger;
    private readonly RuleEngineConnectionOptions _options;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="signalRClient">SignalR 规则引擎客户端</param>
    /// <param name="mapper">上游契约映射器</param>
    /// <param name="logger">日志记录器</param>
    /// <param name="options">连接选项</param>
    public SignalRUpstreamSortingGateway(
        IRuleEngineClient signalRClient,
        IUpstreamContractMapper mapper,
        ILogger<SignalRUpstreamSortingGateway> logger,
        RuleEngineConnectionOptions options)
    {
        _signalRClient = signalRClient ?? throw new ArgumentNullException(nameof(signalRClient));
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
                "通过 SignalR 网关请求分拣决策: ParcelId={ParcelId}",
                request.ParcelId);

            // 确保连接已建立
            if (!_signalRClient.IsConnected)
            {
                var connected = await _signalRClient.ConnectAsync(cancellationToken);
                if (!connected)
                {
                    throw new UpstreamUnavailableException("无法连接到上游 SignalR 服务器");
                }
            }

            // 使用 TaskCompletionSource 等待事件响应
            var tcs = new TaskCompletionSource<SortingResponse>();
            
            // 订阅事件处理响应
            EventHandler<ChuteAssignmentNotificationEventArgs>? handler = null;
            handler = (sender, eventArgs) =>
            {
                if (eventArgs.ParcelId == request.ParcelId)
                {
                    _signalRClient.ChuteAssignmentReceived -= handler;
                    
                    // 使用映射器将协议通知转换为领域层响应
                    var notification = new UpstreamChuteAssignmentNotification
                    {
                        ParcelId = eventArgs.ParcelId,
                        ChuteId = eventArgs.ChuteId,
                        NotificationTime = eventArgs.NotificationTime,
                        Source = "SignalRUpstreamGateway"
                    };
                    var response = _mapper.MapFromUpstreamNotification(notification);
                    
                    tcs.TrySetResult(response);
                }
            };
            
            _signalRClient.ChuteAssignmentReceived += handler;

            try
            {
                // 发送通知
                var notified = await _signalRClient.NotifyParcelDetectedAsync(
                    request.ParcelId,
                    cancellationToken);

                if (!notified)
                {
                    _signalRClient.ChuteAssignmentReceived -= handler;
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
                _signalRClient.ChuteAssignmentReceived -= handler;
                throw;
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning(
                "SignalR 网关请求超时: ParcelId={ParcelId}",
                request.ParcelId);
            throw new UpstreamUnavailableException("请求超时", new OperationCanceledException());
        }
        catch (Exception ex) when (ex is not UpstreamUnavailableException && ex is not InvalidResponseException)
        {
            _logger.LogError(
                ex,
                "SignalR 网关请求失败: ParcelId={ParcelId}",
                request.ParcelId);
            throw new UpstreamUnavailableException($"通信失败: {ex.Message}", ex);
        }
    }
}
