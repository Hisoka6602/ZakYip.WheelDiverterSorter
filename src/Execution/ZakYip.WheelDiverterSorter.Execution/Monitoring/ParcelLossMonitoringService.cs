using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Core.Events.Queue;
using ZakYip.WheelDiverterSorter.Execution.Queues;
using ZakYip.WheelDiverterSorter.Execution.Tracking;

namespace ZakYip.WheelDiverterSorter.Execution.Monitoring;

/// <summary>
/// 包裹丢失主动监控服务
/// </summary>
/// <remarks>
/// 定期扫描队列中的任务，检测是否有包裹超过丢失判定截止时间仍未到达。
/// 这是主动检测机制，用于发现那些永远不会到达传感器的丢失包裹。
/// </remarks>
public class ParcelLossMonitoringService : BackgroundService
{
    private readonly IPositionIndexQueueManager _queueManager;
    private readonly IPositionIntervalTracker? _intervalTracker;
    private readonly ISystemClock _clock;
    private readonly ILogger<ParcelLossMonitoringService> _logger;
    
    // 监控间隔：每500ms扫描一次
    private const int MonitoringIntervalMs = 500;
    
    /// <summary>
    /// 包裹丢失事件
    /// </summary>
    public event EventHandler<ParcelLostEventArgs>? ParcelLostDetected;

    public ParcelLossMonitoringService(
        IPositionIndexQueueManager queueManager,
        ISystemClock clock,
        ILogger<ParcelLossMonitoringService> logger,
        IPositionIntervalTracker? intervalTracker = null)
    {
        _queueManager = queueManager ?? throw new ArgumentNullException(nameof(queueManager));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _intervalTracker = intervalTracker;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("包裹丢失监控服务已启动，监控间隔: {IntervalMs}ms", MonitoringIntervalMs);
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await MonitorQueuesForLostParcels();
                await Task.Delay(MonitoringIntervalMs, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // 正常停止
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "包裹丢失监控服务执行异常");
                // 继续运行，不中断监控
            }
        }
        
        _logger.LogInformation("包裹丢失监控服务已停止");
    }

    private async Task MonitorQueuesForLostParcels()
    {
        var currentTime = _clock.LocalNow;
        var allStatuses = _queueManager.GetAllQueueStatuses();
        
        foreach (var (positionIndex, status) in allStatuses)
        {
            // 检查队列头部任务是否超过丢失判定截止时间
            var headTask = status.HeadTask;
            if (headTask == null)
                continue;
                
            // 如果任务没有设置丢失判定截止时间，跳过
            if (!headTask.LostDetectionDeadline.HasValue)
                continue;
            
            // 检查是否超过丢失判定截止时间
            if (currentTime > headTask.LostDetectionDeadline.Value)
            {
                var delayMs = (currentTime - headTask.ExpectedArrivalTime).TotalMilliseconds;
                var thresholdMs = headTask.LostDetectionTimeoutMs ?? 0;
                
                _logger.LogError(
                    "[主动检测] 包裹 {ParcelId} 在 Position {PositionIndex} 丢失 " +
                    "(延迟={DelayMs}ms, 丢失阈值={ThresholdMs}ms, 截止时间={Deadline:HH:mm:ss.fff})",
                    headTask.ParcelId,
                    positionIndex,
                    delayMs,
                    thresholdMs,
                    headTask.LostDetectionDeadline.Value);
                
                // 触发丢失事件
                await OnParcelLostDetectedAsync(headTask, currentTime, positionIndex);
            }
        }
    }

    private async Task OnParcelLostDetectedAsync(
        Core.Abstractions.Execution.PositionQueueItem lostTask,
        DateTime detectedAt,
        int positionIndex)
    {
        var delayMs = (detectedAt - lostTask.ExpectedArrivalTime).TotalMilliseconds;
        
        // 获取中位数间隔（用于统计）
        var medianIntervalMs = _intervalTracker?.GetStatistics(positionIndex)?.MedianIntervalMs;
        
        // 构建事件参数
        var eventArgs = new ParcelLostEventArgs
        {
            LostParcelId = lostTask.ParcelId,
            DetectedAtPositionIndex = positionIndex,
            DetectedAt = detectedAt,
            ParcelCreatedAt = lostTask.CreatedAt,
            ExpectedArrivalTime = lostTask.ExpectedArrivalTime,
            DelayMs = delayMs,
            TotalLifetimeMs = (detectedAt - lostTask.CreatedAt).TotalMilliseconds,
            TotalTasksRemoved = 0, // 将由处理器填充
            LostThresholdMs = lostTask.LostDetectionTimeoutMs ?? 0,
            MedianIntervalMs = medianIntervalMs,
            LostDetectionFactor = 1.5m // 从配置获取
        };
        
        // 触发事件
        ParcelLostDetected?.Invoke(this, eventArgs);
        
        await Task.CompletedTask;
    }
}
