using ZakYip.WheelDiverterSorter.Communication.Abstractions;
using ZakYip.WheelDiverterSorter.Communication;
using ZakYip.WheelDiverterSorter.Communication.Configuration;
using ZakYip.WheelDiverterSorter.Communication.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;
using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using ZakYip.WheelDiverterSorter.Core.LineModel.Topology;
using ZakYip.WheelDiverterSorter.Core.LineModel.Tracing;
using ZakYip.WheelDiverterSorter.Execution;
using ZakYip.WheelDiverterSorter.Execution.Health;
using ZakYip.WheelDiverterSorter.Ingress;
using ZakYip.WheelDiverterSorter.Ingress.Models;
using ZakYip.WheelDiverterSorter.Ingress.Services;
using ZakYip.WheelDiverterSorter.Observability;
using ZakYip.WheelDiverterSorter.Core.Sorting.Models;
using ZakYip.WheelDiverterSorter.Core.Sorting.Runtime;
using ZakYip.WheelDiverterSorter.Core.Sorting.Overload;
using Microsoft.Extensions.Options;

namespace ZakYip.WheelDiverterSorter.Host.Services;

/// <summary>
/// 包裹分拣编排服务
/// </summary>
/// <remarks>
/// 负责协调整个分拣流程：
/// 1. 监听传感器检测到的包裹
/// 2. 通知RuleEngine包裹到达
/// 3. 等待RuleEngine推送格口分配（带超时）
/// 4. 生成摆轮路径
/// 5. 执行分拣动作
/// </remarks>
public class ParcelSortingOrchestrator : IDisposable
{
    private readonly IParcelDetectionService _parcelDetectionService;
    private readonly IRuleEngineClient _ruleEngineClient;
    private readonly ISwitchingPathGenerator _pathGenerator;
    private readonly ISwitchingPathExecutor _pathExecutor;
    private readonly IPathFailureHandler? _pathFailureHandler;
    private readonly ILogger<ParcelSortingOrchestrator> _logger;
    private readonly RuleEngineConnectionOptions _options;
    private readonly ISystemConfigurationRepository _systemConfigRepository;
    private readonly ISystemRunStateService? _stateService;
    private readonly ICongestionDetector? _congestionDetector;
    private readonly IOverloadHandlingPolicy? _overloadPolicy;
    private readonly CongestionDataCollector? _congestionCollector;
    private readonly PrometheusMetrics? _metrics;
    private readonly IParcelTraceSink? _traceSink;
    private readonly PathHealthChecker? _pathHealthChecker;
    private readonly Dictionary<long, TaskCompletionSource<int>> _pendingAssignments;
    private readonly Dictionary<long, SwitchingPath> _parcelPaths;
    private readonly Dictionary<long, ParcelCreationRecord> _createdParcels; // PR-42: Track created parcels for Parcel-First validation
    private readonly object _lockObject = new object();
    private bool _isConnected;
    private int _roundRobinIndex = 0;

