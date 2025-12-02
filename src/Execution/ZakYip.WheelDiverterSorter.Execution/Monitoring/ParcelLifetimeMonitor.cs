using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Upstream;
using ZakYip.WheelDiverterSorter.Core.Enums.Sorting;
using ZakYip.WheelDiverterSorter.Core.Events.Sorting;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.Sorting.Tracking;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Observability.Utilities;

namespace ZakYip.WheelDiverterSorter.Execution.Monitoring;

/// <summary>
/// 包裹生命周期监控服务
/// </summary>
/// <remarks>
/// 作为 BackgroundService 运行，周期性扫描活跃包裹，判定超时和丢失状态。
/// 
/// <para><b>监控周期</b>：默认每 500ms 扫描一次（可配置）</para>
/// 
/// <para><b>超时判定规则</b>：</para>
/// <list type="bullet">
///   <item>分配超时：Status == Detected 且 now - DetectedAt &gt; DetectionToAssignmentTimeout</item>
///   <item>落格超时：Status == Assigned/Routing 且 AssignedAt != null 且 now - AssignedAt &gt; AssignmentToSortingTimeout</item>
/// </list>
/// 
/// <para><b>丢失判定规则</b>：</para>
/// <list type="bullet">
///   <item>Status != Sorted</item>
///   <item>SortedAt == null</item>
///   <item>now - DetectedAt &gt; MaxLifetimeBeforeLost</item>
/// </list>
/// 
/// <para><b>处理流程</b>：</para>
/// <list type="number">
///   <item>扫描活跃记录</item>
///   <item>判定超时/丢失</item>
///   <item>更新跟踪记录状态</item>
///   <item>触发事件通知</item>
///   <item>尝试路由到异常格口</item>
///   <item>向上游发送完成通知</item>
/// </list>
/// </remarks>
public sealed class ParcelLifetimeMonitor : BackgroundService
{
    private readonly IParcelTrackingService _trackingService;
    private readonly IUpstreamRoutingClient _upstreamClient;
    private readonly ISystemClock _clock;
    private readonly ISafeExecutionService _safeExecutor;
    private readonly ILogger<ParcelLifetimeMonitor> _logger;
    private readonly ParcelTimeoutOptions _timeoutOptions;
    private readonly long _exceptionChuteId;

    /// <summary>
    /// 监控扫描间隔（默认 500ms）
    /// </summary>
    private readonly TimeSpan _scanInterval = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// 包裹超时事件
    /// </summary>
    public event EventHandler<ParcelTimedOutEventArgs>? ParcelTimedOut;

    /// <summary>
    /// 包裹丢失事件
    /// </summary>
    public event EventHandler<ParcelLostEventArgs>? ParcelLost;

