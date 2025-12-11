using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Events.Queue;
using ZakYip.WheelDiverterSorter.Core.Sorting.Orchestration;
using ZakYip.WheelDiverterSorter.Execution.Queues;
using ZakYip.WheelDiverterSorter.Observability.Utilities;

namespace ZakYip.WheelDiverterSorter.Execution.BackgroundServices;

/// <summary>
/// 待执行包裹超时监控服务（事件驱动模式）
/// </summary>
/// <remarks>
/// <para>订阅 PendingParcelQueue 的 ParcelTimedOut 事件，当包裹超时时自动处理。</para>
/// <para>使用 SafeExecutionService 包裹所有异步操作，确保异常不会导致服务崩溃。</para>
/// <para>依赖 ISortingOrchestrator 接口而非具体实现，遵循依赖倒置原则。</para>
/// </remarks>
public class PendingParcelTimeoutMonitor : BackgroundService
{
    private readonly IPendingParcelQueue _pendingQueue;
    private readonly ISortingOrchestrator _orchestrator;
    private readonly ISafeExecutionService _safeExecutor;
    private readonly ILogger<PendingParcelTimeoutMonitor> _logger;

    public PendingParcelTimeoutMonitor(
        IPendingParcelQueue pendingQueue,
        ISortingOrchestrator orchestrator,
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

        // 使用 SafeExecutionService 包裹等待逻辑
        await _safeExecutor.ExecuteAsync(
            async () =>
            {
                // 保持服务运行，直到取消
                await Task.Delay(Timeout.Infinite, stoppingToken);
            },
            operationName: "PendingParcelTimeoutMonitor_EventLoop",
            cancellationToken: stoppingToken
        );

        _logger.LogInformation("待执行包裹超时监控服务已停止");
    }

    /// <summary>
    /// 处理包裹超时事件（使用 SafeInvoke 模式）
    /// </summary>
    private void OnParcelTimedOut(object? sender, ParcelTimedOutEventArgs e)
    {
        // 使用 SafeExecutionService 包裹异步处理逻辑
        _ = _safeExecutor.ExecuteAsync(
            async () =>
            {
                _logger.LogWarning(
                    "包裹 {ParcelId} 等待超时，摆轮节点: {WheelNodeId}, 等待时间: {ElapsedMs}ms，准备处理超时包裹",
                    e.ParcelId, e.WheelNodeId, e.ElapsedMs);

                // 从队列中移除超时包裹（防止重复处理）
                var removed = _pendingQueue.DequeueByWheelNode(e.WheelNodeId);
                
                if (removed != null)
                {
                    _logger.LogInformation(
                        "包裹 {ParcelId} 已从队列移除，开始处理超时逻辑",
                        e.ParcelId);

                    // 调用 Orchestrator 处理超时包裹
                    await _orchestrator.ProcessTimedOutParcelAsync(e.ParcelId);
                }
                else
                {
                    _logger.LogDebug(
                        "包裹 {ParcelId} 可能已被其他操作处理，跳过超时处理",
                        e.ParcelId);
                }
            },
            operationName: $"ProcessTimedOutParcel_{e.ParcelId}",
            cancellationToken: default
        );
    }

    public override void Dispose()
    {
        // 取消订阅事件
        _pendingQueue.ParcelTimedOut -= OnParcelTimedOut;
        base.Dispose();
    }
}
