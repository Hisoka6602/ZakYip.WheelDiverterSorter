using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZakYip.WheelDiverterSorter.Core.Sorting.Contracts;
using ZakYip.WheelDiverterSorter.Ingress.Upstream.Configuration;

namespace ZakYip.WheelDiverterSorter.Ingress.Upstream;

/// <summary>
/// 上游门面实现，统一管理多个上游通道
/// </summary>
public class UpstreamFacade : IUpstreamFacade
{
    private readonly IEnumerable<IUpstreamChannel> _channels;
    private readonly IEnumerable<IUpstreamCommandSender> _commandSenders;
    private readonly IngressOptions _options;
    private readonly ILogger<UpstreamFacade>? _logger;

    public UpstreamFacade(
        IEnumerable<IUpstreamChannel> channels,
        IEnumerable<IUpstreamCommandSender> commandSenders,
        IOptions<IngressOptions> options,
        ILogger<UpstreamFacade>? logger = null)
    {
        _channels = channels ?? throw new ArgumentNullException(nameof(channels));
        _commandSenders = commandSenders ?? throw new ArgumentNullException(nameof(commandSenders));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger;
    }

    public string CurrentChannelName
    {
        get
        {
            var channel = _channels.FirstOrDefault(c => c.IsConnected);
            return channel?.Name ?? "None";
        }
    }

    public IReadOnlyList<string> AvailableChannels
    {
        get
        {
            return _channels.Select(c => c.Name).ToList();
        }
    }

    public async Task<OperationResult<AssignChuteResponse>> AssignChuteAsync(
        AssignChuteRequest request,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var timeout = TimeSpan.FromMilliseconds(_options.DefaultTimeoutMs);

        // 尝试所有可用的命令发送器
        foreach (var sender in _commandSenders)
        {
            try
            {
                _logger?.LogDebug("尝试使用命令发送器分配格口: ParcelId={ParcelId}", request.ParcelId);

                var response = await sender.SendCommandAsync<AssignChuteRequest, AssignChuteResponse>(
                    request,
                    timeout,
                    cancellationToken);

                sw.Stop();

                if (response.IsSuccess && response.ChuteId > 0)
                {
                    _logger?.LogInformation(
                        "成功分配格口: ParcelId={ParcelId}, ChuteId={ChuteId}, Latency={LatencyMs}ms",
                        request.ParcelId,
                        response.ChuteId,
                        sw.ElapsedMilliseconds);

                    return OperationResult<AssignChuteResponse>.Success(
                        response,
                        sw.ElapsedMilliseconds,
                        response.Source ?? CurrentChannelName);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "命令发送器调用失败: ParcelId={ParcelId}", request.ParcelId);
                // 继续尝试下一个发送器
            }
        }

        sw.Stop();

        // 所有通道都失败，启用降级策略
        if (_options.EnableFallback)
        {
            _logger?.LogWarning(
                "所有上游通道失败，使用降级策略: ParcelId={ParcelId}, FallbackChuteId={ChuteId}",
                request.ParcelId,
                _options.FallbackChuteId);

            var fallbackResponse = new AssignChuteResponse
            {
                ParcelId = request.ParcelId,
                ChuteId = _options.FallbackChuteId,
                IsSuccess = true,
                Source = "Fallback",
                ReasonCode = "AllChannelsFailed"
            };

            return OperationResult<AssignChuteResponse>.Fallback(
                fallbackResponse,
                sw.ElapsedMilliseconds,
                "Fallback");
        }

        // 无降级策略，返回失败
        return OperationResult<AssignChuteResponse>.Failure(
            "所有上游通道调用失败且无降级策略",
            "AllChannelsFailed",
            sw.ElapsedMilliseconds);
    }

    public async Task<OperationResult<CreateParcelResponse>> CreateParcelAsync(
        CreateParcelRequest request,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var timeout = TimeSpan.FromMilliseconds(_options.DefaultTimeoutMs);

        // 尝试所有可用的命令发送器
        foreach (var sender in _commandSenders)
        {
            try
            {
                _logger?.LogDebug("尝试使用命令发送器创建包裹: ParcelId={ParcelId}", request.ParcelId);

                var response = await sender.SendCommandAsync<CreateParcelRequest, CreateParcelResponse>(
                    request,
                    timeout,
                    cancellationToken);

                sw.Stop();

                if (response.IsSuccess)
                {
                    _logger?.LogInformation(
                        "成功创建包裹: ParcelId={ParcelId}, Latency={LatencyMs}ms",
                        request.ParcelId,
                        sw.ElapsedMilliseconds);

                    return OperationResult<CreateParcelResponse>.Success(
                        response,
                        sw.ElapsedMilliseconds,
                        CurrentChannelName);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "命令发送器调用失败: ParcelId={ParcelId}", request.ParcelId);
                // 继续尝试下一个发送器
            }
        }

        sw.Stop();

        // 创建包裹是通知性质，即使失败也返回成功
        _logger?.LogWarning(
            "所有上游通道失败，但继续处理包裹: ParcelId={ParcelId}",
            request.ParcelId);

        var fallbackResponse = new CreateParcelResponse
        {
            ParcelId = request.ParcelId,
            IsSuccess = true
        };

        return OperationResult<CreateParcelResponse>.Success(
            fallbackResponse,
            sw.ElapsedMilliseconds,
            "Local");
    }
}
