using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Execution.Queues;
using ZakYip.WheelDiverterSorter.Execution.Orchestration;
using ZakYip.WheelDiverterSorter.Observability.Utilities;

namespace ZakYip.WheelDiverterSorter.Execution.BackgroundServices;

/// <summary>
/// 待执行包裹超时监控后台服务
/// </summary>
/// <remarks>
/// 监听PendingQueue的超时事件，而不是定期轮询。
/// 当包裹入队时立即注册超时任务，超时后自动触发异常处理。
/// </remarks>
public class PendingParcelTimeoutMonitor : BackgroundService
{
    private readonly IPendingParcelQueue _pendingQueue;
    private readonly SortingOrchestrator _orchestrator;
    private readonly ISafeExecutionService _safeExecutor;
    private readonly ILogger<PendingParcelTimeoutMonitor> _logger;
    private readonly CancellationTokenSource _stopTokenSource = new();

    public PendingParcelTimeoutMonitor(
        IPendingParcelQueue pendingQueue,
        SortingOrchestrator orchestrator,
        ISafeExecutionService safeExecutor,
        ILogger<PendingParcelTimeoutMonitor> logger)
    {
        _pendingQueue = pendingQueue ?? throw new ArgumentNullException(nameof(pendingQueue));
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _safeExecutor = safeExecutor ?? throw new ArgumentNullException(nameof(safeExecutor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("待执行包裹超时监控服务已启动（事件驱动模式）");

        // 订阅队列的超时事件
        _pendingQueue.ParcelTimedOut += OnParcelTimedOut;

        await _safeExecutor.ExecuteAsync(
            async () =>
            {
                // 保持服务运行，等待超时事件触发
                await Task.Delay(Timeout.Infinite, stoppingToken);
            },
            operationName: "PendingParcelTimeoutMonitor_EventLoop",
            cancellationToken: stoppingToken);
    }

    private void OnParcelTimedOut(object? sender, ParcelTimedOutEventArgs e)
    {
        _logger.LogWarning(
            "包裹 {ParcelId} 等待超时({ElapsedMs}ms)，原目标格口: {TargetChuteId}，摆轮节点: {WheelNodeId}",
            e.ParcelId,
            e.ElapsedMs,
            e.TargetChuteId,
            e.WheelNodeId);

        // 异步处理超时包裹（不阻塞事件处理）
        _ = Task.Run(async () =>
        {
            try
            {
                await _safeExecutor.ExecuteAsync(
                    async () =>
                    {
                        // 从队列中移除（防止重复处理）
                        var removed = _pendingQueue.DequeueByWheelNode(e.WheelNodeId);
                        
                        if (removed == null)
                        {
                            _logger.LogWarning(
                                "尝试处理超时包裹 {ParcelId} 时，未能从队列中取出（可能已被其他线程处理）",
                                e.ParcelId);
                            return;
                        }

                        _logger.LogInformation(
                            "包裹 {ParcelId} 将被路由到异常格口",
                            e.ParcelId);
                        
                        // TODO: 调用Orchestrator的异常处理方法执行到异常格口的分拣
                        // await _orchestrator.ProcessTimedOutParcelAsync(e.ParcelId, _stopTokenSource.Token);
                    },
                    operationName: $"ProcessTimedOutParcel_{e.ParcelId}",
                    cancellationToken: _stopTokenSource.Token);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "处理超时包裹 {ParcelId} 时发生错误",
                    e.ParcelId);
            }
        }, _stopTokenSource.Token);
    }

    public override void Dispose()
    {
        _pendingQueue.ParcelTimedOut -= OnParcelTimedOut;
        _stopTokenSource.Cancel();
        _stopTokenSource.Dispose();
        base.Dispose();
    }
}
