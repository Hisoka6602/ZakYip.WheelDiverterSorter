using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Upstream;
using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using ZakYip.WheelDiverterSorter.Core.Sorting.Strategy;
using ZakYip.WheelDiverterSorter.Core.Utilities;

namespace ZakYip.WheelDiverterSorter.Execution.Strategy;

/// <summary>
/// 正式分拣格口选择策略
/// </summary>
/// <remarks>
/// 正式分拣模式：由上游 RuleEngine 分配格口。
/// 发送包裹检测通知给上游，等待上游推送格口分配。
/// 超时后自动兜底到异常格口。
/// 
/// <para><b>事件处理</b>：</para>
/// 此策略内部订阅 IUpstreamRoutingClient.ChuteAssignmentReceived 事件。
/// 如果调用方（如 SortingOrchestrator）需要在事件处理前进行验证，
/// 可以使用 NotifyChuteAssignment 方法代替自动事件订阅。
/// </remarks>
public class FormalChuteSelectionStrategy : IChuteSelectionStrategy, IDisposable
{
    private readonly IUpstreamRoutingClient _upstreamClient;
    private readonly IChuteAssignmentTimeoutCalculator? _timeoutCalculator;
    private readonly ISystemClock _clock;
    private readonly ILogger<FormalChuteSelectionStrategy> _logger;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<long, TaskCompletionSource<long>> _pendingAssignments;
    private readonly bool _subscribeToEvents;

    /// <summary>
    /// 默认超时时间（毫秒）
    /// </summary>
    private const int DefaultFallbackTimeoutMs = 5000;

    /// <summary>
    /// 创建正式分拣格口选择策略
    /// </summary>
    /// <param name="upstreamClient">上游路由客户端</param>
    /// <param name="clock">系统时钟</param>
    /// <param name="logger">日志记录器</param>
    /// <param name="timeoutCalculator">超时计算器（可选）</param>
    /// <param name="subscribeToEvents">是否自动订阅上游事件（默认 true）</param>
    /// <remarks>
    /// 当 subscribeToEvents 为 false 时，调用方需要通过 NotifyChuteAssignment 方法手动通知格口分配。
    /// 这允许调用方在分配前进行额外验证（如 PR-42 Parcel-First 验证）。
    /// </remarks>
    public FormalChuteSelectionStrategy(
        IUpstreamRoutingClient upstreamClient,
        ISystemClock clock,
        ILogger<FormalChuteSelectionStrategy> logger,
        IChuteAssignmentTimeoutCalculator? timeoutCalculator = null,
        bool subscribeToEvents = true)
    {
        _upstreamClient = upstreamClient ?? throw new ArgumentNullException(nameof(upstreamClient));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeoutCalculator = timeoutCalculator;
        _subscribeToEvents = subscribeToEvents;
        _pendingAssignments = new System.Collections.Concurrent.ConcurrentDictionary<long, TaskCompletionSource<long>>();

        // 仅在需要时订阅格口分配事件
        // PR-UPSTREAM02: 从 ChuteAssignmentReceived 改为 ChuteAssigned
        if (_subscribeToEvents)
        {
            _upstreamClient.ChuteAssigned += OnChuteAssignmentReceived;
        }
    }

