using System.Collections.Concurrent;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Core.Events.Queue;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;
using ZakYip.WheelDiverterSorter.Execution.Queues;
using ZakYip.WheelDiverterSorter.Execution.Tracking;
using ZakYip.WheelDiverterSorter.Observability.Utilities;

namespace ZakYip.WheelDiverterSorter.Execution.Monitoring;

/// <summary>
/// 包裹丢失主动监控服务
/// </summary>
/// <remarks>
/// 定期扫描队列中的任务，检测是否有包裹超过丢失判定截止时间仍未到达。
/// 这是主动检测机制，用于发现那些永远不会到达传感器的丢失包裹。
/// 
/// 优先级规则：包裹丢失的严重性大于包裹超时。
/// 如果一个包裹同时满足丢失和超时条件，将按丢失处理。
/// </remarks>
public class ParcelLossMonitoringService : BackgroundService
{
    private readonly IPositionIndexQueueManager _queueManager;
    private readonly IPositionIntervalTracker? _intervalTracker;
    private readonly IParcelLossDetectionConfigurationRepository? _configRepository;
    private readonly ISystemClock _clock;
    private readonly ILogger<ParcelLossMonitoringService> _logger;
    private readonly ISafeExecutionService _safeExecutor;
    private readonly PositionIntervalTrackerOptions _trackerOptions;
    
    // 已报告丢失的包裹集合（防止重复日志）
    private readonly ConcurrentDictionary<long, DateTime> _reportedLostParcels = new();
    
    /// <summary>
    /// 包裹丢失事件
    /// </summary>
    public event EventHandler<ParcelLostEventArgs>? ParcelLostDetected;

    public ParcelLossMonitoringService(
        IPositionIndexQueueManager queueManager,
        ISystemClock clock,
        ILogger<ParcelLossMonitoringService> logger,
        ISafeExecutionService safeExecutor,
        IOptions<PositionIntervalTrackerOptions> trackerOptions,
        IPositionIntervalTracker? intervalTracker = null,
        IParcelLossDetectionConfigurationRepository? configRepository = null)
    {
        _queueManager = queueManager ?? throw new ArgumentNullException(nameof(queueManager));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _safeExecutor = safeExecutor ?? throw new ArgumentNullException(nameof(safeExecutor));
        _trackerOptions = trackerOptions?.Value ?? new PositionIntervalTrackerOptions();
        _intervalTracker = intervalTracker;
        _configRepository = configRepository;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var monitoringIntervalMs = _trackerOptions.MonitoringIntervalMs;
        _logger.LogInformation("包裹丢失监控服务已启动，监控间隔: {IntervalMs}ms", monitoringIntervalMs);
        
        await _safeExecutor.ExecuteAsync(
            async () =>
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    await MonitorQueuesForLostParcels();
                    await Task.Delay(monitoringIntervalMs, stoppingToken);
                }
            },
            operationName: "ParcelLossMonitoring",
            cancellationToken: stoppingToken);
        
        _logger.LogInformation("包裹丢失监控服务已停止");
    }

    private async Task MonitorQueuesForLostParcels()
    {
        // 首先检查是否启用了包裹丢失检测
        // 如果配置仓储不存在或读取配置失败，默认禁用检测（安全策略）
        bool isEnabled = false;
        if (_configRepository != null)
        {
            try
            {
                var config = _configRepository.Get();
                isEnabled = config.IsEnabled;
                
                if (!isEnabled)
                {
                    // 检测被禁用，直接返回，不执行任何检测或自动清空逻辑
                    return;
                }
                
                // 检查是否应该自动清空中位数统计数据
                if (_intervalTracker != null && 
                    config.AutoClearMedianIntervalMs > 0 &&
                    _intervalTracker.ShouldAutoClear(config.AutoClearMedianIntervalMs))
                {
                    _logger.LogWarning(
                        "[自动清空中位数] 检测到超过 {IntervalMs}ms 未创建新包裹，正在清空所有中位数统计数据...",
                        config.AutoClearMedianIntervalMs);
                    
                    _intervalTracker.ClearAllStatistics();
                    
                    _logger.LogInformation(
                        "[自动清空中位数] 中位数统计数据已自动清空");
                }
                
                // 检查是否应该自动清空任务队列
                if (_intervalTracker != null && 
                    config.AutoClearQueueIntervalSeconds > 0 &&
                    _intervalTracker.ShouldAutoClear(config.AutoClearQueueIntervalSeconds * 1000))
                {
                    _logger.LogWarning(
                        "[自动清空队列] 检测到超过 {IntervalSeconds}秒 未创建新包裹，正在清空所有任务队列...",
                        config.AutoClearQueueIntervalSeconds);
                    
                    _queueManager.ClearAllQueues();
                    
                    _logger.LogInformation(
                        "[自动清空队列] 所有任务队列已自动清空");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "读取包裹丢失检测配置失败，已禁用包裹丢失检测");
                return; // 配置读取失败时禁用检测（安全策略）
            }
        }
        else
        {
            // 配置仓储不存在，禁用检测（安全策略）
            return;
        }
        
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
            
            // 检查是否已报告过该包裹丢失（防止重复日志）
            if (_reportedLostParcels.ContainsKey(headTask.ParcelId))
                continue;
            
            // 检查是否超过丢失判定截止时间
            if (currentTime > headTask.LostDetectionDeadline.Value)
            {
                // 标记为已报告（在日志之前，防止重复）
                _reportedLostParcels.TryAdd(headTask.ParcelId, currentTime);
                
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
                
                // 触发丢失事件（事件处理器会从队列移除该包裹的所有任务）
                await OnParcelLostDetectedAsync(headTask, currentTime, positionIndex);
            }
        }
        
        // 定期清理过期的已报告记录（保留最近1小时内的记录）
        CleanupExpiredReportedParcels(currentTime);
    }
    
    /// <summary>
    /// 清理过期的已报告包裹记录
    /// </summary>
    /// <remarks>
    /// 保留最近1小时内报告的记录，防止内存无限增长
    /// </remarks>
    private void CleanupExpiredReportedParcels(DateTime currentTime)
    {
        var expirationThreshold = currentTime.AddHours(-1);
        var expiredKeys = _reportedLostParcels
            .Where(kvp => kvp.Value < expirationThreshold)
            .Select(kvp => kvp.Key)
            .ToList();
        
        foreach (var key in expiredKeys)
        {
            _reportedLostParcels.TryRemove(key, out _);
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
        
        // 从配置获取丢失检测系数
        var lostDetectionFactor = _trackerOptions.LostDetectionMultiplier;
        
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
            LostDetectionFactor = (decimal)lostDetectionFactor // 从配置获取
        };
        
        // 触发事件
        ParcelLostDetected.SafeInvoke(this, eventArgs, _logger, nameof(ParcelLostDetected));
        
        await Task.CompletedTask;
    }
}
