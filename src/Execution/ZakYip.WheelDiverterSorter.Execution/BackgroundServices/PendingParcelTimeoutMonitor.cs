using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Execution;
using ZakYip.WheelDiverterSorter.Execution.Configuration;
using ZakYip.WheelDiverterSorter.Execution.Queues;
using ZakYip.WheelDiverterSorter.Observability.Utilities;

namespace ZakYip.WheelDiverterSorter.Execution.BackgroundServices;

/// <summary>
/// 待执行包裹超时监控后台服务
/// </summary>
/// <remarks>
/// 定期检查PendingParcelQueue中的包裹是否超时，
/// 超时的包裹将自动路由到异常格口并执行分拣。
/// </remarks>
public class PendingParcelTimeoutMonitor : BackgroundService
{
    private readonly IPendingParcelQueue _pendingQueue;
    private readonly ISortingOrchestrator _orchestrator;
    private readonly ISafeExecutionService _safeExecutor;
    private readonly ILogger<PendingParcelTimeoutMonitor> _logger;
    private readonly TopologyDrivenSortingOptions _options;

    public PendingParcelTimeoutMonitor(
        IPendingParcelQueue pendingQueue,
        ISortingOrchestrator orchestrator,
        ISafeExecutionService safeExecutor,
        ILogger<PendingParcelTimeoutMonitor> logger,
        IOptions<TopologyDrivenSortingOptions> options)
    {
        _pendingQueue = pendingQueue ?? throw new ArgumentNullException(nameof(pendingQueue));
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _safeExecutor = safeExecutor ?? throw new ArgumentNullException(nameof(safeExecutor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? new TopologyDrivenSortingOptions();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "待执行包裹超时监控服务已启动，检查间隔: {IntervalSeconds}秒，默认超时: {TimeoutSeconds}秒",
            _options.MonitorIntervalSeconds,
            _options.DefaultTimeoutSeconds);

        await _safeExecutor.ExecuteAsync(
            async () =>
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    await CheckAndProcessTimedOutParcelsAsync(stoppingToken);
                    await Task.Delay(
                        TimeSpan.FromSeconds(_options.MonitorIntervalSeconds),
                        stoppingToken);
                }
            },
            operationName: "PendingParcelTimeoutMonitor_MainLoop",
            cancellationToken: stoppingToken);
    }

    private async Task CheckAndProcessTimedOutParcelsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var timedOutParcels = _pendingQueue.GetTimedOutParcels();
            
            if (timedOutParcels.Count == 0)
            {
                return;
            }

            _logger.LogWarning(
                "检测到 {Count} 个超时包裹，将路由到异常格口",
                timedOutParcels.Count);

            foreach (var parcel in timedOutParcels)
            {
                await ProcessTimedOutParcelAsync(parcel, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "检查超时包裹时发生错误");
        }
    }

    private async Task ProcessTimedOutParcelAsync(
        PendingParcel parcel,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogWarning(
                "包裹 {ParcelId} 等待超时({ElapsedSeconds}秒)，原目标格口: {TargetChuteId}，摆轮节点: {WheelNodeId}",
                parcel.ParcelId,
                (DateTimeOffset.Now - parcel.EnqueuedAt).TotalSeconds,
                parcel.TargetChuteId,
                parcel.WheelNodeId);

            // 从队列中移除（防止重复处理）
            var removed = _pendingQueue.DequeueByWheelNode(parcel.WheelNodeId);
            
            if (removed == null)
            {
                _logger.LogWarning(
                    "尝试处理超时包裹 {ParcelId} 时，未能从队列中取出（可能已被其他线程处理）",
                    parcel.ParcelId);
                return;
            }

            // 通过Orchestrator路由到异常格口并执行分拣
            _logger.LogInformation(
                "包裹 {ParcelId} 将被路由到异常格口",
                parcel.ParcelId);
            
            // TODO: 调用Orchestrator的异常处理方法执行到异常格口的分拣
            // await _orchestrator.ProcessTimedOutParcelAsync(parcel.ParcelId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "处理超时包裹 {ParcelId} 时发生错误",
                parcel.ParcelId);
        }
    }
}