    /// <summary>
    /// 构造函数
    /// </summary>
    public ParcelLifetimeMonitor(
        IParcelTrackingService trackingService,
        IUpstreamRoutingClient upstreamClient,
        ISystemClock clock,
        ISafeExecutionService safeExecutor,
        IOptions<ParcelTimeoutOptions> timeoutOptions,
        IOptions<SystemConfiguration> systemConfig,
        ILogger<ParcelLifetimeMonitor> logger)
    {
        _trackingService = trackingService ?? throw new ArgumentNullException(nameof(trackingService));
        _upstreamClient = upstreamClient ?? throw new ArgumentNullException(nameof(upstreamClient));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _safeExecutor = safeExecutor ?? throw new ArgumentNullException(nameof(safeExecutor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeoutOptions = timeoutOptions?.Value ?? ParcelTimeoutOptions.GetDefault();
        _exceptionChuteId = systemConfig?.Value?.ExceptionChuteId ?? 999;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "包裹生命周期监控服务启动，扫描间隔: {ScanIntervalMs}ms，" +
            "分配超时: {DetectionTimeout}s，落格超时: {SortingTimeout}s，最大存活: {MaxLifetime}s",
            _scanInterval.TotalMilliseconds,
            _timeoutOptions.DetectionToAssignmentTimeout.TotalSeconds,
            _timeoutOptions.AssignmentToSortingTimeout.TotalSeconds,
            _timeoutOptions.MaxLifetimeBeforeLost.TotalSeconds);

        await _safeExecutor.ExecuteAsync(
            async () =>
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    await ScanAndProcessAsync(stoppingToken);
                    await Task.Delay(_scanInterval, stoppingToken);
                }
            },
            operationName: "ParcelLifetimeMonitorLoop",
            cancellationToken: stoppingToken);
    }

    private async Task ScanAndProcessAsync(CancellationToken cancellationToken)
    {
        var now = _clock.LocalNowOffset;
        var activeRecords = await _trackingService.GetActiveRecordsAsync(cancellationToken);

        foreach (var record in activeRecords)
        {
            // 先检查是否丢失（优先级高于超时）
            if (ShouldMarkAsLost(record, now))
            {
                await HandleLostAsync(record, now, cancellationToken);
                continue;
            }

            // 再检查是否超时
            if (ShouldMarkAsTimedOut(record, now))
            {
                await HandleTimeoutAsync(record, now, cancellationToken);
            }
        }
    }

    private bool ShouldMarkAsLost(ParcelTrackingRecord record, DateTimeOffset now)
    {
        // 已完成落格的不判定为丢失
        if (record.Status == ParcelLifecycleStatus.Sorted)
            return false;

        // 已丢失的不重复判定
        if (record.Status == ParcelLifecycleStatus.Lost)
            return false;

        // 超过最大存活时间且未落格
        return record.SortedAt == null &&
               now - record.DetectedAt > _timeoutOptions.MaxLifetimeBeforeLost;
    }

    private bool ShouldMarkAsTimedOut(ParcelTrackingRecord record, DateTimeOffset now)
    {
        // 已超时/已丢失/已完成的不重复判定
        if (record.Status is ParcelLifecycleStatus.TimedOut or
            ParcelLifecycleStatus.Lost or
            ParcelLifecycleStatus.Sorted)
            return false;

        // 分配超时：检测后未分配
        if (record.Status == ParcelLifecycleStatus.Detected &&
            record.AssignedAt == null &&
            now - record.DetectedAt > _timeoutOptions.DetectionToAssignmentTimeout)
        {
            return true;
        }

        // 落格超时：已分配但未落格
        if ((record.Status == ParcelLifecycleStatus.Assigned ||
             record.Status == ParcelLifecycleStatus.Routing) &&
            record.AssignedAt != null &&
            record.SortedAt == null &&
            now - record.AssignedAt.Value > _timeoutOptions.AssignmentToSortingTimeout)
        {
            return true;
        }

        return false;
    }

    private async Task HandleTimeoutAsync(
        ParcelTrackingRecord record,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        _logger.LogWarning(
            "包裹 {ParcelId} 超时，状态: {Status}，检测时间: {DetectedAt}",
            record.ParcelId,
            record.Status,
            record.DetectedAt);

        // 更新状态
        await _trackingService.UpdateTimedOutAsync(record.ParcelId, cancellationToken);

        // 触发事件
        var eventArgs = new ParcelTimedOutEventArgs
        {
            ParcelId = record.ParcelId,
            StatusBeforeTimeout = record.Status,
            DetectedAt = record.DetectedAt,
            TimeoutAt = now
        };
        OnParcelTimedOut(eventArgs);

        // 发送上游通知
        await NotifyUpstreamAsync(
            record.ParcelId,
            _exceptionChuteId,
            SortingOutcome.Timeout,
            "包裹超时，路由到异常格口",
            now,
            cancellationToken);
    }

    private async Task HandleLostAsync(
        ParcelTrackingRecord record,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        _logger.LogError(
            "包裹 {ParcelId} 丢失，检测时间: {DetectedAt}，最后确认时间: {LastSeenAt}",
            record.ParcelId,
            record.DetectedAt,
            record.LastSeenAt);

        // 更新状态
        await _trackingService.UpdateLostAsync(record.ParcelId, cancellationToken);

        // 触发事件
        var eventArgs = new ParcelLostEventArgs
        {
            ParcelId = record.ParcelId,
            DetectedAt = record.DetectedAt,
            LostAt = now,
            LastSeenAt = record.LastSeenAt
        };
        OnParcelLost(eventArgs);

        // 发送上游通知
        await NotifyUpstreamAsync(
            record.ParcelId,
            _exceptionChuteId,
            SortingOutcome.Lost,
            "包裹丢失",
            now,
            cancellationToken);
    }

    private async Task NotifyUpstreamAsync(
        long parcelId,
        long chuteId,
        SortingOutcome outcome,
        string? failureReason,
        DateTimeOffset completedAt,
        CancellationToken cancellationToken)
    {
        var notification = new SortingCompletedNotification
        {
            ParcelId = parcelId,
            ActualChuteId = chuteId,
            CompletedAt = completedAt,
            IsSuccess = outcome == SortingOutcome.Success,
            Outcome = outcome,
            FailureReason = failureReason
        };

        try
        {
            await _upstreamClient.NotifySortingCompletedAsync(notification, cancellationToken);
        }
        catch (Exception ex)
        {
            // 发送失败只记录日志，不重试
            _logger.LogWarning(
                ex,
                "向上游发送分拣完成通知失败，包裹: {ParcelId}，结果: {Outcome}",
                parcelId,
                outcome);
        }
    }

    private void OnParcelTimedOut(ParcelTimedOutEventArgs args)
    {
        ParcelTimedOut?.Invoke(this, args);
    }

    private void OnParcelLost(ParcelLostEventArgs args)
    {
        ParcelLost?.Invoke(this, args);
    }
}