    /// <inheritdoc />
    public async Task<ChuteSelectionResult> SelectChuteAsync(SortingContext context, CancellationToken cancellationToken)
    {
        // 超载策略相关代码已删除

        // 检查上游连接状态
        if (!_upstreamClient.IsConnected)
        {
            _logger.LogWarning(
                "包裹 {ParcelId} 上游系统未连接，将使用异常格口 {ExceptionChuteId}",
                context.ParcelId,
                context.ExceptionChuteId);

            return ChuteSelectionResult.Exception(
                context.ExceptionChuteId,
                "上游系统未连接");
        }

        // PR-fix-upstream-notification: 上游通知已在 DetermineTargetChuteAsync 中统一发送
        // 此处不再重复发送，避免双重通知
        _logger.LogDebug(
            "包裹 {ParcelId} 上游通知已在 DetermineTargetChuteAsync 中发送，FormalChuteSelectionStrategy 不再重复发送",
            context.ParcelId);

        // 等待上游推送格口分配
        var tcs = new TaskCompletionSource<long>();
        _pendingAssignments[context.ParcelId] = tcs;

        try
        {
            // 计算超时时间
            var timeoutSeconds = CalculateTimeout(context);
            var timeoutMs = (int)(timeoutSeconds * 1000);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeoutMs);

            var startTime = _clock.LocalNow;
            var targetChuteId = await tcs.Task.WaitAsync(cts.Token);
            var elapsedMs = (_clock.LocalNow - startTime).TotalMilliseconds;

            _logger.LogInformation(
                "包裹 {ParcelId} 从上游系统分配到格口 {ChuteId}（耗时 {ElapsedMs:F0}ms，超时限制 {TimeoutMs}ms）",
                context.ParcelId,
                targetChuteId,
                elapsedMs,
                timeoutMs);

            return ChuteSelectionResult.Success(targetChuteId);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // 超时（非外部取消）
            var timeoutSeconds = CalculateTimeout(context);
            var timeoutMs = (int)(timeoutSeconds * 1000);

            _logger.LogWarning(
                "【路由超时兜底】包裹 {ParcelId} 等待格口分配超时（超时限制：{TimeoutMs}ms），将使用异常格口 {ExceptionChuteId}",
                context.ParcelId,
                timeoutMs,
                context.ExceptionChuteId);

            return ChuteSelectionResult.Exception(
                context.ExceptionChuteId,
                $"等待格口分配超时（{timeoutMs}ms）");
        }
        catch (OperationCanceledException)
        {
            // 外部取消
            _logger.LogInformation(
                "包裹 {ParcelId} 格口选择被取消，将使用异常格口 {ExceptionChuteId}",
                context.ParcelId,
                context.ExceptionChuteId);

            return ChuteSelectionResult.Exception(
                context.ExceptionChuteId,
                "格口选择操作被取消");
        }
        finally
        {
            _pendingAssignments.TryRemove(context.ParcelId, out _);
        }
    }

    /// <summary>
    /// 计算超时时间（秒）
    /// </summary>
    private decimal CalculateTimeout(SortingContext context)
    {
        // 如果有超时计算器，使用动态计算
        if (_timeoutCalculator != null)
        {
            var timeoutContext = new ChuteAssignmentTimeoutContext(
                LineId: 1, // TD-042: 支持多线时从上下文获取
                SafetyFactor: 0.9m
            );

            return _timeoutCalculator.CalculateTimeoutSeconds(timeoutContext);
        }

        // 降级：使用默认值（转换为秒）
        return DefaultFallbackTimeoutMs / 1000m;
    }

    /// <summary>
    /// 处理格口分配通知
    /// </summary>
    private void OnChuteAssignmentReceived(object? sender, ChuteAssignmentEventArgs e)
    {
        NotifyChuteAssignment(e.ParcelId, e.ChuteId);
    }

    /// <summary>
    /// 通知格口分配
    /// </summary>
    /// <param name="parcelId">包裹ID</param>
    /// <param name="chuteId">分配的格口ID</param>
    /// <returns>是否有待处理的分配被完成</returns>
    /// <remarks>
    /// 当 subscribeToEvents 为 false 时，调用方应使用此方法通知策略格口分配结果。
    /// 这允许调用方在分配前进行额外验证（如 PR-42 Parcel-First 验证）。
    /// </remarks>
    public bool NotifyChuteAssignment(long parcelId, long chuteId)
    {
        if (_pendingAssignments.TryGetValue(parcelId, out var tcs))
        {
            _logger.LogDebug(
                "收到包裹 {ParcelId} 的格口分配: {ChuteId}",
                parcelId,
                chuteId);

            tcs.TrySetResult(chuteId);
            _pendingAssignments.TryRemove(parcelId, out _);
            return true;
        }
        else
        {
            // 迟到的响应：包裹已经超时并被路由到异常口
            _logger.LogInformation(
                "【迟到路由响应】收到包裹 {ParcelId} 的格口分配 (ChuteId={ChuteId})，" +
                "但该包裹已因超时被路由到异常口，不再改变去向。",
                parcelId,
                chuteId);
            return false;
        }
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        // PR-UPSTREAM02: 从 ChuteAssignmentReceived 改为 ChuteAssigned
        if (_subscribeToEvents)
        {
            _upstreamClient.ChuteAssigned -= OnChuteAssignmentReceived;
        }

        // 取消所有待处理的分配
        foreach (var kvp in _pendingAssignments)
        {
            kvp.Value.TrySetCanceled();
        }
        _pendingAssignments.Clear();
    }
}
