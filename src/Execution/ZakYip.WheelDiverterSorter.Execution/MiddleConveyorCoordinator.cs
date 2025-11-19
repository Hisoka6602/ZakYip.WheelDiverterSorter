using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.LineModel;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;
using ZakYip.WheelDiverterSorter.Core.LineModel.Enums;


using ZakYip.WheelDiverterSorter.Core.LineModel.Bindings;
using ZakYip.WheelDiverterSorter.Core.LineModel.Segments;namespace ZakYip.WheelDiverterSorter.Execution;

/// <summary>
/// 中段皮带多段联动协调器实现。
/// 负责协调多个中段皮带段的启停，支持顺序控制和策略配置。
/// </summary>
public sealed class MiddleConveyorCoordinator : IMiddleConveyorCoordinator
{
    private readonly ILogger<MiddleConveyorCoordinator> _logger;
    private readonly MiddleConveyorIoOptions _options;

    public IReadOnlyList<IConveyorSegment> Segments { get; }

    public MiddleConveyorCoordinator(
        IReadOnlyList<IConveyorSegment> segments,
        MiddleConveyorIoOptions options,
        ILogger<MiddleConveyorCoordinator> logger)
    {
        Segments = segments ?? throw new ArgumentNullException(nameof(segments));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async ValueTask<ConveyorOperationResult> StartAllAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogDebug("中段皮带联动功能未启用，跳过启动操作");
            return ConveyorOperationResult.Success();
        }

        if (Segments.Count == 0)
        {
            _logger.LogWarning("没有配置中段皮带段，跳过启动操作");
            return ConveyorOperationResult.Success();
        }

        _logger.LogInformation("开始联动启动所有中段皮带段，策略={Strategy}", _options.StartOrderStrategy);

        var orderedSegments = OrderSegmentsForStart();

        foreach (var segment in orderedSegments)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await segment.StartAsync(cancellationToken);
            if (!result.IsSuccess)
            {
                _logger.LogError("皮带段 [{SegmentKey}] 启动失败: {Reason}", segment.SegmentId.Key, result.FailureReason);
                return result;
            }

            // 如果不是同时启动策略，添加延迟
            if (_options.StartOrderStrategy != "Simultaneous" && _options.LinkageDelayMs > 0)
            {
                await Task.Delay(_options.LinkageDelayMs, cancellationToken);
            }
        }

        _logger.LogInformation("所有中段皮带段启动完成");
        return ConveyorOperationResult.Success();
    }

    public async ValueTask<ConveyorOperationResult> StopAllAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogDebug("中段皮带联动功能未启用，跳过停止操作");
            return ConveyorOperationResult.Success();
        }

        if (Segments.Count == 0)
        {
            _logger.LogWarning("没有配置中段皮带段，跳过停止操作");
            return ConveyorOperationResult.Success();
        }

        _logger.LogInformation("开始联动停止所有中段皮带段，策略={Strategy}", _options.StopOrderStrategy);

        var orderedSegments = OrderSegmentsForStop();

        foreach (var segment in orderedSegments)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await segment.StopAsync(cancellationToken);
            if (!result.IsSuccess)
            {
                _logger.LogWarning("皮带段 [{SegmentKey}] 停止失败: {Reason}", segment.SegmentId.Key, result.FailureReason);
                // 停止失败时继续停止其他段
            }

            // 如果不是同时停止策略，添加延迟
            if (_options.StopOrderStrategy != "Simultaneous" && _options.LinkageDelayMs > 0)
            {
                await Task.Delay(_options.LinkageDelayMs, cancellationToken);
            }
        }

        _logger.LogInformation("所有中段皮带段停止完成");
        return ConveyorOperationResult.Success();
    }

    public async ValueTask<ConveyorOperationResult> StopDownstreamFirstAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogDebug("中段皮带联动功能未启用，跳过停止操作");
            return ConveyorOperationResult.Success();
        }

        if (Segments.Count == 0)
        {
            _logger.LogWarning("没有配置中段皮带段，跳过停止操作");
            return ConveyorOperationResult.Success();
        }

        _logger.LogInformation("开始先停下游的有序停机");

        // 按优先级倒序排列（优先级数值大的先停，即下游先停）
        var orderedSegments = Segments.OrderByDescending(s => s.SegmentId.Priority).ToList();

        foreach (var segment in orderedSegments)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await segment.StopAsync(cancellationToken);
            if (!result.IsSuccess)
            {
                _logger.LogWarning("皮带段 [{SegmentKey}] 停止失败: {Reason}", segment.SegmentId.Key, result.FailureReason);
            }

            if (_options.LinkageDelayMs > 0)
            {
                await Task.Delay(_options.LinkageDelayMs, cancellationToken);
            }
        }

        _logger.LogInformation("先停下游的有序停机完成");
        return ConveyorOperationResult.Success();
    }

    public bool HasAnyFault()
    {
        return Segments.Any(s => s.State == ConveyorSegmentState.Fault);
    }

    private IReadOnlyList<IConveyorSegment> OrderSegmentsForStart()
    {
        return _options.StartOrderStrategy switch
        {
            "UpstreamFirst" => Segments.OrderBy(s => s.SegmentId.Priority).ToList(),
            "DownstreamFirst" => Segments.OrderByDescending(s => s.SegmentId.Priority).ToList(),
            "Simultaneous" => Segments.ToList(),
            _ => Segments.OrderBy(s => s.SegmentId.Priority).ToList()
        };
    }

    private IReadOnlyList<IConveyorSegment> OrderSegmentsForStop()
    {
        return _options.StopOrderStrategy switch
        {
            "DownstreamFirst" => Segments.OrderByDescending(s => s.SegmentId.Priority).ToList(),
            "UpstreamFirst" => Segments.OrderBy(s => s.SegmentId.Priority).ToList(),
            "Simultaneous" => Segments.ToList(),
            _ => Segments.OrderByDescending(s => s.SegmentId.Priority).ToList()
        };
    }
}