    /// <summary>
    /// PR-42: 包裹创建记录（用于 Parcel-First 语义验证）
    /// </summary>
    private class ParcelCreationRecord
    {
        public long ParcelId { get; init; }
        public DateTimeOffset CreatedAt { get; init; }
        public DateTimeOffset? UpstreamRequestSentAt { get; set; }
        public DateTimeOffset? UpstreamReplyReceivedAt { get; set; }
        public DateTimeOffset? RouteBoundAt { get; set; }
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    public ParcelSortingOrchestrator(
        IParcelDetectionService parcelDetectionService,
        IRuleEngineClient ruleEngineClient,
        ISwitchingPathGenerator pathGenerator,
        ISwitchingPathExecutor pathExecutor,
        IOptions<RuleEngineConnectionOptions> options,
        ISystemConfigurationRepository systemConfigRepository,
        ILogger<ParcelSortingOrchestrator> logger,
        IPathFailureHandler? pathFailureHandler = null,
        ISystemRunStateService? stateService = null,
        ICongestionDetector? congestionDetector = null,
        IOverloadHandlingPolicy? overloadPolicy = null,
        CongestionDataCollector? congestionCollector = null,
        PrometheusMetrics? metrics = null,
        IParcelTraceSink? traceSink = null,
        PathHealthChecker? pathHealthChecker = null)
    {
        _parcelDetectionService = parcelDetectionService ?? throw new ArgumentNullException(nameof(parcelDetectionService));
        _ruleEngineClient = ruleEngineClient ?? throw new ArgumentNullException(nameof(ruleEngineClient));
        _pathGenerator = pathGenerator ?? throw new ArgumentNullException(nameof(pathGenerator));
        _pathExecutor = pathExecutor ?? throw new ArgumentNullException(nameof(pathExecutor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _systemConfigRepository = systemConfigRepository ?? throw new ArgumentNullException(nameof(systemConfigRepository));
        _pathFailureHandler = pathFailureHandler;
        _stateService = stateService; // Optional: for state validation
        _congestionDetector = congestionDetector; // Optional: for congestion detection (PR-08B)
        _overloadPolicy = overloadPolicy; // Optional: for overload handling (PR-08B)
        _congestionCollector = congestionCollector; // Optional: for congestion data collection (PR-08B)
        _metrics = metrics; // Optional: for metrics collection (PR-08B)
        _traceSink = traceSink; // Optional: for parcel trace logging (PR-10)
        _pathHealthChecker = pathHealthChecker; // Optional: for node health checking (PR-14)
        _pendingAssignments = new Dictionary<long, TaskCompletionSource<int>>();
        _parcelPaths = new Dictionary<long, SwitchingPath>();
        _createdParcels = new Dictionary<long, ParcelCreationRecord>(); // PR-42: Track created parcels

        // 订阅包裹检测事件
        _parcelDetectionService.ParcelDetected += OnParcelDetected;
        
        // 订阅重复触发异常事件
        _parcelDetectionService.DuplicateTriggerDetected += OnDuplicateTriggerDetected;
        
        // 订阅格口分配事件
        _ruleEngineClient.ChuteAssignmentReceived += OnChuteAssignmentReceived;
    }

    /// <summary>
    /// 启动编排服务
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("正在启动包裹分拣编排服务...");

        // 连接到RuleEngine
        _isConnected = await _ruleEngineClient.ConnectAsync(cancellationToken);

        if (_isConnected)
        {
            _logger.LogInformation("成功连接到RuleEngine，包裹分拣编排服务已启动");
        }
        else
        {
            _logger.LogWarning("无法连接到RuleEngine，将在包裹检测时尝试重新连接");
        }
    }

    /// <summary>
    /// 停止编排服务
    /// </summary>
    public async Task StopAsync()
    {
        _logger.LogInformation("正在停止包裹分拣编排服务...");

        // 断开与RuleEngine的连接
        await _ruleEngineClient.DisconnectAsync();
        _isConnected = false;

        _logger.LogInformation("包裹分拣编排服务已停止");
    }

    /// <summary>
    /// 处理格口分配通知
    /// </summary>
    private void OnChuteAssignmentReceived(object? sender, ChuteAssignmentNotificationEventArgs e)
    {
        lock (_lockObject)
        {
            // PR-42: Invariant 2 - 上游响应必须匹配已存在的本地包裹
            if (!_createdParcels.ContainsKey(e.ParcelId))
            {
                _logger.LogError(
                    "[PR-42 Invariant Violation] 收到未知包裹 {ParcelId} 的路由响应 (ChuteId={ChuteId})，" +
                    "本地不存在此包裹实体。响应已丢弃，不创建幽灵包裹。",
                    e.ParcelId,
                    e.ChuteId);
                return; // 不处理未知包裹的路由响应
            }

            // 记录上游响应接收时间
            _createdParcels[e.ParcelId].UpstreamReplyReceivedAt = DateTimeOffset.UtcNow;
            
            if (_pendingAssignments.TryGetValue(e.ParcelId, out var tcs))
            {
                _logger.LogDebug("收到包裹 {ParcelId} 的格口分配: {ChuteId}", e.ParcelId, e.ChuteId);
                
                // 记录路由绑定时间
                _createdParcels[e.ParcelId].RouteBoundAt = DateTimeOffset.UtcNow;
                
                // PR-42: 记录路由绑定完成的 Trace 日志
                _logger.LogTrace(
                    "[PR-42 Parcel-First] 路由绑定完成: ParcelId={ParcelId}, ChuteId={ChuteId}, " +
                    "时间顺序: Created={CreatedAt:o} -> RequestSent={RequestAt:o} -> ReplyReceived={ReplyAt:o} -> RouteBound={BoundAt:o}",
                    e.ParcelId,
                    e.ChuteId,
                    _createdParcels[e.ParcelId].CreatedAt,
                    _createdParcels[e.ParcelId].UpstreamRequestSentAt,
                    _createdParcels[e.ParcelId].UpstreamReplyReceivedAt,
                    _createdParcels[e.ParcelId].RouteBoundAt);
                
                tcs.TrySetResult(e.ChuteId);
                _pendingAssignments.Remove(e.ParcelId);
            }
        }
    }

    /// <summary>
    /// 处理重复触发异常事件
    /// </summary>
    private async void OnDuplicateTriggerDetected(object? sender, ZakYip.WheelDiverterSorter.Ingress.Models.DuplicateTriggerEventArgs e)
    {
        var parcelId = e.ParcelId;
        _logger.LogWarning(
            "检测到重复触发异常: ParcelId={ParcelId}, 传感器={SensorId}, " +
            "距上次触发={TimeSinceLastMs}ms, 原因={Reason}",
            parcelId,
            e.SensorId,
            e.TimeSinceLastTriggerMs,
            e.Reason);

        try
        {
            // PR-42: Parcel-First - 本地创建包裹实体（即使是重复触发异常也需要创建实体）
            var createdAt = DateTimeOffset.UtcNow;
            lock (_lockObject)
            {
                _createdParcels[parcelId] = new ParcelCreationRecord
                {
                    ParcelId = parcelId,
                    CreatedAt = createdAt
                };
            }
            
            // PR-08B: 记录包裹进入系统
            _congestionCollector?.RecordParcelEntry(parcelId, DateTime.UtcNow);

            // 获取异常格口ID
            var systemConfig = _systemConfigRepository.Get();
            var exceptionChuteId = systemConfig.ExceptionChuteId;

            // 通知RuleEngine包裹重复触发异常
            var notificationSent = await _ruleEngineClient.NotifyParcelDetectedAsync(parcelId);
            
            if (!notificationSent)
            {
                _logger.LogError(
                    "包裹 {ParcelId} (重复触发异常) 无法发送检测通知到RuleEngine，将直接发送到异常格口",
                    parcelId);
            }

            // 直接将包裹发送到异常格口，不等待RuleEngine响应
            await ProcessSortingAsync(parcelId, exceptionChuteId, isOverloadException: false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理重复触发异常包裹 {ParcelId} 时发生错误", parcelId);
            
            // 清理包裹记录（PR-42）
            lock (_lockObject)
            {
                _createdParcels.Remove(parcelId);
            }
        }
    }

    /// <summary>
    /// 处理包裹检测事件
    /// </summary>
    private async void OnParcelDetected(object? sender, ParcelDetectedEventArgs e)
    {
        var parcelId = e.ParcelId;
        _logger.LogInformation("检测到包裹 {ParcelId}，开始处理分拣流程", parcelId);

        try
        {
            // PR-42: Parcel-First - 本地创建包裹实体并记录创建时间
            var createdAt = DateTimeOffset.UtcNow;
            lock (_lockObject)
            {
                _createdParcels[parcelId] = new ParcelCreationRecord
                {
                    ParcelId = parcelId,
                    CreatedAt = createdAt
                };
            }
            
            // PR-42: 记录本地包裹创建的 Trace 日志
            _logger.LogTrace(
                "[PR-42 Parcel-First] 本地创建包裹: ParcelId={ParcelId}, CreatedAt={CreatedAt:o}, 来源传感器={SensorId}",
                parcelId,
                createdAt,
                e.SensorId);

            // PR-10: 记录包裹创建事件
            await WriteTraceAsync(new ParcelTraceEventArgs
            {
                ItemId = parcelId,
                BarCode = null,
                OccurredAt = createdAt,
                Stage = "Created",
                Source = "Ingress",
                Details = $"传感器检测到包裹"
            });

            // 验证系统状态（只有运行状态才能创建包裹）
            if (_stateService != null)
            {
                var validationResult = _stateService.ValidateParcelCreation();
                if (!validationResult.IsSuccess)
                {
                    _logger.LogWarning(
                        "包裹 {ParcelId} 被拒绝：{ErrorMessage}",
                        parcelId,
                        validationResult.ErrorMessage);
                    
                    // 清理创建记录
                    lock (_lockObject)
                    {
                        _createdParcels.Remove(parcelId);
                    }
                    return;
                }
            }

            // PR-08B: 记录包裹进入系统
            _congestionCollector?.RecordParcelEntry(parcelId, DateTime.UtcNow);

            // 获取系统配置
            var systemConfig = _systemConfigRepository.Get();
            var exceptionChuteId = systemConfig.ExceptionChuteId;

            // PR-08B: 入口拥堵检测与超载判断
            if (_congestionDetector != null && _overloadPolicy != null && _congestionCollector != null)
            {
                // 收集当前拥堵快照
                var snapshot = _congestionCollector.CollectSnapshot();
                
                // 检测拥堵等级
                var congestionLevel = _congestionDetector.Detect(in snapshot);
                
                // 更新拥堵等级指标
                _metrics?.SetCongestionLevel((int)congestionLevel);
                _metrics?.SetInFlightParcels(snapshot.InFlightParcels);
                _metrics?.SetAverageLatency(snapshot.AverageLatencyMs);

                // 构造超载上下文
                var overloadContext = new OverloadContext
                {
                    ParcelId = parcelId.ToString(),
                    TargetChuteId = 0, // 尚未分配目标格口
                    CurrentLineSpeed = 1000m, // 1 m/s = 1000 mm/s，可从配置读取
                    CurrentPosition = "Entry",
                    EstimatedArrivalWindowMs = 10000, // 预估10秒窗口，实际应计算
                    CurrentCongestionLevel = congestionLevel,
                    RemainingTtlMs = 30000, // 预估30秒TTL，实际应计算
                    InFlightParcels = snapshot.InFlightParcels
                };

                // 评估是否需要超载处置
                var overloadDecision = _overloadPolicy.Evaluate(in overloadContext);

                if (overloadDecision.ShouldForceException)
                {
                    // 超载：直接路由到异常口
                    _logger.LogWarning(
                        "包裹 {ParcelId} 触发超载策略，直接发送到异常格口。原因：{Reason}，拥堵等级：{CongestionLevel}",
                        parcelId,
                        overloadDecision.Reason,
                        congestionLevel);

                    // PR-10: 记录超载决策事件
                    await WriteTraceAsync(new ParcelTraceEventArgs
                    {
                        ItemId = parcelId,
                        BarCode = null,
                        OccurredAt = DateTimeOffset.UtcNow,
                        Stage = "OverloadDecision",
                        Source = "OverloadPolicy",
                        Details = $"Reason=EntryOverload, CongestionLevel={congestionLevel}, Decision={overloadDecision.Reason}"
                    });

                    // 记录超载包裹指标
                    var reason = DetermineOverloadReason(overloadDecision.Reason);
                    _metrics?.RecordOverloadParcel(reason);

                    // 直接发送到异常格口
                    await ProcessSortingAsync(parcelId, exceptionChuteId, isOverloadException: true);
                    return;
                }

                if (overloadDecision.ShouldMarkAsOverflow)
                {
                    _logger.LogInformation(
                        "包裹 {ParcelId} 标记为潜在超载风险。原因：{Reason}",
                        parcelId,
                        overloadDecision.Reason);
                }
            }

            int? targetChuteId = null;

            // 根据分拣模式确定目标格口
            switch (systemConfig.SortingMode)
            {
                case SortingMode.Formal:
                    // 正式分拣模式：从RuleEngine获取格口
                    targetChuteId = await GetChuteFromRuleEngineAsync(parcelId, systemConfig);
                    break;

                case SortingMode.FixedChute:
                    // 指定落格分拣模式：使用固定格口
                    targetChuteId = systemConfig.FixedChuteId;
                    _logger.LogInformation(
                        "包裹 {ParcelId} 使用指定落格模式，目标格口 {ChuteId}",
                        parcelId,
                        targetChuteId);
                    break;

                case SortingMode.RoundRobin:
                    // 循环格口落格模式：按顺序使用格口
                    targetChuteId = GetNextRoundRobinChute(systemConfig);
                    _logger.LogInformation(
                        "包裹 {ParcelId} 使用循环落格模式，目标格口 {ChuteId}",
                        parcelId,
                        targetChuteId);
                    break;

                default:
                    _logger.LogError(
                        "未知的分拣模式 {SortingMode}，包裹 {ParcelId} 将发送到异常格口",
                        systemConfig.SortingMode,
                        parcelId);
                    targetChuteId = exceptionChuteId;
                    break;
            }

            // 如果没有获取到有效的目标格口，使用异常格口
            if (!targetChuteId.HasValue || targetChuteId.Value <= 0)
            {
                _logger.LogWarning(
                    "包裹 {ParcelId} 未能确定有效的目标格口，将发送到异常格口",
                    parcelId);
                targetChuteId = exceptionChuteId;
            }

            // 执行分拣
            await ProcessSortingAsync(parcelId, targetChuteId.Value, isOverloadException: false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理包裹 {ParcelId} 时发生异常", parcelId);
        }
    }

    /// <summary>
    /// 根据超载决策原因确定指标原因标签
    /// </summary>
    private string DetermineOverloadReason(string? decisionReason)
    {
        if (string.IsNullOrEmpty(decisionReason))
        {
            return "Unknown";
        }

        if (decisionReason.Contains("TTL") || decisionReason.Contains("超时") || decisionReason.Contains("Timeout"))
        {
            return "Timeout";
        }

        if (decisionReason.Contains("窗口") || decisionReason.Contains("Window"))
        {
            return "WindowMiss";
        }

        if (decisionReason.Contains("在途") || decisionReason.Contains("InFlight") || decisionReason.Contains("Capacity"))
        {
            return "CapacityExceeded";
        }

        if (decisionReason.Contains("拥堵") || decisionReason.Contains("Congestion"))
        {
            return "Congestion";
        }

        return "Other";
    }

    /// <summary>
    /// 执行分拣流程
    /// </summary>
    private async Task ProcessSortingAsync(long parcelId, int targetChuteId, bool isOverloadException = false)
    {
        try
        {
            // 获取系统配置
            var systemConfig = _systemConfigRepository.Get();
            var exceptionChuteId = systemConfig.ExceptionChuteId;

            // 生成摆轮路径
            var path = _pathGenerator.GeneratePath(targetChuteId);

            if (path == null)
            {
                _logger.LogWarning(
                    "包裹 {ParcelId} 无法生成到格口 {TargetChuteId} 的路径，将发送到异常格口",
                    parcelId,
                    targetChuteId);

                // 生成到异常格口的路径
                targetChuteId = exceptionChuteId;
                path = _pathGenerator.GeneratePath(targetChuteId);

                if (path == null)
                {
                    _logger.LogError("包裹 {ParcelId} 连异常格口路径都无法生成，分拣失败", parcelId);
                    _congestionCollector?.RecordParcelCompletion(parcelId, DateTime.UtcNow, false);
                    return;
                }
            }

            // PR-14: 节点健康检查 - 检查路径是否经过不健康的节点
            if (_pathHealthChecker != null && path != null && !isOverloadException)
            {
                var pathHealthResult = _pathHealthChecker.ValidatePath(path);
                
                if (!pathHealthResult.IsHealthy)
                {
                    var unhealthyNodeList = string.Join(", ", pathHealthResult.UnhealthyNodeIds);
                    
                    _logger.LogWarning(
                        "包裹 {ParcelId} 的路径经过不健康节点 [{UnhealthyNodes}]，将重定向到异常格口",
                        parcelId,
                        unhealthyNodeList);

                    // PR-10: 记录节点降级决策事件
                    await WriteTraceAsync(new ParcelTraceEventArgs
                    {
                        ItemId = parcelId,
                        BarCode = null,
                        OccurredAt = DateTimeOffset.UtcNow,
                        Stage = "OverloadDecision",
                        Source = "NodeHealthCheck",
                        Details = $"Reason=NodeDegraded, UnhealthyNodes=[{unhealthyNodeList}], OriginalTargetChute={targetChuteId}"
                    });

                    // 记录节点降级指标
                    _metrics?.RecordOverloadParcel("NodeDegraded");

                    // 重新生成到异常格口的路径
                    targetChuteId = exceptionChuteId;
                    path = _pathGenerator.GeneratePath(targetChuteId);

                    if (path == null)
                    {
                        _logger.LogError("包裹 {ParcelId} 连异常格口路径都无法生成（节点降级），分拣失败", parcelId);
                        _congestionCollector?.RecordParcelCompletion(parcelId, DateTime.UtcNow, false);
                        return;
                    }

                    // 标记为节点降级异常
                    isOverloadException = true;
                }
            }

            // PR-08C: 路径规划阶段的二次超载检查
            if (!isOverloadException && _overloadPolicy != null && _congestionCollector != null && path != null)
            {
                // 计算路径所需的总时间（毫秒）
                double totalRouteTimeMs = path.Segments.Sum(s => (double)s.TtlMilliseconds);

                // PR-10: 记录路径规划完成事件
                await WriteTraceAsync(new ParcelTraceEventArgs
                {
                    ItemId = parcelId,
                    BarCode = null,
                    OccurredAt = DateTimeOffset.UtcNow,
                    Stage = "RoutePlanned",
                    Source = "Execution",
                    Details = $"TargetChuteId={targetChuteId}, SegmentCount={path.Segments.Count}, EstimatedTimeMs={totalRouteTimeMs:F0}"
                });
                
                // 预估剩余时间（简化估算：假设30秒总TTL，减去已使用时间）
                const double EstimatedTotalTtlMs = 30000;
                double estimatedElapsedMs = 1000; // 假设包裹进入后已经过1秒
                double remainingTtlMs = EstimatedTotalTtlMs - estimatedElapsedMs;

                // 构造路由超载上下文
                var snapshot = _congestionCollector.CollectSnapshot();
                var congestionLevel = _congestionDetector?.Detect(in snapshot) ?? ZakYip.WheelDiverterSorter.Core.Sorting.Runtime.CongestionLevel.Normal;
                
                var routeOverloadContext = new OverloadContext
                {
                    ParcelId = parcelId.ToString(),
                    TargetChuteId = targetChuteId,
                    CurrentLineSpeed = 1000m, // 1 m/s = 1000 mm/s
                    CurrentPosition = "PathPlanning",
                    EstimatedArrivalWindowMs = remainingTtlMs,
                    CurrentCongestionLevel = congestionLevel,
                    RemainingTtlMs = remainingTtlMs,
                    InFlightParcels = snapshot.InFlightParcels
                };

                // 检查路径是否能在剩余时间内完成
                if (totalRouteTimeMs > remainingTtlMs)
                {
                    // 路由时间预算不足，调用超载策略
                    var routeDecision = _overloadPolicy.Evaluate(in routeOverloadContext);
                    
                    if (routeDecision.ShouldForceException)
                    {
                        _logger.LogWarning(
                            "包裹 {ParcelId} 路径规划阶段检测到超载：路径需要 {RequiredMs}ms，但剩余时间仅 {RemainingMs}ms，决策：{Decision}",
                            parcelId,
                            totalRouteTimeMs,
                            remainingTtlMs,
                            routeDecision.Reason);

                        // PR-10: 记录路由超载决策事件
                        await WriteTraceAsync(new ParcelTraceEventArgs
                        {
                            ItemId = parcelId,
                            BarCode = null,
                            OccurredAt = DateTimeOffset.UtcNow,
                            Stage = "OverloadDecision",
                            Source = "OverloadPolicy",
                            Details = $"Reason=RouteOverload, CongestionLevel={congestionLevel}, RequiredMs={totalRouteTimeMs:F0}, RemainingMs={remainingTtlMs:F0}"
                        });

                        // 记录路由超载指标
                        _metrics?.RecordOverloadParcel("RouteOverload");

                        // 重新生成到异常格口的路径
                        targetChuteId = exceptionChuteId;
                        path = _pathGenerator.GeneratePath(targetChuteId);

                        if (path == null)
                        {
                            _logger.LogError("包裹 {ParcelId} 连异常格口路径都无法生成，分拣失败", parcelId);
                            _congestionCollector?.RecordParcelCompletion(parcelId, DateTime.UtcNow, false);
                            return;
                        }

                        // 标记为超载异常
                        isOverloadException = true;
                    }
                }
            }

            // 最终校验：确保路径不为空
            if (path == null)
            {
                _logger.LogError("包裹 {ParcelId} 路径为空，无法执行分拣", parcelId);
                _congestionCollector?.RecordParcelCompletion(parcelId, DateTime.UtcNow, false);
                return;
            }

            // 记录包裹路径，用于失败处理
            lock (_lockObject)
            {
                _parcelPaths[parcelId] = path;
            }

            // 执行摆轮路径
            var executionResult = await _pathExecutor.ExecuteAsync(path);

            if (executionResult.IsSuccess)
            {
                _logger.LogInformation(
                    "包裹 {ParcelId} 成功分拣到格口 {ActualChuteId}{OverloadFlag}",
                    parcelId,
                    executionResult.ActualChuteId,
                    isOverloadException ? " [超载异常]" : "");

                // PR-10: 记录成功落格事件
                if (isOverloadException)
                {
                    await WriteTraceAsync(new ParcelTraceEventArgs
                    {
                        ItemId = parcelId,
                        BarCode = null,
                        OccurredAt = DateTimeOffset.UtcNow,
                        Stage = "ExceptionDiverted",
                        Source = "Execution",
                        Details = $"ChuteId={executionResult.ActualChuteId}, Reason=Overload"
                    });
                }
                else
                {
                    await WriteTraceAsync(new ParcelTraceEventArgs
                    {
                        ItemId = parcelId,
                        BarCode = null,
                        OccurredAt = DateTimeOffset.UtcNow,
                        Stage = "Diverted",
                        Source = "Execution",
                        Details = $"ChuteId={executionResult.ActualChuteId}, TargetChuteId={targetChuteId}"
                    });
                }
                
                // PR-08B: 记录包裹完成
                _congestionCollector?.RecordParcelCompletion(parcelId, DateTime.UtcNow, true);
            }
            else
            {
                _logger.LogError(
                    "包裹 {ParcelId} 分拣失败: {FailureReason}，实际到达格口: {ActualChuteId}",
                    parcelId,
                    executionResult.FailureReason,
                    executionResult.ActualChuteId);

                // PR-10: 记录异常落格事件
                await WriteTraceAsync(new ParcelTraceEventArgs
                {
                    ItemId = parcelId,
                    BarCode = null,
                    OccurredAt = DateTimeOffset.UtcNow,
                    Stage = "ExceptionDiverted",
                    Source = "Execution",
                    Details = $"ChuteId={executionResult.ActualChuteId}, Reason={executionResult.FailureReason}"
                });

                // PR-08B: 记录包裹完成（失败）
                _congestionCollector?.RecordParcelCompletion(parcelId, DateTime.UtcNow, false);

                // 处理路径执行失败
                if (_pathFailureHandler != null)
                {
                    _pathFailureHandler.HandlePathFailure(
                        parcelId,
                        path,
                        executionResult.FailureReason ?? "未知错误",
                        executionResult.FailedSegment);
                }
            }

            // 清理包裹路径记录和创建记录（PR-42）
            lock (_lockObject)
            {
                _parcelPaths.Remove(parcelId);
                _createdParcels.Remove(parcelId); // PR-42: 清理包裹创建记录
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "执行包裹 {ParcelId} 分拣时发生异常", parcelId);
            _congestionCollector?.RecordParcelCompletion(parcelId, DateTime.UtcNow, false);
            
            // 清理包裹记录（PR-42）
            lock (_lockObject)
            {
                _createdParcels.Remove(parcelId);
            }
        }
    }

    /// <summary>
    /// 从RuleEngine获取格口分配（正式分拣模式）
    /// </summary>
    private async Task<int?> GetChuteFromRuleEngineAsync(long parcelId, SystemConfiguration systemConfig)
    {
        var exceptionChuteId = systemConfig.ExceptionChuteId;

        // PR-42: Invariant 1 - 上游请求必须引用已存在的本地包裹
        lock (_lockObject)
        {
            if (!_createdParcels.ContainsKey(parcelId))
            {
                _logger.LogError(
                    "[PR-42 Invariant Violation] 尝试为不存在的包裹 {ParcelId} 发送上游请求。" +
                    "请求已阻止，不发送到上游。包裹将被路由到异常格口。",
                    parcelId);
                return exceptionChuteId;
            }
        }

        // 步骤1: 通知RuleEngine包裹到达
        var upstreamRequestSentAt = DateTimeOffset.UtcNow;
        
        // 记录上游请求发送时间（PR-42）
        lock (_lockObject)
        {
            if (_createdParcels.ContainsKey(parcelId))
            {
                _createdParcels[parcelId].UpstreamRequestSentAt = upstreamRequestSentAt;
            }
        }
        
        // PR-42: 记录发送上游路由请求的 Trace 日志
        _logger.LogTrace(
            "[PR-42 Parcel-First] 发送上游路由请求: ParcelId={ParcelId}, RequestSentAt={SentAt:o}",
            parcelId,
            upstreamRequestSentAt);
        
        var notificationSent = await _ruleEngineClient.NotifyParcelDetectedAsync(parcelId);
        
        if (!notificationSent)
        {
            _logger.LogError(
                "包裹 {ParcelId} 无法发送检测通知到RuleEngine，将返回异常格口",
                parcelId);
            return exceptionChuteId;
        }

        // 步骤2: 等待RuleEngine推送格口分配（带超时）
        int? targetChuteId = null;
        var tcs = new TaskCompletionSource<int>();
        
        lock (_lockObject)
        {
            _pendingAssignments[parcelId] = tcs;
        }

        try
        {
            // 使用系统配置中的超时时间
            var timeoutMs = systemConfig.ChuteAssignmentTimeoutMs;
            var startTime = DateTime.UtcNow;
            using var cts = new CancellationTokenSource(timeoutMs);
            targetChuteId = await tcs.Task.WaitAsync(cts.Token);
            var elapsedMs = (DateTime.UtcNow - startTime).TotalMilliseconds;
            
            _logger.LogInformation("包裹 {ParcelId} 从RuleEngine分配到格口 {ChuteId}", parcelId, targetChuteId);

            // PR-10: 记录上游分配事件
            await WriteTraceAsync(new ParcelTraceEventArgs
            {
                ItemId = parcelId,
                BarCode = null,
                OccurredAt = DateTimeOffset.UtcNow,
                Stage = "UpstreamAssigned",
                Source = "Upstream",
                Details = $"ChuteId={targetChuteId}, LatencyMs={elapsedMs:F0}, Status=Success"
            });

            return targetChuteId;
        }
        catch (TimeoutException)
        {
            _logger.LogWarning(
                "包裹 {ParcelId} 等待格口分配超时（{TimeoutMs}ms），将返回异常格口",
                parcelId,
                systemConfig.ChuteAssignmentTimeoutMs);
            return exceptionChuteId;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning(
                "包裹 {ParcelId} 等待格口分配超时（{TimeoutMs}ms），将返回异常格口",
                parcelId,
                systemConfig.ChuteAssignmentTimeoutMs);
            return exceptionChuteId;
        }
        finally
        {
            // 清理待处理的分配
            lock (_lockObject)
            {
                _pendingAssignments.Remove(parcelId);
            }
        }
    }

    /// <summary>
    /// 获取下一个循环格口（循环格口落格模式）
    /// </summary>
    private int GetNextRoundRobinChute(SystemConfiguration systemConfig)
    {
        lock (_lockObject)
        {
            if (systemConfig.AvailableChuteIds == null || systemConfig.AvailableChuteIds.Count == 0)
            {
                _logger.LogError("循环格口落格模式配置错误：没有可用格口，将使用异常格口");
                return systemConfig.ExceptionChuteId;
            }

            // 获取当前索引的格口
            var chuteId = systemConfig.AvailableChuteIds[_roundRobinIndex];

            // 移动到下一个索引（循环）
            _roundRobinIndex = (_roundRobinIndex + 1) % systemConfig.AvailableChuteIds.Count;

            return chuteId;
        }
    }

    /// <summary>
    /// 写入包裹追踪日志
    /// </summary>
    private async ValueTask WriteTraceAsync(ParcelTraceEventArgs eventArgs)
    {
        if (_traceSink != null)
        {
            await _traceSink.WriteAsync(eventArgs);
        }
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        // 取消订阅事件
        _parcelDetectionService.ParcelDetected -= OnParcelDetected;
        _parcelDetectionService.DuplicateTriggerDetected -= OnDuplicateTriggerDetected;
        _ruleEngineClient.ChuteAssignmentReceived -= OnChuteAssignmentReceived;

        // 断开连接
        StopAsync().GetAwaiter().GetResult();
    }
}
