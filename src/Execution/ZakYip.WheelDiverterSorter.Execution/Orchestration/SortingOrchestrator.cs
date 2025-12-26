using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Observability;
using ZakYip.WheelDiverterSorter.Core.LineModel;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Execution.Health;
using ZakYip.WheelDiverterSorter.Execution.Queues;
using ZakYip.WheelDiverterSorter.Core.Enums.System;
using ZakYip.WheelDiverterSorter.Core.Events.Sensor;
using ZakYip.WheelDiverterSorter.Core.Enums.Sorting;
using ZakYip.WheelDiverterSorter.Core.Events.Sorting;
using ZakYip.WheelDiverterSorter.Core.Sorting.Models;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Sorting.Runtime;
using ZakYip.WheelDiverterSorter.Core.Sorting.Overload;
using ZakYip.WheelDiverterSorter.Core.Sorting.Policies;
using ZakYip.WheelDiverterSorter.Core.Sorting.Strategy;
using ZakYip.WheelDiverterSorter.Core.Enums.Monitoring;
using ZakYip.WheelDiverterSorter.Core.Events.Monitoring;
using ZakYip.WheelDiverterSorter.Core.LineModel.Tracing;
using ZakYip.WheelDiverterSorter.Core.LineModel.Routing;
using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using ZakYip.WheelDiverterSorter.Core.LineModel.Topology;
using ZakYip.WheelDiverterSorter.Core.Sorting.Interfaces;
using ZakYip.WheelDiverterSorter.Execution.PathExecution;
using ZakYip.WheelDiverterSorter.Observability.Utilities;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Ingress;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Upstream;
using ZakYip.WheelDiverterSorter.Core.Sorting.Orchestration;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Execution;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;

namespace ZakYip.WheelDiverterSorter.Execution.Orchestration;

/// <summary>
/// 分拣编排服务实现
/// </summary>
/// <remarks>
/// Execution 层的核心服务，协调整个分拣流程。
///
/// <para><b>架构职责</b>：</para>
/// <list type="bullet">
///   <item>不直接访问硬件驱动（通过 Execution 层抽象）</item>
///   <item>不包含 HTTP 路由逻辑（由 Host 层 Controller 处理）</item>
///   <item>不直接依赖 Communication/Ingress 层（通过抽象接口访问）</item>
///   <item>专注于业务流程编排和步骤协调</item>
///   <item>将长流程拆分为多个小方法，保持可测试性</item>
/// </list>
/// </remarks>
public class SortingOrchestrator : ISortingOrchestrator, IDisposable
{
    // 性能估算常量 - 用于超载检测和路径规划
    private const decimal DefaultLineSpeedMmps = 1000m; // 1 m/s = 1000 mm/s

    private const double EstimatedTotalTtlMs = 30000; // 预估30秒TTL
    private const double EstimatedArrivalWindowMs = 10000; // 预估10秒窗口
    private const double EstimatedElapsedMs = 1000; // 假设包裹进入后已经过1秒

    // TD-062: 拓扑驱动分拣流程常量
    private const int DefaultTimeoutSeconds = 10; // 默认超时时间（秒）

    /// <summary>
    /// 单个摆轮动作执行超时时间（毫秒）
    /// </summary>
    /// <remarks>
    /// 用于 IO 触发执行时单段摆轮动作的超时设置。
    /// 5000ms 作为保守默认值，覆盖大多数摆轮动作场景。
    /// </remarks>
    private const int DefaultSingleActionTimeoutMs = 5000;

    // 空的可用格口列表（静态共享实例）
    private static readonly IReadOnlyList<long> EmptyAvailableChuteIds = Array.Empty<long>();

    private readonly ISensorEventProvider _sensorEventProvider;
    private readonly IUpstreamRoutingClient _upstreamClient;
    private readonly ISwitchingPathGenerator _pathGenerator;
    private readonly ISwitchingPathExecutor _pathExecutor;
    private readonly IPathFailureHandler? _pathFailureHandler;
    private readonly ISystemClock _clock;
    private readonly ILogger<SortingOrchestrator> _logger;
    private readonly UpstreamConnectionOptions _options;
    private readonly ISystemConfigurationRepository _systemConfigRepository;
    private readonly ISystemStateManager _systemStateManager; // 必需：用于状态验证
    private readonly ICongestionDetector? _congestionDetector;
    private readonly ICongestionDataCollector? _congestionCollector;
    private readonly IParcelTraceSink? _traceSink;
    private readonly PathHealthChecker? _pathHealthChecker;
    private readonly IChuteAssignmentTimeoutCalculator? _timeoutCalculator;
    private readonly ISortingExceptionHandler _exceptionHandler;
    private readonly IChuteSelectionService? _chuteSelectionService;
    private readonly IChuteDropoffCallbackConfigurationRepository? _callbackConfigRepository;
    private readonly IRoutePlanRepository? _routePlanRepository;
    private readonly object? _upstreamServer; // 服务端模式（可选，类型为 IRuleEngineServer）

    // 新的 Position-Index 队列系统依赖
    private readonly IPositionIndexQueueManager? _queueManager;

    private readonly IChutePathTopologyRepository? _topologyRepository;
    private readonly IConveyorSegmentRepository? _segmentRepository;
    private readonly ISensorConfigurationRepository? _sensorConfigRepository;
    private readonly ISafeExecutionService? _safeExecutor;
    private readonly Tracking.IPositionIntervalTracker? _intervalTracker;
    private readonly Monitoring.ParcelLossMonitoringService? _lossMonitoringService;
    private readonly AlarmService? _alarmService; // 告警服务（用于失败率统计）
    private readonly ISortingStatisticsService? _statisticsService; // 分拣统计服务

    // 包裹路由相关的状态 - 使用线程安全集合 (PR-44)
    private readonly ConcurrentDictionary<long, TaskCompletionSource<long>> _pendingAssignments;

    private readonly ConcurrentDictionary<long, SwitchingPath> _parcelPaths;
    private readonly ConcurrentDictionary<long, ParcelCreationRecord> _createdParcels; // PR-42: Track created parcels
    private readonly ConcurrentDictionary<long, long> _parcelTargetChutes; // Track target chute for each parcel (for Position-Index queue system)
    private readonly ConcurrentDictionary<long, byte> _timeoutCompensationInserted; // 记录已插入超时补偿任务的包裹ID，防止重复插入（使用byte占位）
    
    // Issue #2: 缓存传感器到位置的映射，避免重复查询（热路径优化）
    private readonly ConcurrentDictionary<long, (long DiverterId, int PositionIndex)> _sensorToPositionCache;
    
    // Issue #3: 缓存每个position后续节点列表，避免超时时重复构造
    private readonly ConcurrentDictionary<int, List<DiverterPathNode>> _subsequentNodesCache;
    
    /// <summary>
    /// 包裹条码缓存，用于在日志中附加来自上游 DWS 的条码信息以便追踪。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 条码的写入时机：在 <c>OnChuteAssignmentReceived</c> 中，当上游消息的
    /// <c>DwsPayload.Barcode</c> 字段存在且非空时，将对应包裹 ID 与条码写入缓存。
    /// </para>
    /// <para>
    /// 条码的清理时机：在以下五类清理路径中移除缓存项，以避免内存泄漏并保证状态一致：
    /// 1) 路由/分拣超时清理；2) 正常分拣完成后的清理；3) 包裹丢失（lost）处理时的清理；
    /// 4) 显式的包裹清理操作；5) <c>CleanupParcelMemory</c> 中统一回收包裹相关内存时的清理。
    /// </para>
    /// <para>
    /// 线程安全：使用 <see cref="ConcurrentDictionary{TKey,TValue}"/> 存储条码，
    /// 支持多线程并发读写而无需额外锁，符合本类在多线程回调与后台任务中的并发访问需求。
    /// </para>
    /// <para>
    /// 日志格式：为了便于关联，同一包裹的日志中会在消息末尾附加
    /// <c>",BarCode:{value}"</c> 形式的后缀，其中 <c>{value}</c> 为缓存中的条码值。
    /// </para>
    /// </remarks>
    private readonly ConcurrentDictionary<long, string> _parcelBarcodes;
    
    private readonly object _lockObject = new object(); // 保留用于 RoundRobin 索引和连接状态
    private int _roundRobinIndex = 0;

    /// <summary>
    /// 包裹创建事件 - 当通过IO检测到包裹并在本地创建后触发
    /// </summary>
    public event EventHandler<ParcelCreatedEventArgs>? ParcelCreated;

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

    public SortingOrchestrator(
        ISensorEventProvider sensorEventProvider,
        IUpstreamRoutingClient upstreamClient,
        ISwitchingPathGenerator pathGenerator,
        ISwitchingPathExecutor pathExecutor,
        IOptions<UpstreamConnectionOptions> options,
        ISystemConfigurationRepository systemConfigRepository,
        ISystemClock clock,
        ILogger<SortingOrchestrator> logger,
        ISortingExceptionHandler exceptionHandler,
        ISystemStateManager systemStateManager, // 必需：用于状态验证
        IPathFailureHandler? pathFailureHandler = null,
        ICongestionDetector? congestionDetector = null,
        ICongestionDataCollector? congestionCollector = null,
        IParcelTraceSink? traceSink = null,
        PathHealthChecker? pathHealthChecker = null,
        IChuteAssignmentTimeoutCalculator? timeoutCalculator = null,
        IChuteSelectionService? chuteSelectionService = null,
        IPositionIndexQueueManager? queueManager = null, // 新的 Position-Index 队列管理器
        IChutePathTopologyRepository? topologyRepository = null, // TD-062: 拓扑配置仓储
        IConveyorSegmentRepository? segmentRepository = null, // TD-062: 线体段配置仓储
        ISensorConfigurationRepository? sensorConfigRepository = null, // TD-062: 传感器配置仓储
        ISafeExecutionService? safeExecutor = null, // TD-062: 安全执行服务
        Tracking.IPositionIntervalTracker? intervalTracker = null, // Position 间隔追踪器
        IChuteDropoffCallbackConfigurationRepository? callbackConfigRepository = null, // 落格回调配置仓储
        Monitoring.ParcelLossMonitoringService? lossMonitoringService = null, // 包裹丢失监控服务
        AlarmService? alarmService = null, // 告警服务（用于失败率统计）
        ISortingStatisticsService? statisticsService = null, // 分拣统计服务
        IRoutePlanRepository? routePlanRepository = null, // 路由计划仓储（用于保存格口分配）
        object? upstreamServer = null) // 上游服务端（服务端模式，可选）
    /// <remarks>
    /// upstreamServer 参数类型为 object 以避免 Execution 层直接引用 Communication 层（架构约束）。
    /// 实际运行时类型应为 IRuleEngineServer。使用反射订阅 ChuteAssigned 事件。
    /// 此架构约束是临时方案，未来应考虑：
    /// 1. 在 Core 层定义共享的事件接口
    /// 2. 使用适配器模式包装服务端为 IUpstreamRoutingClient
    /// 3. 使用消息总线解耦事件订阅
    /// </remarks>
    {
        _sensorEventProvider = sensorEventProvider ?? throw new ArgumentNullException(nameof(sensorEventProvider));
        _upstreamClient = upstreamClient ?? throw new ArgumentNullException(nameof(upstreamClient));
        _pathGenerator = pathGenerator ?? throw new ArgumentNullException(nameof(pathGenerator));
        _pathExecutor = pathExecutor ?? throw new ArgumentNullException(nameof(pathExecutor));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _systemConfigRepository = systemConfigRepository ?? throw new ArgumentNullException(nameof(systemConfigRepository));
        _exceptionHandler = exceptionHandler ?? throw new ArgumentNullException(nameof(exceptionHandler));
        _systemStateManager = systemStateManager ?? throw new ArgumentNullException(nameof(systemStateManager)); // 必需
        _pathFailureHandler = pathFailureHandler;
        _congestionDetector = congestionDetector;
        _congestionCollector = congestionCollector;
        _traceSink = traceSink;
        _pathHealthChecker = pathHealthChecker;
        _timeoutCalculator = timeoutCalculator;
        _chuteSelectionService = chuteSelectionService;

        // 新的 Position-Index 队列系统依赖（可选）
        _queueManager = queueManager;
        _topologyRepository = topologyRepository;
        _segmentRepository = segmentRepository;
        _sensorConfigRepository = sensorConfigRepository;
        _safeExecutor = safeExecutor;
        _intervalTracker = intervalTracker;
        _callbackConfigRepository = callbackConfigRepository;
        _lossMonitoringService = lossMonitoringService;
        _alarmService = alarmService;
        _statisticsService = statisticsService;
        _routePlanRepository = routePlanRepository;
        _upstreamServer = upstreamServer;

        _pendingAssignments = new ConcurrentDictionary<long, TaskCompletionSource<long>>();
        _parcelPaths = new ConcurrentDictionary<long, SwitchingPath>();
        _createdParcels = new ConcurrentDictionary<long, ParcelCreationRecord>();
        _parcelTargetChutes = new ConcurrentDictionary<long, long>();
        _timeoutCompensationInserted = new ConcurrentDictionary<long, byte>();
        _sensorToPositionCache = new ConcurrentDictionary<long, (long, int)>();
        _subsequentNodesCache = new ConcurrentDictionary<int, List<DiverterPathNode>>();
        _parcelBarcodes = new ConcurrentDictionary<long, string>();

        // 订阅包裹检测事件
        _sensorEventProvider.ParcelDetected += OnParcelDetected;
        _sensorEventProvider.DuplicateTriggerDetected += OnDuplicateTriggerDetected;
        _sensorEventProvider.ChuteDropoffDetected += OnChuteDropoffDetected;

        // PR-UPSTREAM02: 订阅格口分配事件（从 ChuteAssignmentReceived 改为 ChuteAssigned）
        _upstreamClient.ChuteAssigned += OnChuteAssignmentReceived;

        // 订阅服务端模式的格口分配事件（如果存在）
        // 注意：使用反射是临时方案，以避免 Execution 层引用 Communication 层（架构约束）
        // 缺点：失去编译时类型安全，事件名称或签名变更会导致运行时静默失败
        // 改进建议：将 ChuteAssigned 事件移至 Core 层的共享接口，或使用适配器模式
        if (_upstreamServer != null)
        {
            SubscribeToChuteAssignedEvent(_upstreamServer, nameof(OnChuteAssignmentReceived));
        }

        // 订阅系统状态变更事件（用于自动清空队列）
        _systemStateManager.StateChanged += OnSystemStateChanged;

        // TD-LOSS-ORCHESTRATOR-001: 订阅包裹丢失事件
        if (_lossMonitoringService != null)
        {
            _lossMonitoringService.ParcelLostDetected += OnParcelLostDetectedAsync;
        }
    }

    /// <summary>
    /// 启动编排服务
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("正在启动分拣编排服务...");

        // Issue #2: 构建传感器到位置的映射缓存（启动时优化）
        BuildSensorToPositionCache();

        // 启动传感器事件监听
        _logger.LogInformation("正在启动传感器事件监听...");
        await _sensorEventProvider.StartAsync(cancellationToken);
        _logger.LogInformation("传感器事件监听已启动");

        // 连接到上游系统
        // 连接管理由SendAsync内部处理，无需手动连接
        _logger.LogInformation("分拣编排服务已启动（上游连接由SendAsync自动管理）");
    }

    /// <summary>
    /// 停止编排服务
    /// </summary>
    public async Task StopAsync()
    {
        _logger.LogInformation("正在停止分拣编排服务...");

        // Phase 6: 清空所有队列（停止/急停/复位时）
        if (_queueManager != null)
        {
            _logger.LogInformation("正在清空所有 Position-Index 队列...");
            _queueManager.ClearAllQueues();
            _logger.LogInformation("所有队列已清空");
        }

        // 停止传感器事件监听
        _logger.LogInformation("正在停止传感器事件监听...");
        await _sensorEventProvider.StopAsync();
        _logger.LogInformation("传感器事件监听已停止");

        // 断开与上游系统的连接
        // 连接由Client内部管理

        _logger.LogInformation("分拣编排服务已停止");
    }

    /// <summary>
    /// 处理包裹分拣流程（主入口）
    /// </summary>
    public async Task<SortingResult> ProcessParcelAsync(long parcelId, long sensorId, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation(
                "[生命周期-创建] P{ParcelId} 入口传感器{SensorId}触发 T={StartTime:HH:mm:ss.fff}",
                parcelId,
                sensorId,
                _clock.LocalNow);

            // 步骤 1: 创建本地包裹实体（Parcel-First）
            await CreateParcelEntityAsync(parcelId, sensorId);

            // 步骤 2: 验证系统状态
            var stateValidation = await ValidateSystemStateAsync(parcelId);
            if (!stateValidation.IsValid)
            {
                _logger.LogWarning(
                    "[生命周期-拒绝] P{ParcelId} 系统验证失败: {Reason}",
                    parcelId,
                    stateValidation.Reason);
                CleanupParcelRecord(parcelId);
                stopwatch.Stop();
                return new SortingResult(
                    IsSuccess: false,
                    ParcelId: parcelId.ToString(),
                    ActualChuteId: 0,
                    TargetChuteId: 0,
                    ExecutionTimeMs: stopwatch.Elapsed.TotalMilliseconds,
                    FailureReason: stateValidation.Reason
                );
            }

            // 步骤 3: 拥堵检测与超载评估
            var overloadDecision = await DetectCongestionAndOverloadAsync(parcelId);

            // 步骤 4: 确定目标格口
            var targetChuteId = await DetermineTargetChuteAsync(parcelId, overloadDecision);
            _logger.LogInformation(
                "[生命周期-路由] P{ParcelId} 目标格口={TargetChuteId}",
                parcelId,
                targetChuteId);

            // 步骤 5: 生成队列任务并入队（Phase 4 完整实现）
            _logger.LogDebug("[步骤 5/5] 生成队列任务并入队");

            // 获取系统配置和异常格口ID
            var systemConfig = _systemConfigRepository.Get();
            var exceptionChuteId = systemConfig.ExceptionChuteId;

            // 检查队列服务是否可用
            if (_queueManager == null)
            {
                _logger.LogError(
                    "[队列服务缺失] 包裹 {ParcelId} 分拣失败：队列管理器未配置",
                    parcelId);

                stopwatch.Stop();
                return new SortingResult(
                    IsSuccess: false,
                    ParcelId: parcelId.ToString(),
                    ActualChuteId: 0,
                    TargetChuteId: targetChuteId,
                    ExecutionTimeMs: stopwatch.Elapsed.TotalMilliseconds,
                    FailureReason: "队列管理器未配置"
                );
            }

            // 使用 GenerateQueueTasks 生成队列任务列表
            _logger.LogDebug(
                "[队列任务生成] 开始为包裹 {ParcelId} 生成到格口 {TargetChuteId} 的队列任务",
                parcelId,
                targetChuteId);

            var queueTasks = _pathGenerator.GenerateQueueTasks(
                parcelId,
                targetChuteId,
                _clock.LocalNow);

            // 如果生成失败或为空，生成异常格口任务
            if (queueTasks == null || queueTasks.Count == 0)
            {
                _logger.LogWarning(
                    "[队列任务生成失败] 包裹 {ParcelId} 无法生成到目标格口 {TargetChuteId} 的任务，生成异常格口任务",
                    parcelId, targetChuteId);

                queueTasks = _pathGenerator.GenerateQueueTasks(
                    parcelId,
                    exceptionChuteId,
                    _clock.LocalNow);

                if (queueTasks == null || queueTasks.Count == 0)
                {
                    // 连异常格口任务都无法生成
                    stopwatch.Stop();
                    return new SortingResult(
                        IsSuccess: false,
                        ParcelId: parcelId.ToString(),
                        ActualChuteId: 0,
                        TargetChuteId: targetChuteId,
                        ExecutionTimeMs: stopwatch.Elapsed.TotalMilliseconds,
                        FailureReason: "无法生成队列任务（包括异常格口任务）"
                    );
                }

                targetChuteId = exceptionChuteId; // 更新为异常格口
            }

            // 将所有任务加入对应的 positionIndex 队列
            // 记录包裹的目标格口，用于后续回调
            _parcelTargetChutes[parcelId] = targetChuteId;

            foreach (var task in queueTasks)
            {
                _queueManager.EnqueueTask(task.PositionIndex, task);
            }

            _logger.LogInformation(
                "[生命周期-入队] P{ParcelId} {TaskCount}任务入队 目标C{TargetChuteId} 耗时{ElapsedMs:F0}ms",
                parcelId,
                queueTasks.Count,
                targetChuteId,
                stopwatch.Elapsed.TotalMilliseconds);

            stopwatch.Stop();
            return new SortingResult(
                IsSuccess: true,
                ParcelId: parcelId.ToString(),
                ActualChuteId: targetChuteId,  // 实际格口要等IO触发执行后才能最终确定
                TargetChuteId: targetChuteId,
                ExecutionTimeMs: stopwatch.Elapsed.TotalMilliseconds,
                FailureReason: null
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[处理异常] 处理包裹 {ParcelId} 时发生异常",
                parcelId);
            CleanupParcelRecord(parcelId);
            stopwatch.Stop();

            return new SortingResult(
                IsSuccess: false,
                ParcelId: parcelId.ToString(),
                ActualChuteId: 0,
                TargetChuteId: 0,
                ExecutionTimeMs: stopwatch.Elapsed.TotalMilliseconds,
                FailureReason: $"处理异常: {ex.Message}"
            );
        }
    }

    /// <summary>
    /// 执行调试分拣（跳过包裹创建和上游路由）
    /// </summary>
    public async Task<SortingResult> ExecuteDebugSortAsync(string parcelId, long targetChuteId, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("开始调试分拣: 包裹ID={ParcelId}, 目标格口={TargetChuteId}", parcelId, targetChuteId);

            // 验证系统状态
            var stateValidation = await ValidateSystemStateAsync(0); // 使用 0 作为占位符，因为调试模式不创建真实包裹
            if (!stateValidation.IsValid)
            {
                stopwatch.Stop();
                return new SortingResult(
                    IsSuccess: false,
                    ParcelId: parcelId,
                    ActualChuteId: 0,
                    TargetChuteId: targetChuteId,
                    ExecutionTimeMs: stopwatch.Elapsed.TotalMilliseconds,
                    FailureReason: stateValidation.Reason
                );
            }

            // 直接生成和执行路径
            var systemConfig = _systemConfigRepository.Get();
            var exceptionChuteId = systemConfig.ExceptionChuteId;
            var path = _pathGenerator.GeneratePath(targetChuteId);

            // 如果路径生成失败，尝试生成到异常格口的路径
            if (path == null)
            {
                path = _exceptionHandler.GenerateExceptionPath(
                    exceptionChuteId,
                    0, // 调试模式使用占位符
                    $"无法生成到格口 {targetChuteId} 的路径");

                if (path == null)
                {
                    stopwatch.Stop();
                    // Create failure result directly since we can't use exception handler with string parcelId
                    return new SortingResult(
                        IsSuccess: false,
                        ParcelId: parcelId,
                        ActualChuteId: 0,
                        TargetChuteId: targetChuteId,
                        ExecutionTimeMs: stopwatch.Elapsed.TotalMilliseconds,
                        FailureReason: "路径生成失败: 调试分拣，连异常格口路径都无法生成"
                    );
                }

                // 更新目标格口为异常格口
                targetChuteId = exceptionChuteId;
                _logger.LogInformation("已重定向到异常格口 {ExceptionChuteId}", exceptionChuteId);
            }

            _logger.LogInformation("路径生成成功: 段数={SegmentCount}, 目标格口={TargetChuteId}",
                path.Segments.Count, path.TargetChuteId);

            // 执行路径
            var executionResult = await _pathExecutor.ExecuteAsync(path, cancellationToken);

            stopwatch.Stop();

            // 记录指标和日志
            if (executionResult.IsSuccess)
            {
                _logger.LogInformation(
                    "调试分拣成功: 包裹ID={ParcelId}, 实际格口={ActualChuteId}, 目标格口={TargetChuteId}",
                    parcelId,
                    executionResult.ActualChuteId,
                    targetChuteId);
                RecordSortingSuccess(stopwatch.Elapsed.TotalSeconds);
            }
            else
            {
                _logger.LogError(
                    "调试分拣失败: 包裹ID={ParcelId}, 失败原因={FailureReason}, 实际到达格口={ActualChuteId}",
                    parcelId,
                    executionResult.FailureReason,
                    executionResult.ActualChuteId);
                RecordSortingFailure(stopwatch.Elapsed.TotalSeconds, isTimeout: true);
            }

            return new SortingResult(
                IsSuccess: executionResult.IsSuccess,
                ParcelId: parcelId,
                ActualChuteId: executionResult.ActualChuteId,
                TargetChuteId: targetChuteId,
                ExecutionTimeMs: stopwatch.Elapsed.TotalMilliseconds,
                FailureReason: executionResult.FailureReason
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "调试分拣异常: 包裹ID={ParcelId}", parcelId);
            stopwatch.Stop();

            return new SortingResult(
                IsSuccess: false,
                ParcelId: parcelId,
                ActualChuteId: 0,
                TargetChuteId: targetChuteId,
                ExecutionTimeMs: stopwatch.Elapsed.TotalMilliseconds,
                FailureReason: $"调试分拣异常: {ex.Message}"
            );
        }
    }

    #region Private Methods - 流程步骤拆分

    /// <summary>
    /// 步骤 1: 创建本地包裹实体（PR-42 Parcel-First）
    /// </summary>
    private async Task CreateParcelEntityAsync(long parcelId, long sensorId)
    {
        var createdAt = new DateTimeOffset(_clock.LocalNow);

        // PR-44: ConcurrentDictionary 是线程安全的，不需要锁
        _createdParcels[parcelId] = new ParcelCreationRecord
        {
            ParcelId = parcelId,
            CreatedAt = createdAt
        };

        _logger.LogTrace(
            "[Parcel-First] 本地创建包裹: ParcelId={ParcelId}, CreatedAt={CreatedAt:o}, 来源传感器={SensorId}",
            parcelId,
            createdAt,
            sensorId);

        // PR-10: 记录包裹创建事件
        await WriteTraceAsync(new ParcelTraceEventArgs
        {
            ItemId = parcelId,
            BarCode = null,
            OccurredAt = createdAt,
            Stage = "Created",
            Source = "Ingress",
            Details = $"传感器检测到包裹: {sensorId}"
        });

        // PR-08B: 记录包裹进入系统
        _congestionCollector?.RecordParcelEntry(parcelId, _clock.LocalNow);

        // 记录包裹在入口位置（position 0）的时间，用于跟踪入口到第一个摆轮的间隔
        _intervalTracker?.RecordParcelPosition(parcelId, 0, _clock.LocalNow);

        // 触发包裹创建事件，通知其他逻辑代码
        var parcelCreatedArgs = new ParcelCreatedEventArgs
        {
            ParcelId = parcelId,
            CreatedAt = createdAt,
            SensorId = sensorId,
            Barcode = null  // 将来可以从扫码器获取
        };

        ParcelCreated.SafeInvoke(this, parcelCreatedArgs, _logger, nameof(ParcelCreated));
    }

    /// <summary>
    /// 步骤 2: 验证系统状态
    /// </summary>
    private Task<(bool IsValid, string? Reason)> ValidateSystemStateAsync(long parcelId)
    {
        var currentState = _systemStateManager.CurrentState;
        if (!currentState.AllowsParcelCreation())
        {
            var errorMessage = currentState.GetParcelCreationDeniedMessage();
            _logger.LogWarning(
                "包裹 {ParcelId} 被拒绝：{ErrorMessage}",
                parcelId,
                errorMessage);

            return Task.FromResult((IsValid: false, Reason: (string?)errorMessage));
        }

        return Task.FromResult((IsValid: true, Reason: (string?)null));
    }

    /// <summary>
    /// 步骤 3: 拥堵检测与超载评估
    /// </summary>
    /// <summary>
    /// 步骤 3: 拥堵检测与超载评估
    /// </summary>
    private Task<OverloadDecision> DetectCongestionAndOverloadAsync(long parcelId)
    {
        // 策略相关代码已删除，始终返回正常决策
        return Task.FromResult(new OverloadDecision
        {
            ShouldForceException = false,
            ShouldMarkAsOverflow = false,
            Reason = null
        });
    }

    /// <summary>
    /// 步骤 4: 确定目标格口
    /// </summary>
    /// <remarks>
    /// PR-08: 当 IChuteSelectionService 可用时，使用统一的策略服务进行格口选择。
    /// 这样可以将分拣模式的判断逻辑收敛到单一服务中，消除多处重复的分支代码。
    ///
    /// PR-fix-upstream-notification-all-modes: 在所有模式下都向上游发送包裹检测通知。
    /// 只有 Formal 模式会等待并使用上游返回的路由决策，其他模式仅发送通知但使用本地策略。
    /// </remarks>
    private async Task<long> DetermineTargetChuteAsync(long parcelId, OverloadDecision overloadDecision)
    {
        var systemConfig = _systemConfigRepository.Get();
        var exceptionChuteId = systemConfig.ExceptionChuteId;

        // PR-fix-upstream-notification-all-modes: 在所有模式下都向上游发送包裹检测通知
        await SendUpstreamNotificationAsync(parcelId, systemConfig.ExceptionChuteId);

        // 如果超载决策要求强制异常，直接返回异常格口
        if (overloadDecision.ShouldForceException)
        {
            return exceptionChuteId;
        }

        // PR-08: 优先使用统一的格口选择服务
        if (_chuteSelectionService != null)
        {
            return await SelectChuteViaServiceAsync(parcelId, systemConfig, overloadDecision);
        }

        // 兼容模式：使用原有的模式分支逻辑
        return systemConfig.SortingMode switch
        {
            SortingMode.Formal => await GetChuteFromUpstreamAsync(parcelId, systemConfig),
            SortingMode.FixedChute => GetFixedChute(systemConfig),
            SortingMode.RoundRobin => GetNextRoundRobinChute(systemConfig),
            _ => GetDefaultExceptionChute(parcelId, systemConfig)
        };
    }

    /// <summary>
    /// PR-08: 通过统一的格口选择服务确定目标格口
    /// </summary>
    private async Task<long> SelectChuteViaServiceAsync(
        long parcelId,
        SystemConfiguration systemConfig,
        OverloadDecision overloadDecision)
    {
        var availableChuteIds = systemConfig.AvailableChuteIds?.AsReadOnly() ?? EmptyAvailableChuteIds;

        var context = new SortingContext
        {
            ParcelId = parcelId,
            SortingMode = systemConfig.SortingMode,
            ExceptionChuteId = systemConfig.ExceptionChuteId,
            FixedChuteId = systemConfig.FixedChuteId,
            AvailableChuteIds = availableChuteIds
        };

        var result = await _chuteSelectionService!.SelectChuteAsync(context, CancellationToken.None);

        if (!result.IsSuccess)
        {
            _logger.LogError(
                "包裹 {ParcelId} 格口选择失败: {ErrorMessage}，将使用异常格口 {ExceptionChuteId}",
                parcelId,
                result.ErrorMessage,
                systemConfig.ExceptionChuteId);
            return systemConfig.ExceptionChuteId;
        }

        if (result.IsException)
        {
            _logger.LogWarning(
                "包裹 {ParcelId} 路由到异常格口 {ExceptionChuteId}。原因: {Reason}",
                parcelId,
                result.TargetChuteId,
                result.ExceptionReason);
        }

        return result.TargetChuteId;
    }

    /// <summary>
    /// PR-fix-upstream-notification-all-modes: 向上游发送包裹检测通知（所有模式）
    /// </summary>
    /// <remarks>
    /// 在所有分拣模式下都向上游发送包裹检测通知。
    /// 通知失败时记录错误日志，但不阻止后续流程（只有 Formal 模式会因此返回异常格口）。
    ///
    /// <para><b>PR-fix-shadow-upstream-notification</b>：删除影子实现，上游通知只通过 IUpstreamRoutingClient 发送一次。</para>
    /// <list type="bullet">
    ///   <item>删除了重复的 Server 模式广播逻辑（line 764-798）</item>
    ///   <item>IUpstreamRoutingClient 的实现类会根据配置自动选择 Client 或 Server 模式</item>
    ///   <item>Server 模式下，实现类内部会自动广播到所有连接的客户端</item>
    /// </list>
    /// </remarks>
    private async Task SendUpstreamNotificationAsync(long parcelId, long exceptionChuteId)
    {
        // Invariant 1 - 上游请求必须引用已存在的本地包裹
        // ConcurrentDictionary.ContainsKey 是线程安全的
        if (!_createdParcels.ContainsKey(parcelId))
        {
            _logger.LogError(
                "[Invariant Violation] 尝试为不存在的包裹 {ParcelId} 发送上游通知。" +
                "通知已阻止，不发送到上游。",
                parcelId);
            return;
        }

        // 发送上游通知
        var upstreamRequestSentAt = new DateTimeOffset(_clock.LocalNow);

        // 使用 TryGetValue 是线程安全的
        if (_createdParcels.TryGetValue(parcelId, out var parcel))
        {
            parcel.UpstreamRequestSentAt = upstreamRequestSentAt;
        }

        _logger.LogInformation(
            "[Parcel-First] 发送上游包裹检测通知: ParcelId={ParcelId}, SentAt={SentAt:o}, ClientType={ClientType}, ClientFullName={ClientFullName}, IsConnected={IsConnected}",
            parcelId,
            upstreamRequestSentAt,
            _upstreamClient.GetType().Name,
            _upstreamClient.GetType().FullName,
            _upstreamClient.IsConnected);

        var notificationSent = await _upstreamClient.SendAsync(new ParcelDetectedMessage { ParcelId = parcelId, DetectedAt = _clock.LocalNowOffset }, CancellationToken.None);

        if (!notificationSent)
        {
            _logger.LogError(
                "包裹 {ParcelId} 无法发送检测通知到上游系统。连接失败或上游不可用。ClientType={ClientType}",
                parcelId,
                _upstreamClient.GetType().Name);
        }
        else
        {
            _logger.LogInformation(
                "包裹 {ParcelId} 已成功发送检测通知到上游系统 (ClientType={ClientType}, ClientFullName={ClientFullName}, IsConnected={IsConnected})",
                parcelId,
                _upstreamClient.GetType().Name,
                _upstreamClient.GetType().FullName,
                _upstreamClient.IsConnected);
        }
    }

    /// <summary>
    /// 通知上游系统包裹分拣完成
    /// </summary>
    /// <param name="parcelId">包裹ID</param>
    /// <param name="actualChuteId">实际落格格口ID</param>
    /// <param name="isSuccess">是否成功</param>
    /// <param name="failureReason">失败原因（如果失败）</param>
    /// <param name="finalStatus">包裹最终状态（可选，默认根据isSuccess推断）</param>
    /// <remarks>
    /// 在所有分拣模式下都向上游发送分拣完成通知。
    /// 通知失败时记录错误日志，但不阻止后续流程。
    /// </remarks>
    private async Task NotifyUpstreamSortingCompletedAsync(
        long parcelId,
        long actualChuteId,
        bool isSuccess,
        string? failureReason,
        Core.Enums.Parcel.ParcelFinalStatus? finalStatus = null)
    {
        try
        {
            var notification = new SortingCompletedNotification
            {
                ParcelId = parcelId,
                ActualChuteId = actualChuteId,
                CompletedAt = new DateTimeOffset(_clock.LocalNow),
                IsSuccess = isSuccess,
                FailureReason = failureReason,
                FinalStatus = finalStatus ?? (isSuccess
                    ? Core.Enums.Parcel.ParcelFinalStatus.Success
                    : Core.Enums.Parcel.ParcelFinalStatus.ExecutionError)
            };

            _logger.LogTrace(
                "发送分拣完成通知到上游: ParcelId={ParcelId}, ActualChuteId={ActualChuteId}, IsSuccess={IsSuccess}, ClientType={ClientType}",
                parcelId,
                actualChuteId,
                isSuccess,
                _upstreamClient.GetType().Name);

            var notificationSent = await _upstreamClient.SendAsync(new SortingCompletedMessage { Notification = notification }, CancellationToken.None);

            if (!notificationSent)
            {
                _logger.LogError(
                    "包裹 {ParcelId} 无法发送分拣完成通知到上游系统。连接失败或上游不可用。",
                    parcelId);
            }
            else
            {
                _logger.LogInformation(
                    "包裹 {ParcelId} 已成功发送分拣完成通知到上游系统: ActualChuteId={ActualChuteId}, IsSuccess={IsSuccess}",
                    parcelId,
                    actualChuteId,
                    isSuccess);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "发送包裹 {ParcelId} 分拣完成通知到上游系统时发生异常",
                parcelId);
        }
    }

    /// <summary>
    /// 从上游系统获取格口分配（正式分拣模式）
    /// </summary>
    /// <remarks>
    /// PR-fix-upstream-notification-all-modes: 通知发送逻辑已提取到 SendUpstreamNotificationAsync，
    /// 此方法仅负责等待上游返回格口分配。
    /// </remarks>
    private async Task<long> GetChuteFromUpstreamAsync(long parcelId, SystemConfiguration systemConfig)
    {
        var exceptionChuteId = systemConfig.ExceptionChuteId;

        // 注意：上游通知已在 DetermineTargetChuteAsync 中发送，此处不再重复发送

        // 等待上游推送格口分配（带动态超时）
        var tcs = new TaskCompletionSource<long>();

        // 计算动态超时时间
        var timeoutSeconds = CalculateChuteAssignmentTimeout(systemConfig);
        var timeoutMs = (int)(timeoutSeconds * 1000);
        var startTime = _clock.LocalNow;

        // PR-44: ConcurrentDictionary 索引器赋值是线程安全的
        _pendingAssignments[parcelId] = tcs;

        _logger.LogDebug(
            "[格口分配-等待] 包裹 {ParcelId} 开始等待上游格口分配 | 超时限制={TimeoutMs}ms | 开始时间={StartTime:HH:mm:ss.fff}",
            parcelId,
            timeoutMs,
            startTime);

        try
        {
            using var cts = new CancellationTokenSource(timeoutMs);
            var targetChuteId = await tcs.Task.WaitAsync(cts.Token);
            var elapsedMs = (_clock.LocalNow - startTime).TotalMilliseconds;

            _logger.LogInformation(
                "[格口分配-成功] 包裹 {ParcelId} 从上游系统分配到格口 {ChuteId} | 耗时={ElapsedMs:F0}ms | 超时限制={TimeoutMs}ms",
                parcelId,
                targetChuteId,
                elapsedMs,
                timeoutMs);

            // PR-10: 记录上游分配事件
            await WriteTraceAsync(new ParcelTraceEventArgs
            {
                ItemId = parcelId,
                BarCode = null,
                OccurredAt = new DateTimeOffset(_clock.LocalNow),
                Stage = "UpstreamAssigned",
                Source = "Upstream",
                Details = $"ChuteId={targetChuteId}, LatencyMs={elapsedMs:F0}, Status=Success, TimeoutMs={timeoutMs}"
            });

            return targetChuteId;
        }
        catch (TimeoutException)
        {
            var elapsedMs = (_clock.LocalNow - startTime).TotalMilliseconds;
            _logger.LogWarning(
                "[格口分配-超时] 包裹 {ParcelId} 等待上游格口分配超时 | 耗时={ElapsedMs:F0}ms | 超时限制={TimeoutMs}ms | 即将返回异常格口={ExceptionChuteId}",
                parcelId,
                elapsedMs,
                timeoutMs,
                exceptionChuteId);

            return await HandleRoutingTimeoutAsync(parcelId, systemConfig, exceptionChuteId, "Timeout");
        }
        catch (OperationCanceledException)
        {
            var elapsedMs = (_clock.LocalNow - startTime).TotalMilliseconds;
            _logger.LogWarning(
                "[格口分配-取消] 包裹 {ParcelId} 等待上游格口分配被取消 | 耗时={ElapsedMs:F0}ms | 超时限制={TimeoutMs}ms | 即将返回异常格口={ExceptionChuteId}",
                parcelId,
                elapsedMs,
                timeoutMs,
                exceptionChuteId);

            return await HandleRoutingTimeoutAsync(parcelId, systemConfig, exceptionChuteId, "Cancelled");
        }
        finally
        {
            // PR-44: ConcurrentDictionary.TryRemove 是线程安全的
            // 注意：OnChuteAssignmentReceived可能已提前移除，导致此处返回false
            var removed = _pendingAssignments.TryRemove(parcelId, out _);
            var barcodeSuffix = GetBarcodeSuffix(parcelId);
            _logger.LogDebug(
                "[格口分配-清理] 包裹 {ParcelId}{BarcodeSuffix} 的TaskCompletionSource清理完成 | " +
                "从_pendingAssignments中移除={Removed}（false表示可能已在事件处理器中提前移除）",
                parcelId,
                barcodeSuffix,
                removed);
        }
    }

    /// <summary>
    /// 计算格口分配超时时间（秒）
    /// </summary>
    private decimal CalculateChuteAssignmentTimeout(SystemConfiguration systemConfig)
    {
        // 如果有超时计算器，使用动态计算
        if (_timeoutCalculator != null)
        {
            var context = new ChuteAssignmentTimeoutContext(
                LineId: 1, // TD-042: 当前假设只有一条线，未来支持多线时需要从包裹上下文获取LineId
                SafetyFactor: systemConfig.ChuteAssignmentTimeout?.SafetyFactor ?? 0.9m
            );

            return _timeoutCalculator.CalculateTimeoutSeconds(context);
        }

        // 降级：使用配置的固定超时时间
        return systemConfig.ChuteAssignmentTimeout?.FallbackTimeoutSeconds ?? _options.FallbackTimeoutSeconds;
    }

    /// <summary>
    /// 处理路由超时（提取公共逻辑）
    /// </summary>
    private async Task<long> HandleRoutingTimeoutAsync(
        long parcelId,
        SystemConfiguration systemConfig,
        long exceptionChuteId,
        string status)
    {
        var timeoutSeconds = CalculateChuteAssignmentTimeout(systemConfig);
        var timeoutMs = (int)(timeoutSeconds * 1000);

        _logger.LogWarning(
            "【路由超时兜底】包裹 {ParcelId} 等待格口分配超时（超时限制：{TimeoutMs}ms），已分拣至异常口。" +
            "异常口ChuteId={ExceptionChuteId}, " +
            "发生时间={OccurredAt:yyyy-MM-dd HH:mm:ss.fff}",
            parcelId,
            timeoutMs,
            exceptionChuteId,
            _clock.LocalNow);

        // PR-10: 记录超时事件
        await WriteTraceAsync(new ParcelTraceEventArgs
        {
            ItemId = parcelId,
            BarCode = null,
            OccurredAt = new DateTimeOffset(_clock.LocalNow),
            Stage = "RoutingTimeout",
            Source = "Upstream",
            Details = $"TimeoutMs={timeoutMs}, Status={status}, RoutedToException={exceptionChuteId}"
        });

        // 立即发送超时通知到上游系统
        await NotifyUpstreamSortingCompletedAsync(
            parcelId,
            exceptionChuteId,
            isSuccess: false,
            failureReason: $"AssignmentTimeout: {status}",
            finalStatus: Core.Enums.Parcel.ParcelFinalStatus.Timeout);

        // 清理目标格口记录，防止后续IO触发时重复发送通知
        _parcelTargetChutes.TryRemove(parcelId, out _);
        
        // 清理条码缓存
        _parcelBarcodes.TryRemove(parcelId, out _);

        return exceptionChuteId;
    }

    /// <summary>
    /// 获取固定格口（固定格口模式）
    /// </summary>
    private long GetFixedChute(SystemConfiguration systemConfig)
    {
        var chuteId = systemConfig.FixedChuteId ?? systemConfig.ExceptionChuteId;
        _logger.LogDebug("使用固定格口模式，目标格口: {ChuteId}", chuteId);
        return chuteId;
    }

    /// <summary>
    /// 获取下一个轮询格口（轮询模式）
    /// </summary>
    private long GetNextRoundRobinChute(SystemConfiguration systemConfig)
    {
        lock (_lockObject)
        {
            if (systemConfig.AvailableChuteIds == null || systemConfig.AvailableChuteIds.Count == 0)
            {
                _logger.LogError("循环格口落格模式配置错误：没有可用格口，将使用异常格口");
                return systemConfig.ExceptionChuteId;
            }

            var chuteId = systemConfig.AvailableChuteIds[_roundRobinIndex];
            _roundRobinIndex = (_roundRobinIndex + 1) % systemConfig.AvailableChuteIds.Count;

            _logger.LogDebug("使用轮询模式，目标格口: {ChuteId}", chuteId);
            return chuteId;
        }
    }

    /// <summary>
    /// 获取默认异常格口（未知模式）
    /// </summary>
    private long GetDefaultExceptionChute(long parcelId, SystemConfiguration systemConfig)
    {
        _logger.LogError(
            "未知的分拣模式 {SortingMode}，包裹 {ParcelId} 将发送到异常格口",
            systemConfig.SortingMode,
            parcelId);
        return systemConfig.ExceptionChuteId;
    }

    /// <summary>
    /// 5.5: 记录分拣结果
    /// </summary>
    /// <remarks>
    /// PR-UPSTREAM02: 添加落格完成通知发送
    /// </remarks>
    private async Task RecordSortingResultAsync(long parcelId, SortingResult result, bool isOverloadException)
    {
        // PR-08B: 记录包裹完成
        _congestionCollector?.RecordParcelCompletion(parcelId, _clock.LocalNow, result.IsSuccess);

        // 清理路径记录
        // PR-44: ConcurrentDictionary.TryRemove 是线程安全的
        _parcelPaths.TryRemove(parcelId, out _);
        
        // 清理条码缓存
        _parcelBarcodes.TryRemove(parcelId, out _);

        // 清理位置追踪记录（防止内存泄漏和混淆后续包裹）
        _intervalTracker?.ClearParcelTracking(parcelId);

        // PR-UPSTREAM02: 发送落格完成通知给上游系统
        var notification = new SortingCompletedNotification
        {
            ParcelId = parcelId,
            ActualChuteId = result.ActualChuteId,
            CompletedAt = new DateTimeOffset(_clock.LocalNow),
            IsSuccess = result.IsSuccess && !isOverloadException,
            FailureReason = isOverloadException ? "超载重定向到异常格口" : result.FailureReason
        };

        var notificationSent = await _upstreamClient.SendAsync(new SortingCompletedMessage { Notification = notification }, CancellationToken.None);

        if (!notificationSent)
        {
            _logger.LogWarning(
                "[落格完成通知] 发送失败 | ParcelId={ParcelId} | ChuteId={ChuteId} | IsSuccess={IsSuccess}",
                parcelId,
                result.ActualChuteId,
                result.IsSuccess);
        }
    }

    /// <summary>
    /// 清理包裹记录
    /// </summary>
    private void CleanupParcelRecord(long parcelId)
    {
        // PR-44: ConcurrentDictionary.TryRemove 是线程安全的
        _createdParcels.TryRemove(parcelId, out _);
        _parcelPaths.TryRemove(parcelId, out _);
        _pendingAssignments.TryRemove(parcelId, out _);
        _parcelBarcodes.TryRemove(parcelId, out _);
    }

    #endregion Private Methods - 流程步骤拆分

    #region Event Handlers - 事件处理

    /// <summary>
    /// 处理包裹检测事件
    /// </summary>
    /// <remarks>
    /// PR-fix-event-handler-logging: 增强日志和异常处理
    /// </remarks>
    private async void OnParcelDetected(object? sender, ParcelDetectedEventArgs e)
    {
        try
        {
            _logger.LogDebug(
                "[事件处理] 收到 ParcelDetected 事件: ParcelId={ParcelId}, SensorId={SensorId}, DetectedAt={DetectedAt:o}",
                e.ParcelId,
                e.SensorId,
                e.DetectedAt);

            // Issue #2: 检查是否为 WheelFront 传感器触发（使用缓存避免重复查询）
            if (_queueManager != null && _sensorToPositionCache.TryGetValue(e.SensorId, out var position))
            {
                _logger.LogInformation(
                    "[WheelFront触发] 检测到摆轮前传感器触发: SensorId={SensorId}, DiverterId={DiverterId}, PositionIndex={PositionIndex}",
                    e.SensorId,
                    position.DiverterId,
                    position.PositionIndex);

                // 这是摆轮前传感器触发，处理待执行队列中的包裹
                await HandleWheelFrontSensorAsync(e.SensorId, position.DiverterId, position.PositionIndex);
                return;
            }
            else if (_sensorConfigRepository != null && _queueManager != null && _topologyRepository != null)
            {
                // 缓存未命中时fallback到原有逻辑（仅在缓存未初始化或配置更新时）
                var sensorConfig = _sensorConfigRepository.Get();
                var sensor = sensorConfig.Sensors.FirstOrDefault(s => s.SensorId == e.SensorId);

                if (sensor?.IoType == SensorIoType.WheelFront)
                {
                    // 从拓扑配置中找到对应的摆轮节点
                    var topology = _topologyRepository.Get();
                    var node = topology?.DiverterNodes.FirstOrDefault(n => n.FrontSensorId == e.SensorId);

                    if (node == null)
                    {
                        _logger.LogError(
                            "[配置错误] WheelFront传感器 {SensorId} 在拓扑配置中未找到对应的摆轮节点",
                            e.SensorId);
                        return;
                    }

                    _logger.LogInformation(
                        "[WheelFront触发] 检测到摆轮前传感器触发: SensorId={SensorId}, DiverterId={DiverterId}, PositionIndex={PositionIndex}",
                        e.SensorId,
                        node.DiverterId,
                        node.PositionIndex);

                    // 这是摆轮前传感器触发，处理待执行队列中的包裹
                    await HandleWheelFrontSensorAsync(e.SensorId, node.DiverterId, node.PositionIndex);
                    return;
                }
            }
            else
            {
                _logger.LogWarning(
                    "[配置缺失] Position-Index 队列系统组件未完全配置: SensorConfigRepo={HasSensorConfig}, QueueManager={HasQueue}, TopologyRepo={HasTopo}",
                    _sensorConfigRepository != null,
                    _queueManager != null,
                    _topologyRepository != null);
            }

            // 默认行为：ParcelCreation 传感器，创建新包裹并进入正常分拣流程
            _logger.LogInformation(
                "[ParcelCreation触发] 检测到包裹创建传感器触发，开始处理包裹: ParcelId={ParcelId}, SensorId={SensorId}",
                e.ParcelId,
                e.SensorId);

            await ProcessParcelAsync(e.ParcelId, e.SensorId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[事件处理异常] 处理 ParcelDetected 事件时发生异常: ParcelId={ParcelId}, SensorId={SensorId}",
                e.ParcelId,
                e.SensorId);
        }
    }

    /// <summary>
    /// 处理摆轮前传感器触发事件（TD-062）
    /// </summary>
    /// <param name="sensorId">传感器ID</param>
    /// <param name="boundWheelDiverterId">绑定的摆轮ID（long类型）</param>
    /// <param name="positionIndex">Position Index</param>
    private async Task HandleWheelFrontSensorAsync(long sensorId, long boundWheelDiverterId, int positionIndex)
    {
        _logger.LogDebug(
            "开始处理摆轮前传感器触发: SensorId={SensorId}, BoundWheelDiverterId={BoundWheelDiverterId}, PositionIndex={PositionIndex}",
            sensorId,
            boundWheelDiverterId,
            positionIndex);

        if (_safeExecutor != null)
        {
            _logger.LogDebug("使用 SafeExecutionService 执行摆轮前传感器处理");
            // 使用 SafeExecutionService 包裹异步操作
            await _safeExecutor.ExecuteAsync(
                () => ExecuteWheelFrontSortingAsync(boundWheelDiverterId, sensorId, positionIndex),
                operationName: $"WheelFrontTriggered_Sensor{sensorId}",
                cancellationToken: default);
        }
        else
        {
            _logger.LogDebug("直接执行摆轮前传感器处理（无 SafeExecutor）");
            // Fallback: 没有 SafeExecutor 时直接执行
            await ExecuteWheelFrontSortingAsync(boundWheelDiverterId, sensorId, positionIndex);
        }
    }

    /// <summary>
    /// 执行摆轮前传感器触发的分拣逻辑（Position-Index 队列系统）
    /// </summary>
    /// <param name="boundWheelDiverterId">绑定的摆轮ID（long类型）</param>
    /// <param name="sensorId">传感器ID</param>
    /// <param name="positionIndex">Position Index</param>
    private async Task ExecuteWheelFrontSortingAsync(long boundWheelDiverterId, long sensorId, int positionIndex)
    {
        var currentTime = _clock.LocalNow;

        _logger.LogDebug(
            "Position {PositionIndex} 传感器 {SensorId} 触发，检查队列任务",
            positionIndex, sensorId);

        // 先窥视队列头部任务（不出队）
        var peekedTask = _queueManager!.PeekTask(positionIndex);
        
        if (peekedTask == null)
        {
            // 队列为空但传感器触发 - 可能是输送线上的残留包裹（干扰信号）
            // 不应记录为超时异常，只记录日志用于监控
            _logger.LogInformation(
                "[干扰信号] Position {PositionIndex} 队列为空，但传感器 {SensorId} 被触发 (摆轮ID={WheelDiverterId}) - " +
                "可能是输送线残留包裹，不执行任何动作",
                positionIndex, sensorId, boundWheelDiverterId);
            
            // 不记录为分拣失败，不影响系统统计
            // RecordSortingFailure(0, isTimeout: true); // 移除此调用
            return;
        }

        // 检查是否启用提前触发检测
        var systemConfig = _systemConfigRepository.Get();
        var enableEarlyTriggerDetection = systemConfig?.EnableEarlyTriggerDetection ?? false;
        
        // 提前触发检测：如果启用且队列任务有 EarliestDequeueTime，检查当前时间是否过早
        if (enableEarlyTriggerDetection
            && peekedTask.EarliestDequeueTime is DateTime earliestDequeueTime
            && currentTime < earliestDequeueTime)
        {
            var earlyMs = (earliestDequeueTime - currentTime).TotalMilliseconds;
            
            // 获取输送段信息用于日志记录
            string segmentInfo = "线体信息未知";
            if (_topologyRepository != null && _segmentRepository != null)
            {
                var topology = _topologyRepository.Get();
                if (topology != null)
                {
                    var node = topology.FindNodeByDiverterId(peekedTask.DiverterId);
                    if (node != null)
                    {
                        var segment = _segmentRepository.GetById(node.SegmentId);
                        if (segment != null)
                        {
                            segmentInfo = $"线体Id={segment.SegmentId}, " +
                                         $"线体长度={segment.LengthMm}mm, " +
                                         $"线体速度={segment.SpeedMmps}mm/s, " +
                                         $"容差时间={segment.TimeToleranceMs}ms";
                        }
                    }
                }
            }
            
            _logger.LogWarning(
                "[提前触发检测] Position {PositionIndex} 传感器 {SensorId} 提前触发 {EarlyMs}ms，" +
                "包裹 {ParcelId} 不出队、不执行动作 | " +
                "当前时间={CurrentTime:HH:mm:ss.fff}, " +
                "最早出队时间={EarliestTime:HH:mm:ss.fff}, " +
                "期望到达时间={ExpectedTime:HH:mm:ss.fff}, " +
                "{SegmentInfo}",
                positionIndex, sensorId, earlyMs,
                peekedTask.ParcelId,
                currentTime,
                earliestDequeueTime,
                peekedTask.ExpectedArrivalTime,
                segmentInfo);
            
            // 记录为分拣异常（用于监控提前触发频率）
            _alarmService?.RecordSortingFailure();
            
            // 不出队、不执行动作，直接返回
            return;
        }
        
        // 正常流程：从队列取出任务
        var task = _queueManager!.DequeueTask(positionIndex);
        
        if (task == null)
        {
            // 理论上不应该发生（刚刚窥视时队列有任务），但防御性编程
            _logger.LogWarning(
                "Position {PositionIndex} 窥视有任务但出队失败，传感器 {SensorId} (摆轮ID={WheelDiverterId})",
                positionIndex, sensorId, boundWheelDiverterId);
            
            RecordSortingFailure(0, isTimeout: true);
            return;
        }

        _logger.LogDebug(
            "[生命周期-传感器] P{ParcelId} Pos{PositionIndex} S{SensorId}触发 取队列任务",
            task.ParcelId, positionIndex, sensorId);

        // 记录包裹到达此位置（用于跟踪相邻position间的间隔）
        _intervalTracker?.RecordParcelPosition(task.ParcelId, positionIndex, currentTime);

        // 动态更新下一个position的期望到达时间（避免误差累积）
        // 只有在启用提前触发检测时才更新，因为这个功能依赖于准确的时间窗口
        if (systemConfig?.EnableEarlyTriggerDetection ?? false)
        {
            UpdateNextPositionExpectedTime(task, positionIndex, currentTime);
        }

        // IO触发逻辑说明：
        // 1. 包裹物理丢失的情况由 ParcelLossMonitoringService 主动检测并处理
        // 2. 既然IO被触发，说明包裹物理上已经到达传感器，不可能是"丢失"
        // 3. IO触发时只需要判断"超时"（延迟到达）或"正常到达"
        // 4. 即使包裹延迟很大（如前面包裹丢失导致），也应该正常执行摆轮动作

        // 检查是否超时（延迟到达）
        // FIX: 读取全局实时配置，确保通过 /api/sorting/detection-switches 的配置变更立即生效
        // 使用 SystemConfiguration.EnableTimeoutDetection 作为唯一控制源
        var enableTimeoutDetection = systemConfig?.EnableTimeoutDetection ?? false;
        
        // - 启用时：延迟到达判定为"超时"，执行回退动作并插入补偿任务
        // - 禁用时：不区分"延迟到达"和"正常到达"，一律按正常到达执行主动作
        var isTimeout = enableTimeoutDetection && 
                       currentTime > task.ExpectedArrivalTime.AddMilliseconds(task.TimeoutThresholdMs);

        DiverterDirection actionToExecute;

        if (isTimeout)
        {
            var delayMs = (currentTime - task.ExpectedArrivalTime).TotalMilliseconds;
            _logger.LogWarning(
                "包裹 {ParcelId} 在 Position {PositionIndex} 超时 (延迟 {DelayMs}ms)，使用回退动作 {FallbackAction}",
                task.ParcelId, positionIndex, delayMs, task.FallbackAction);

            actionToExecute = task.FallbackAction;

            // 超时包裹需要在后续所有 position 插入 Straight 任务到队列头部
            // 但只在首次超时时插入一次，避免重复插入导致队列堆积
            if (_timeoutCompensationInserted.TryAdd(task.ParcelId, 0))
            {
                // Issue #3: 使用缓存的后续节点列表，避免重复查询和构造
                if (_subsequentNodesCache.TryGetValue(positionIndex, out var subsequentNodes) && subsequentNodes.Any())
                {
                    _logger.LogWarning(
                        "包裹 {ParcelId} 首次超时，在后续 {Count} 个 position 插入 Straight 任务: [{Positions}]",
                        task.ParcelId,
                        subsequentNodes.Count,
                        string.Join(", ", subsequentNodes.Select(n => n.PositionIndex)));

                    foreach (var node in subsequentNodes)
                    {
                        var now = _clock.LocalNow;
                        var compensationEarliestDequeueTime = now.AddMilliseconds(-task.TimeoutThresholdMs);
                        if (compensationEarliestDequeueTime < task.CreatedAt)
                        {
                            compensationEarliestDequeueTime = task.CreatedAt;
                        }
                        
                        var straightTask = new PositionQueueItem
                        {
                            ParcelId = task.ParcelId,
                            DiverterId = node.DiverterId,
                            DiverterAction = DiverterDirection.Straight,
                            ExpectedArrivalTime = now, // 已超时，使用当前时间
                            TimeoutThresholdMs = task.TimeoutThresholdMs,
                            FallbackAction = DiverterDirection.Straight,
                            PositionIndex = node.PositionIndex,
                            CreatedAt = task.CreatedAt, // 使用原始包裹创建时间
                            // 丢失判定超时 = TimeoutThreshold * 1.5
                            LostDetectionTimeoutMs = (long)(task.TimeoutThresholdMs * 1.5),
                            LostDetectionDeadline = now.AddMilliseconds(task.TimeoutThresholdMs * 1.5),
                            // 最早出队时间（提前触发检测）
                            EarliestDequeueTime = compensationEarliestDequeueTime,
                            // 继承原任务的超时检测开关设置
                            EnableTimeoutDetection = task.EnableTimeoutDetection
                        };

                        // 使用优先入队，插入到队列头部
                        _queueManager!.EnqueuePriorityTask(node.PositionIndex, straightTask);
                    }
                }
                else if (_topologyRepository != null)
                {
                    // 缓存未命中时的fallback（配置动态变更场景）
                    var topology = _topologyRepository.Get();
                    if (topology != null)
                    {
                        // 找到所有比当前 position 大的节点
                        var fallbackSubsequentNodes = topology.DiverterNodes
                            .Where(n => n.PositionIndex > positionIndex)
                            .OrderBy(n => n.PositionIndex)
                            .ToList();

                        if (fallbackSubsequentNodes.Any())
                        {
                            _logger.LogWarning(
                                "包裹 {ParcelId} 首次超时，在后续 {Count} 个 position 插入 Straight 任务 (缓存未命中，使用fallback): [{Positions}]",
                                task.ParcelId,
                                fallbackSubsequentNodes.Count,
                                string.Join(", ", fallbackSubsequentNodes.Select(n => n.PositionIndex)));

                            foreach (var node in fallbackSubsequentNodes)
                            {
                                var now = _clock.LocalNow;
                                var fallbackEarliestDequeueTime = now.AddMilliseconds(-task.TimeoutThresholdMs);
                                if (fallbackEarliestDequeueTime < task.CreatedAt)
                                {
                                    fallbackEarliestDequeueTime = task.CreatedAt;
                                }
                                
                                var straightTask = new PositionQueueItem
                                {
                                    ParcelId = task.ParcelId,
                                    DiverterId = node.DiverterId,
                                    DiverterAction = DiverterDirection.Straight,
                                    ExpectedArrivalTime = now, // 已超时，使用当前时间
                                    TimeoutThresholdMs = task.TimeoutThresholdMs,
                                    FallbackAction = DiverterDirection.Straight,
                                    PositionIndex = node.PositionIndex,
                                    CreatedAt = task.CreatedAt, // 使用原始包裹创建时间
                                    // 丢失判定超时 = TimeoutThreshold * 1.5
                                    LostDetectionTimeoutMs = (long)(task.TimeoutThresholdMs * 1.5),
                                    LostDetectionDeadline = now.AddMilliseconds(task.TimeoutThresholdMs * 1.5),
                                    // 最早出队时间（提前触发检测）
                                    EarliestDequeueTime = fallbackEarliestDequeueTime,
                                    // 继承原任务的超时检测开关设置
                                    EnableTimeoutDetection = task.EnableTimeoutDetection
                                };

                                // 使用优先入队，插入到队列头部
                                _queueManager!.EnqueuePriorityTask(node.PositionIndex, straightTask);
                            }
                        }
                    }
                }
            }
            else if (!_timeoutCompensationInserted.ContainsKey(task.ParcelId))
            {
                _logger.LogDebug(
                    "包裹 {ParcelId} 在 Position {PositionIndex} 再次超时，已在首次超时时插入补偿任务，本次不重复插入",
                    task.ParcelId, positionIndex);
            }

            RecordSortingFailure(0, isTimeout: true);
        }
        else
        {
            actionToExecute = task.DiverterAction;
        }

        _logger.LogInformation(
            "包裹 {ParcelId} 在 Position {PositionIndex} 执行动作 {Action} (摆轮ID={DiverterId}, 超时={IsTimeout})",
            task.ParcelId, positionIndex, actionToExecute, task.DiverterId, isTimeout);

        // 执行摆轮动作（Phase 5 完整实现）
        // 使用现有的 PathExecutor 执行单段路径，复用已有的硬件抽象层
        var singleSegmentPath = new SwitchingPath
        {
            // In single-segment (single-action) execution, the target chute is determined by the diverter action itself,
            // not by a multi-segment path. Therefore, TargetChuteId is set to 0 as a placeholder and is not used.
            TargetChuteId = 0,
            Segments = new List<SwitchingPathSegment>
            {
                new SwitchingPathSegment
                {
                    SequenceNumber = 1,
                    DiverterId = task.DiverterId,
                    TargetDirection = actionToExecute,
                    TtlMilliseconds = DefaultSingleActionTimeoutMs
                }
            }.AsReadOnly(),
            GeneratedAt = _clock.LocalNowOffset,
            FallbackChuteId = 0
        };

        try
        {
            var executionResult = await _pathExecutor.ExecuteAsync(singleSegmentPath, default);

            if (!executionResult.IsSuccess)
            {
                _logger.LogError(
                    "[生命周期-失败] P{ParcelId} Pos{PositionIndex} 摆轮执行失败: {ErrorMessage}",
                    task.ParcelId, positionIndex, executionResult.FailureReason);
                RecordSortingFailure(0, isTimeout: true);

                // 摆轮动作失败，通知上游
                await NotifyUpstreamSortingCompletedAsync(
                    task.ParcelId,
                    0, // 未知格口
                    isSuccess: false,
                    failureReason: executionResult.FailureReason);
                return;
            }

            _logger.LogTrace(
                "[生命周期-执行] P{ParcelId} Pos{PositionIndex} 摆轮动作{Action}执行完成",
                task.ParcelId, positionIndex, actionToExecute);

            // 检查是否需要在摆轮执行时触发回调
            if (_callbackConfigRepository != null)
            {
                var callbackConfig = _callbackConfigRepository.Get();
                if (callbackConfig.CallbackMode == ChuteDropoffCallbackMode.OnWheelExecution)
                {
                    // OnWheelExecution 模式：检查是否为终态
                    // 1. 摆轮实际转向（Left 或 Right）- 包裹已完成分拣，落入格口
                    // 2. 最后一个摆轮直行通过 - 包裹已到达拓扑末端，必然是终态，落入异常格口
                    bool isLastDiverter = IsLastDiverterInTopology(positionIndex);
                    bool isFinalState = (actionToExecute != DiverterDirection.Straight) || isLastDiverter;

                    if (isFinalState)
                    {
                        // 需要确定实际的目标格口ID
                        long actualChuteId = await DetermineActualChuteIdAsync(task.ParcelId, actionToExecute, task.DiverterId);

                        // 检查是否已经发送过通知（防止重复触发时重复发送）
                        // 只在 actualChuteId > 0 时发送通知并清理
                        if (actualChuteId > 0)
                        {
                            if (isLastDiverter && actionToExecute == DiverterDirection.Straight)
                            {
                                _logger.LogInformation(
                                    "[生命周期-完成] P{ParcelId} D{DiverterId}最后摆轮直行 落入异常格口C{ActualChuteId} (OnWheelExecution模式)",
                                    task.ParcelId, task.DiverterId, actualChuteId);
                            }
                            else
                            {
                                _logger.LogInformation(
                                    "[生命周期-完成] P{ParcelId} D{DiverterId}转向{Direction} 落格C{ActualChuteId} (OnWheelExecution模式)",
                                    task.ParcelId, task.DiverterId, actionToExecute, actualChuteId);
                            }

                            await NotifyUpstreamSortingCompletedAsync(
                                task.ParcelId,
                                actualChuteId,
                                isSuccess: !isTimeout,
                                failureReason: isTimeout ? "SortingTimeout" : null,
                                finalStatus: isTimeout ? Core.Enums.Parcel.ParcelFinalStatus.Timeout : null);

                            // 发送通知后清理包裹在内存中的所有痕迹，防止内存泄漏
                            CleanupParcelMemory(task.ParcelId);
                        }
                        else
                        {
                            _logger.LogWarning(
                                "包裹 {ParcelId} 摆轮执行完成，但无法确定目标格口ID（可能已发送过通知），跳过重复发送",
                                task.ParcelId);
                        }
                    }
                    else
                    {
                        _logger.LogDebug(
                            "包裹 {ParcelId} 在摆轮 {DiverterId} 直行通过，还需经过后续摆轮，OnWheelExecution 模式不发送通知",
                            task.ParcelId, task.DiverterId);
                    }
                }
                else
                {
                    // OnSensorTrigger 模式：超时时也需要通知上游分拣失败
                    if (isTimeout)
                    {
                        long actualChuteId = await DetermineActualChuteIdAsync(task.ParcelId, actionToExecute, task.DiverterId);
                        _logger.LogWarning(
                            "[生命周期-超时] P{ParcelId} 超时到达 落格C{ActualChuteId} (OnSensorTrigger模式)",
                            task.ParcelId, actualChuteId);
                        await NotifyUpstreamSortingCompletedAsync(
                            task.ParcelId,
                            actualChuteId,
                            isSuccess: false,
                            failureReason: "SortingTimeout",
                            finalStatus: Core.Enums.Parcel.ParcelFinalStatus.Timeout);

                        // 发送通知后清理包裹在内存中的所有痕迹
                        CleanupParcelMemory(task.ParcelId);
                    }
                    else
                    {
                        _logger.LogDebug(
                            "包裹 {ParcelId} 摆轮执行成功，OnSensorTrigger 模式等待落格传感器触发",
                            task.ParcelId);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "包裹 {ParcelId} 在 Position {PositionIndex} 执行摆轮动作时发生异常",
                task.ParcelId, positionIndex);
            RecordSortingFailure(0, isTimeout: true);

            // 异常情况，通知上游
            await NotifyUpstreamSortingCompletedAsync(
                task.ParcelId,
                0,
                isSuccess: false,
                failureReason: ex.Message);
            return;
        }

        if (!isTimeout)
        {
            RecordSortingSuccess(0);
        }
    }

    /// <summary>
    /// 判断指定位置索引是否为拓扑中的最后一个摆轮
    /// </summary>
    /// <param name="positionIndex">位置索引</param>
    /// <returns>是否为最后一个摆轮</returns>
    private bool IsLastDiverterInTopology(int positionIndex)
    {
        if (_topologyRepository == null)
        {
            // 如果拓扑仓储不可用，保守地返回 false
            _logger.LogWarning("拓扑仓储不可用，无法判断是否为最后一个摆轮，Position={PositionIndex}", positionIndex);
            return false;
        }

        var topology = _topologyRepository.Get();
        if (topology == null || topology.DiverterNodes == null || topology.DiverterNodes.Count == 0)
        {
            _logger.LogWarning("拓扑配置无效或为空，无法判断是否为最后一个摆轮，Position={PositionIndex}", positionIndex);
            return false;
        }

        // 获取拓扑中的最大位置索引
        var maxPositionIndex = topology.DiverterNodes.Max(n => n.PositionIndex);

        // 如果当前位置索引等于最大位置索引，则为最后一个摆轮
        bool isLast = positionIndex == maxPositionIndex;

        _logger.LogDebug(
            "判断摆轮位置: Position={PositionIndex}, MaxPosition={MaxPosition}, IsLast={IsLast}",
            positionIndex, maxPositionIndex, isLast);

        return isLast;
    }

    /// <summary>
    /// 确定包裹的实际落格格口ID
    /// </summary>
    /// <param name="parcelId">包裹ID</param>
    /// <param name="action">执行的摆轮动作</param>
    /// <param name="diverterId">摆轮ID</param>
    /// <returns>实际格口ID</returns>
    private Task<long> DetermineActualChuteIdAsync(long parcelId, DiverterDirection action, long diverterId)
    {
        // 首先尝试从目标格口字典中获取（Position-Index 队列系统）
        if (_parcelTargetChutes.TryGetValue(parcelId, out var targetChuteId))
        {
            _logger.LogDebug(
                "从目标格口字典获取包裹 {ParcelId} 的目标格口: {TargetChuteId}",
                parcelId, targetChuteId);
            return Task.FromResult(targetChuteId);
        }

        // 尝试从包裹路径中获取目标格口（旧的路径系统）
        if (_parcelPaths.TryGetValue(parcelId, out var path))
        {
            _logger.LogDebug(
                "从路径字典获取包裹 {ParcelId} 的目标格口: {TargetChuteId}",
                parcelId, path.TargetChuteId);
            return Task.FromResult(path.TargetChuteId);
        }

        // 无法确定，返回0
        _logger.LogWarning(
            "无法确定包裹 {ParcelId} 的实际格口ID，DiverterId={DiverterId}, Action={Action}",
            parcelId, diverterId, action);
        return Task.FromResult(0L);
    }

    /// <summary>
    /// 处理重复触发异常事件
    /// </summary>
    private async void OnDuplicateTriggerDetected(object? sender, DuplicateTriggerEventArgs e)
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
            // PR-42: Parcel-First - 本地创建包裹实体
            await CreateParcelEntityAsync(parcelId, e.SensorId);

            // 获取异常格口ID
            var systemConfig = _systemConfigRepository.Get();
            var exceptionChuteId = systemConfig.ExceptionChuteId;

            // 通知上游包裹重复触发异常（不等待响应）
            await _upstreamClient.SendAsync(new ParcelDetectedMessage { ParcelId = parcelId, DetectedAt = _clock.LocalNowOffset }, CancellationToken.None);

            // 使用新的队列系统将包裹发送到异常格口
            if (_queueManager != null)
            {
                var queueTasks = _pathGenerator.GenerateQueueTasks(
                    parcelId,
                    exceptionChuteId,
                    _clock.LocalNow);

                if (queueTasks != null && queueTasks.Count > 0)
                {
                    // 记录包裹的目标格口
                    _parcelTargetChutes[parcelId] = exceptionChuteId;

                    foreach (var task in queueTasks)
                    {
                        _queueManager.EnqueueTask(task.PositionIndex, task);
                    }

                    _logger.LogInformation(
                        "重复触发异常包裹 {ParcelId} 已加入队列，目标异常格口={ExceptionChuteId}",
                        parcelId, exceptionChuteId);
                }
                else
                {
                    _logger.LogError(
                        "重复触发异常包裹 {ParcelId} 无法生成到异常格口的队列任务",
                        parcelId);
                }
            }
            else
            {
                _logger.LogError(
                    "重复触发异常包裹 {ParcelId} 处理失败：队列管理器未配置",
                    parcelId);
            }

            CleanupParcelRecord(parcelId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理重复触发异常包裹 {ParcelId} 时发生错误", parcelId);
            CleanupParcelRecord(parcelId);
        }
    }

    /// <summary>
    /// 处理落格传感器检测事件
    /// </summary>
    private async void OnChuteDropoffDetected(object? sender, ChuteDropoffDetectedEventArgs e)
    {
        try
        {
            _logger.LogInformation(
                "[落格事件] 落格传感器检测到包裹落入格口: ChuteId={ChuteId}, SensorId={SensorId}, DetectedAt={DetectedAt:o}",
                e.ChuteId,
                e.SensorId,
                e.DetectedAt);

            // 检查是否为 OnSensorTrigger 模式
            if (_callbackConfigRepository == null)
            {
                _logger.LogDebug("未注入落格回调配置仓储，跳过处理");
                return;
            }

            var callbackConfig = _callbackConfigRepository.Get();
            if (callbackConfig.CallbackMode != ChuteDropoffCallbackMode.OnSensorTrigger)
            {
                _logger.LogDebug(
                    "当前落格回调模式为 {Mode}，不处理落格传感器事件",
                    callbackConfig.CallbackMode);
                return;
            }

            // 在 OnSensorTrigger 模式下，需要找到落入该格口的包裹ID
            // 从 _parcelTargetChutes 中查找目标格口为该格口的包裹
            long? parcelId = FindParcelByTargetChute(e.ChuteId);

            if (parcelId == null)
            {
                _logger.LogWarning(
                    "[落格事件] 无法找到目标格口为 {ChuteId} 的包裹，可能已经完成或超时",
                    e.ChuteId);
                return;
            }

            _logger.LogInformation(
                "[生命周期-完成] P{ParcelId} 落入格口C{ChuteId} (OnSensorTrigger模式)",
                parcelId.Value,
                e.ChuteId);

            // 发送分拣完成通知
            await NotifyUpstreamSortingCompletedAsync(
                parcelId.Value,
                e.ChuteId,
                isSuccess: true,
                failureReason: null);

            // 清理包裹在内存中的所有痕迹
            CleanupParcelMemory(parcelId.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[落格事件异常] 处理落格传感器事件时发生异常: ChuteId={ChuteId}, SensorId={SensorId}",
                e.ChuteId,
                e.SensorId);
        }
    }

    /// <summary>
    /// 根据目标格口查找包裹ID
    /// </summary>
    /// <param name="targetChuteId">目标格口ID</param>
    /// <returns>包裹ID，如果未找到则返回null</returns>
    /// <remarks>
    /// TODO (TD-073): 当多个包裹同时分拣到同一格口时，FirstOrDefault 只会返回第一个匹配的包裹ID。
    /// 可能的优化方案：
    /// 1. 添加时序验证：只匹配最近完成摆轮动作的包裹
    /// 2. 使用 FIFO 队列：按摆轮执行顺序记录预期落格的包裹
    /// 3. 添加超时清理：清理长时间未落格的包裹记录，避免误匹配
    /// </remarks>
    private long? FindParcelByTargetChute(long targetChuteId)
    {
        // 从 _parcelTargetChutes 中查找目标格口匹配的包裹
        var matchingParcel = _parcelTargetChutes
            .FirstOrDefault(kvp => kvp.Value == targetChuteId);

        if (matchingParcel.Key == 0)
        {
            return null; // 未找到（ConcurrentDictionary.FirstOrDefault 返回 default(KeyValuePair) 时 Key=0）
        }

        return matchingParcel.Key;
    }

    /// <summary>
    /// 处理系统状态变更事件
    /// </summary>
    /// <remarks>
    /// 根据 CORE_ROUTING_LOGIC.md 规则：
    /// - 当系统状态转换到 Ready、EmergencyStop 或 Faulted 时，清空所有队列
    /// - 确保停止/急停/复位时不会残留任务影响后续操作
    /// </remarks>
    private void OnSystemStateChanged(object? sender, StateChangeEventArgs e)
    {
        _logger.LogInformation(
            "[系统状态变更] 检测到状态转换: {OldState} -> {NewState}",
            e.OldState,
            e.NewState);

        // 判断是否需要清空队列
        // 根据 CORE_ROUTING_LOGIC.md: "清空队列时机: 面板按钮（停止/急停/复位）时清空所有队列"
        // 对应状态转换：
        // - 任何状态 -> EmergencyStop (急停)
        // - Running/Paused -> Ready (停止)
        // - EmergencyStop -> Ready (急停解除/复位)
        // - Faulted -> Ready (故障恢复/复位)
        // - 任何状态 -> Faulted (故障)
        bool shouldClearQueues = e.NewState switch
        {
            SystemState.EmergencyStop => true,  // 急停时必须清空
            SystemState.Ready when e.OldState is SystemState.Running or SystemState.Paused => true,  // 从运行状态停止
            SystemState.Ready when e.OldState is SystemState.EmergencyStop or SystemState.Faulted => true,  // 复位时清空
            SystemState.Faulted => true,  // 故障时清空
            _ => false
        };

        if (shouldClearQueues)
        {
            if (_queueManager != null)
            {
                _logger.LogWarning(
                    "[队列清理] 系统状态转换到 {NewState}，正在清空所有 Position-Index 队列...",
                    e.NewState);

                _queueManager.ClearAllQueues();

                _logger.LogInformation(
                    "[队列清理] 队列清空完成，状态: {OldState} -> {NewState}",
                    e.OldState,
                    e.NewState);
            }
            else
            {
                _logger.LogDebug(
                    "[队列清理] 检测到应清空队列的状态转换 ({OldState} -> {NewState})，但 QueueManager 未注入",
                    e.OldState,
                    e.NewState);
            }

            // 需求1: 清空中位数统计数据
            if (_intervalTracker != null)
            {
                _logger.LogWarning(
                    "[中位数清理] 系统状态转换到 {NewState}，正在清空所有 Position 的中位数统计数据...",
                    e.NewState);

                _intervalTracker.ClearAllStatistics();

                _logger.LogInformation(
                    "[中位数清理] 中位数统计数据清空完成，状态: {OldState} -> {NewState}",
                    e.OldState,
                    e.NewState);
            }
            else
            {
                _logger.LogDebug(
                    "[中位数清理] 检测到应清空统计数据的状态转换 ({OldState} -> {NewState})，但 IntervalTracker 未注入",
                    e.OldState,
                    e.NewState);
            }
        }
        else
        {
            _logger.LogDebug(
                "[队列清理] 状态转换 {OldState} -> {NewState} 无需清空队列",
                e.OldState,
                e.NewState);
        }
    }

    /// <summary>
    /// 处理格口分配通知
    /// </summary>
    private async void OnChuteAssignmentReceived(object? sender, ChuteAssignmentEventArgs e)
    {
        await Task.Yield();
        try
        {
            var receivedAt = _clock.LocalNow;

            // 构建日志消息，如果有条码则包含在日志中
            var barcode = e.DwsPayload?.Barcode;
            var hasBarcode = !string.IsNullOrEmpty(barcode);

            var logMessage = hasBarcode
                ? "[格口分配-接收] 收到包裹 {ParcelId} 的格口分配通知 | ChuteId={ChuteId} | 条码={Barcode} | 接收时间={ReceivedAt:HH:mm:ss.fff}"
                : "[格口分配-接收] 收到包裹 {ParcelId} 的格口分配通知 | ChuteId={ChuteId} | 接收时间={ReceivedAt:HH:mm:ss.fff}";

            object[] logArgs = hasBarcode
                ? new object[] { e.ParcelId, e.ChuteId, barcode!, receivedAt }
                : new object[] { e.ParcelId, e.ChuteId, receivedAt };

            _logger.LogInformation(logMessage, logArgs);

            // 缓存条码（如果存在）用于后续日志追踪
            if (hasBarcode)
            {
                _parcelBarcodes.TryAdd(e.ParcelId, barcode!);
            }

            // Invariant 2 - 上游响应必须匹配已存在的本地包裹
            // 使用 TryGetValue 避免 ContainsKey + 索引器的重复查找
            if (!_createdParcels.TryGetValue(e.ParcelId, out var parcelRecord))
            {
                _logger.LogError(
                    "[Invariant Violation] 收到未知包裹 {ParcelId} 的路由响应 (ChuteId={ChuteId})，" +
                    "本地不存在此包裹实体。响应已丢弃，不创建幽灵包裹。",
                    e.ParcelId,
                    e.ChuteId);
                return;
            }

            // 记录上游响应接收时间
            // 注意：对 ConcurrentDictionary 中对象的属性赋值不是原子操作
            // 假设：每个包裹的格口分配通知只会收到一次，不会有并发修改同一包裹记录的情况
            parcelRecord.UpstreamReplyReceivedAt = new DateTimeOffset(receivedAt);

            // ⚠️ 关键修复（PR-UPSTREAM-TIMEOUT-FIX）：先完成TCS解除超时等待，再异步更新RoutePlan
            // 问题根因：UpdateRoutePlanWithChuteAssignmentAsync 包含数据库操作，如果耗时超过剩余超时时间，
            //         会导致超时处理器在 TCS 完成前触发，将包裹路由到异常格口。
            //
            // 修复策略：
            // 1. 立即完成 TCS，解除 GetChuteFromUpstreamAsync 的等待，防止超时
            // 2. 在后台异步更新 RoutePlan，不阻塞主流程
            // 3. 即使 RoutePlan 更新失败，包裹仍能正常分拣（格口已通过TCS传递）
            //
            // 时序保证：
            // - 步骤3"确定目标格口"已经从上游获取到 chuteId（通过TCS）
            // - 步骤5"生成队列任务"使用该 chuteId 生成路径和任务
            // - RoutePlan 主要用于历史记录和追溯，不是分拣流程的关键路径

            if (_pendingAssignments.TryGetValue(e.ParcelId, out var tcs))
            {
                // 正常情况：在超时前收到响应，立即完成等待任务
                _logger.LogInformation(
                    "[格口分配-接收成功] 包裹 {ParcelId} 成功分配到格口 {ChuteId}，立即完成TCS解除超时等待",
                    e.ParcelId,
                    e.ChuteId);

                // 记录路由绑定时间
                parcelRecord.RouteBoundAt = new DateTimeOffset(receivedAt);

                // 记录路由绑定完成的 Trace 日志
                _logger.LogTrace(
                    "[Parcel-First] 路由绑定完成: ParcelId={ParcelId}, ChuteId={ChuteId}, " +
                    "时间顺序: Created={CreatedAt:o} -> RequestSent={RequestAt:o} -> ReplyReceived={ReplyAt:o} -> RouteBound={BoundAt:o}",
                    e.ParcelId,
                    e.ChuteId,
                    parcelRecord.CreatedAt,
                    parcelRecord.UpstreamRequestSentAt,
                    parcelRecord.UpstreamReplyReceivedAt,
                    parcelRecord.RouteBoundAt);

                // ⚠️ 关键：立即完成TCS，解除GetChuteFromUpstreamAsync的超时等待
                var taskCompleted = tcs.TrySetResult(e.ChuteId);

                var barcodeSuffix = GetBarcodeSuffix(e.ParcelId);
                _logger.LogDebug(
                    "[格口分配-TCS完成] 包裹 {ParcelId}{BarcodeSuffix} 的TaskCompletionSource{Result}",
                    e.ParcelId,
                    barcodeSuffix,
                    taskCompleted ? "已成功设置结果" : "设置结果失败（可能已被取消或超时）");

                // 在后台异步更新 RoutePlan（不阻塞主流程）
                // 使用 SafeExecutionService 将数据库操作移到后台执行
                if (_safeExecutor != null)
                {
                    _ = _safeExecutor.ExecuteAsync(
                        async () =>
                        {
                            try
                            {
                                await UpdateRoutePlanWithChuteAssignmentAsync(e.ParcelId, e.ChuteId, e.AssignedAt);

                                var barcodeSuffixInner = GetBarcodeSuffix(e.ParcelId);
                                _logger.LogDebug(
                                    "[格口分配-RoutePlan已更新] 包裹 {ParcelId}{BarcodeSuffix} 的RoutePlan已成功更新为格口 {ChuteId}",
                                    e.ParcelId,
                                    barcodeSuffixInner,
                                    e.ChuteId);
                            }
                            catch (Exception ex)
                            {
                                var barcodeSuffixInner = GetBarcodeSuffix(e.ParcelId);
                                _logger.LogError(
                                    ex,
                                    "[格口分配-RoutePlan更新失败] 更新包裹 {ParcelId}{BarcodeSuffix} 的RoutePlan时发生错误 (ChuteId={ChuteId})",
                                    e.ParcelId,
                                    barcodeSuffixInner,
                                    e.ChuteId);
                            }
                        },
                        operationName: "SortingOrchestrator.OnChuteAssignmentReceived_RoutePlanUpdate");
                }
                else
                {
                    // 降级：如果 SafeExecutionService 不可用，使用 Task.Run
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await UpdateRoutePlanWithChuteAssignmentAsync(e.ParcelId, e.ChuteId, e.AssignedAt);

                            var barcodeSuffixInner = GetBarcodeSuffix(e.ParcelId);
                            _logger.LogDebug(
                                "[格口分配-RoutePlan已更新] 包裹 {ParcelId}{BarcodeSuffix} 的RoutePlan已成功更新为格口 {ChuteId}",
                                e.ParcelId,
                                barcodeSuffixInner,
                                e.ChuteId);
                        }
                        catch (Exception ex)
                        {
                            var barcodeSuffixInner = GetBarcodeSuffix(e.ParcelId);
                            _logger.LogError(
                                ex,
                                "[格口分配-RoutePlan更新失败] 更新包裹 {ParcelId}{BarcodeSuffix} 的RoutePlan时发生错误 (ChuteId={ChuteId})",
                                e.ParcelId,
                                barcodeSuffixInner,
                                e.ChuteId);
                        }
                    });
                }

                // 不在此处移除TCS，由GetChuteFromUpstreamAsync的finally块统一清理
            }
            else
            {
                // 迟到的响应：包裹已经超时并被路由到异常口
                _logger.LogWarning(
                    "【迟到路由响应】收到包裹 {ParcelId} 的格口分配 (ChuteId={ChuteId})，" +
                    "但该包裹已因超时被路由到异常口（_pendingAssignments中未找到对应的TCS）。" +
                    "接收时间={ReceivedAt:yyyy-MM-dd HH:mm:ss.fff}，将在后台更新RoutePlan记录正确的目标格口",
                    e.ParcelId,
                    e.ChuteId,
                    receivedAt);

                // 即使迟到，仍然在后台更新 RoutePlan 以保留正确的历史记录
                // 使用 SafeExecutionService 包裹后台任务
                if (_safeExecutor != null)
                {
                    _ = _safeExecutor.ExecuteAsync(
                        async () =>
                        {
                            try
                            {
                                await UpdateRoutePlanWithChuteAssignmentAsync(e.ParcelId, e.ChuteId, e.AssignedAt);

                                var barcodeSuffixInner = GetBarcodeSuffix(e.ParcelId);
                                _logger.LogDebug(
                                    "[迟到响应-RoutePlan已更新] 包裹 {ParcelId}{BarcodeSuffix} 的RoutePlan已更新为格口 {ChuteId}（虽然实际已路由到异常口）",
                                    e.ParcelId,
                                    barcodeSuffixInner,
                                    e.ChuteId);
                            }
                            catch (Exception ex)
                            {
                                var barcodeSuffixInner = GetBarcodeSuffix(e.ParcelId);
                                _logger.LogError(
                                    ex,
                                    "[迟到响应-RoutePlan更新失败] 更新包裹 {ParcelId}{BarcodeSuffix} 的RoutePlan时发生错误 (ChuteId={ChuteId})",
                                    e.ParcelId,
                                    barcodeSuffixInner,
                                    e.ChuteId);
                            }
                        },
                        operationName: "SortingOrchestrator.OnChuteAssignmentReceived_LateRoutePlanUpdate");
                }
                else
                {
                    // 降级：如果 SafeExecutionService 不可用，使用 Task.Run
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await UpdateRoutePlanWithChuteAssignmentAsync(e.ParcelId, e.ChuteId, e.AssignedAt);

                            var barcodeSuffixInner = GetBarcodeSuffix(e.ParcelId);
                            _logger.LogDebug(
                                "[迟到响应-RoutePlan已更新] 包裹 {ParcelId}{BarcodeSuffix} 的RoutePlan已更新为格口 {ChuteId}（虽然实际已路由到异常口）",
                                e.ParcelId,
                                barcodeSuffixInner,
                                e.ChuteId);
                        }
                        catch (Exception ex)
                        {
                            var barcodeSuffixInner = GetBarcodeSuffix(e.ParcelId);
                            _logger.LogError(
                                ex,
                                "[迟到响应-RoutePlan更新失败] 更新包裹 {ParcelId}{BarcodeSuffix} 的RoutePlan时发生错误 (ChuteId={ChuteId})",
                                e.ParcelId,
                                barcodeSuffixInner,
                                e.ChuteId);
                        }
                    });
                }
            }
        }
        catch (Exception ex)
        {
            // 捕获所有未处理的异常，防止 async void 方法导致应用崩溃
            _logger.LogCritical(
                ex,
                "[格口分配-严重错误] OnChuteAssignmentReceived 发生未处理异常: ParcelId={ParcelId}, ChuteId={ChuteId}",
                e?.ParcelId ?? 0,
                e?.ChuteId ?? 0);
        }
    }

    /// <summary>
    /// 更新 RoutePlan 中的目标格口
    /// </summary>
    /// <remarks>
    /// 当收到上游的格口分配通知时，需要更新包裹的路由计划，保存分配的格口信息。
    /// 如果 RoutePlan 不存在，则创建新的路由计划。
    /// </remarks>
    private async Task UpdateRoutePlanWithChuteAssignmentAsync(long parcelId, long chuteId, DateTimeOffset assignedAt)
    {
        if (_routePlanRepository == null)
        {
            _logger.LogWarning(
                "RoutePlanRepository 未注入，无法保存包裹 {ParcelId} 的格口分配 (ChuteId={ChuteId})",
                parcelId,
                chuteId);
            return;
        }

        try
        {
            // 获取或创建 RoutePlan
            var routePlan = await _routePlanRepository.GetByParcelIdAsync(parcelId);

            if (routePlan == null)
            {
                // 创建新的 RoutePlan
                routePlan = new RoutePlan(parcelId, chuteId, assignedAt);

                var barcodeSuffix = GetBarcodeSuffix(parcelId);
                _logger.LogInformation(
                    "创建新的路由计划: ParcelId={ParcelId}{BarcodeSuffix}, TargetChuteId={ChuteId}, AssignedAt={AssignedAt:yyyy-MM-dd HH:mm:ss.fff}",
                    parcelId,
                    barcodeSuffix,
                    chuteId,
                    assignedAt);
            }
            else
            {
                // 如果 RoutePlan 已存在，检查是否需要更新格口
                if (routePlan.CurrentTargetChuteId != chuteId)
                {
                    _logger.LogInformation(
                        "包裹 {ParcelId} 的目标格口从 {OldChuteId} 更新为 {NewChuteId}",
                        parcelId,
                        routePlan.CurrentTargetChuteId,
                        chuteId);

                    // 使用改口逻辑更新格口
                    var result = routePlan.TryApplyChuteChange(chuteId, assignedAt, out var decision);

                    if (!result.IsSuccess)
                    {
                        _logger.LogWarning(
                            "包裹 {ParcelId} 改口失败: {Reason}，决策结果={Outcome}",
                            parcelId,
                            result.ErrorMessage,
                            decision.Outcome);
                        return;
                    }
                }
                else
                {
                    _logger.LogDebug(
                        "包裹 {ParcelId} 的目标格口 {ChuteId} 未改变，无需更新",
                        parcelId,
                        chuteId);
                }
            }

            // 保存 RoutePlan
            await _routePlanRepository.SaveAsync(routePlan);

            var barcodeSuffixFinal = GetBarcodeSuffix(parcelId);
            _logger.LogDebug(
                "成功保存包裹 {ParcelId}{BarcodeSuffix} 的路由计划，目标格口={ChuteId}",
                parcelId,
                barcodeSuffixFinal,
                chuteId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "更新包裹 {ParcelId} 的路由计划时发生错误 (ChuteId={ChuteId})",
                parcelId,
                chuteId);
        }
    }

    #endregion Event Handlers - 事件处理

    #region 服务端事件订阅辅助方法（反射）

    /// <summary>
    /// 使用反射订阅服务端的 ChuteAssigned 事件
    /// </summary>
    /// <param name="server">服务端对象（实际类型为 IRuleEngineServer）</param>
    /// <param name="handlerMethodName">事件处理方法名称</param>
    /// <remarks>
    /// 此方法使用反射以避免 Execution 层引用 Communication 层。
    /// 缺点：失去编译时类型安全，事件名称或签名变更会导致运行时静默失败。
    /// </remarks>
    private void SubscribeToChuteAssignedEvent(object server, string handlerMethodName)
    {
        var serverType = server.GetType();
        var chuteAssignedEvent = serverType.GetEvent("ChuteAssigned");
        if (chuteAssignedEvent == null)
        {
            _logger.LogWarning(
                "无法在服务端类型 {ServerType} 上找到 ChuteAssigned 事件",
                serverType.FullName);
            return;
        }

        var handlerMethodInfo = typeof(SortingOrchestrator).GetMethod(
            handlerMethodName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        if (handlerMethodInfo == null)
        {
            _logger.LogWarning(
                "无法找到事件处理方法 {MethodName}",
                handlerMethodName);
            return;
        }

        try
        {
            var handlerDelegate = Delegate.CreateDelegate(
                chuteAssignedEvent.EventHandlerType!,
                this,
                handlerMethodInfo);
            chuteAssignedEvent.AddEventHandler(server, handlerDelegate);

            _logger.LogDebug(
                "成功订阅服务端的 ChuteAssigned 事件，处理器={HandlerMethod}",
                handlerMethodName);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "订阅服务端 ChuteAssigned 事件失败，处理器={HandlerMethod}",
                handlerMethodName);
        }
    }

    /// <summary>
    /// 使用反射取消订阅服务端的 ChuteAssigned 事件
    /// </summary>
    /// <param name="server">服务端对象（实际类型为 IRuleEngineServer）</param>
    /// <param name="handlerMethodName">事件处理方法名称</param>
    private void UnsubscribeFromChuteAssignedEvent(object server, string handlerMethodName)
    {
        var serverType = server.GetType();
        var chuteAssignedEvent = serverType.GetEvent("ChuteAssigned");
        if (chuteAssignedEvent == null)
        {
            return;
        }

        var handlerMethodInfo = typeof(SortingOrchestrator).GetMethod(
            handlerMethodName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        if (handlerMethodInfo == null)
        {
            return;
        }

        try
        {
            var handlerDelegate = Delegate.CreateDelegate(
                chuteAssignedEvent.EventHandlerType!,
                this,
                handlerMethodInfo);
            chuteAssignedEvent.RemoveEventHandler(server, handlerDelegate);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "取消订阅服务端 ChuteAssigned 事件失败，处理器={HandlerMethod}",
                handlerMethodName);
        }
    }

    #endregion 服务端事件订阅辅助方法（反射）

    #region RoutePlan 更新逻辑

    /// <summary>
    /// 更新 RoutePlan 中的目标格口
    /// </summary>
    private async ValueTask WriteTraceAsync(ParcelTraceEventArgs eventArgs)
    {
        if (_traceSink != null)
        {
            await _traceSink.WriteAsync(eventArgs);
        }
    }

    #endregion RoutePlan 更新逻辑

    /// <summary>
    /// 处理超时包裹（路由到异常格口）
    /// </summary>
    public async Task ProcessTimedOutParcelAsync(long parcelId, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("包裹 {ParcelId} 等待超时未到达摆轮，准备路由到异常格口", parcelId);

        try
        {
            // 获取异常格口配置（同步方法）
            var systemConfig = _systemConfigRepository.Get();
            var exceptionChuteId = systemConfig.ExceptionChuteId;

            // 使用统一的异常处理器生成到异常格口的路径
            var path = _exceptionHandler.GenerateExceptionPath(
                exceptionChuteId,
                parcelId,
                "包裹等待超时未到达摆轮");

            if (path == null)
            {
                _logger.LogError(
                    "包裹 {ParcelId} 超时后无法生成到异常格口 {ChuteId} 的路径",
                    parcelId, exceptionChuteId);
                return;
            }

            // 执行到异常格口的分拣
            _logger.LogInformation(
                "包裹 {ParcelId} 开始执行超时兜底分拣，目标格口: {ChuteId}",
                parcelId, exceptionChuteId);

            var executionResult = await _pathExecutor.ExecuteAsync(path, cancellationToken);

            if (executionResult.IsSuccess)
            {
                _logger.LogInformation(
                    "超时包裹 {ParcelId} 已成功路由到异常格口 {ChuteId}",
                    parcelId, exceptionChuteId);

                // 记录成功指标（执行时间待 PathExecutionResult 扩展后再传入）
                RecordSortingSuccess(0);

                // 发送落格完成通知到上游系统
                var notification = new SortingCompletedNotification
                {
                    ParcelId = parcelId,
                    ActualChuteId = exceptionChuteId,
                    CompletedAt = new DateTimeOffset(_clock.LocalNow),
                    // IsSuccess 表示"是否成功到达目标格口"。根据 UPSTREAM_CONNECTION_GUIDE.md，
                    // 虽然超时后成功路由到异常格口属于系统已妥善处理，但本字段仅在包裹到达预期目标格口时为 true。
                    // 路由到异常格口（如因超时）视为"未达目标"，因此 IsSuccess=false，FinalStatus=Timeout。
                    IsSuccess = false,
                    FinalStatus = Core.Enums.Parcel.ParcelFinalStatus.Timeout,
                    FailureReason = "包裹等待超时未到达摆轮"
                };

                var notificationSent = await _upstreamClient.SendAsync(new SortingCompletedMessage { Notification = notification }, cancellationToken);

                if (!notificationSent)
                {
                    _logger.LogWarning(
                        "超时包裹 {ParcelId} 落格完成通知发送失败",
                        parcelId);
                }
                else
                {
                    _logger.LogInformation(
                        "超时包裹 {ParcelId} 已发送落格完成通知到上游系统 (FinalStatus=Timeout)",
                        parcelId);
                }
            }
            else
            {
                _logger.LogError(
                    "超时包裹 {ParcelId} 路由到异常格口 {ChuteId} 失败: {FailureReason}",
                    parcelId, exceptionChuteId, executionResult.FailureReason);

                // 记录失败指标

                // 即使路由失败也发送通知到上游
                // 根因是超时，执行失败是次要问题，因此 FinalStatus 仍为 Timeout
                var notification = new SortingCompletedNotification
                {
                    ParcelId = parcelId,
                    ActualChuteId = exceptionChuteId,
                    CompletedAt = new DateTimeOffset(_clock.LocalNow),
                    IsSuccess = false,
                    FinalStatus = Core.Enums.Parcel.ParcelFinalStatus.Timeout,
                    FailureReason = $"超时后路由到异常格口失败: {executionResult.FailureReason}"
                };

                await _upstreamClient.SendAsync(new SortingCompletedMessage { Notification = notification }, cancellationToken);
            }

            // 记录拥堵数据（如果启用）
            _congestionCollector?.RecordParcelCompletion(parcelId, _clock.LocalNow, executionResult.IsSuccess);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理超时包裹 {ParcelId} 时发生异常", parcelId);
        }
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        // 取消订阅事件
        _sensorEventProvider.ParcelDetected -= OnParcelDetected;
        _sensorEventProvider.DuplicateTriggerDetected -= OnDuplicateTriggerDetected;
        _sensorEventProvider.ChuteDropoffDetected -= OnChuteDropoffDetected;
        // PR-UPSTREAM02: 从 ChuteAssignmentReceived 改为 ChuteAssigned
        _upstreamClient.ChuteAssigned -= OnChuteAssignmentReceived;

        // 取消订阅服务端模式的格口分配事件（如果存在）
        if (_upstreamServer != null)
        {
            UnsubscribeFromChuteAssignedEvent(_upstreamServer, nameof(OnChuteAssignmentReceived));
        }

        // 取消订阅系统状态变更事件
        _systemStateManager.StateChanged -= OnSystemStateChanged;

        // TD-LOSS-ORCHESTRATOR-001: 取消订阅包裹丢失事件
        if (_lossMonitoringService != null)
        {
            _lossMonitoringService.ParcelLostDetected -= OnParcelLostDetectedAsync;
        }

        // 断开连接
        StopAsync().GetAwaiter().GetResult();
    }

    #region 包裹丢失处理 (TD-LOSS-ORCHESTRATOR-001)

    /// <summary>
    /// 处理包裹丢失事件
    /// </summary>
    /// <remarks>
    /// 当 ParcelLossMonitoringService 检测到包裹丢失时触发此方法。
    ///
    /// 处理流程：
    /// 1. 从所有队列删除丢失包裹的任务
    /// 2. 将受影响的包裹（在丢失包裹创建之后、丢失检测之前创建的包裹）的任务方向改为直行
    /// 3. 上报丢失包裹到上游（包含受影响包裹信息）
    /// 4. 清理丢失包裹的本地记录
    ///
    /// <b>受影响包裹的判定规则：</b>
    /// - 包裹创建时间 > 丢失包裹创建时间
    /// - 包裹创建时间 < 丢失检测时间
    /// - 这些包裹的任务方向会被改为直行（Straight），以导向异常格口
    ///
    /// <b>不受影响的包裹：</b>
    /// - 在丢失包裹创建之前创建的包裹（保持原方向）
    /// - 在丢失检测之后创建的包裹（保持原方向）
    ///
    /// <b>关于 async void 的使用说明：</b>
    /// 此方法使用 async void 是因为它是一个事件处理器，必须匹配 EventHandler 委托签名。
    /// 所有异步操作都包裹在 SafeExecutionService 中，确保异常不会导致应用程序崩溃。
    /// SafeExecutionService 会捕获并记录所有未处理的异常。
    /// </remarks>
    private async void OnParcelLostDetectedAsync(object? sender, Core.Events.Queue.ParcelLostEventArgs e)
    {
        if (_safeExecutor == null)
        {
            _logger.LogWarning(
                "[包裹丢失] 未配置 SafeExecutionService，无法安全处理丢失事件 (ParcelId={ParcelId})",
                e.LostParcelId);
            return;
        }

        _logger.LogError(
            "[生命周期-丢失] P{ParcelId} Pos{Position}丢失 延迟{DelayMs}ms 阈值{ThresholdMs}ms",
            e.LostParcelId,
            e.DetectedAtPositionIndex,
            e.DelayMs,
            e.LostThresholdMs);

        // ⚠️ 关键修复：优先取消待处理的格口分配，再执行其他操作
        // 原代码问题：CleanupParcelMemory在SafeExecutor异步回调的最后才执行，
        // 可能导致上游格口分配到达时，TCS已被移除但其他清理操作尚未完成

        // 1. 立即取消待处理的格口分配（如果存在）
        if (_pendingAssignments.TryRemove(e.LostParcelId, out var tcs))
        {
            // 尝试取消TCS（如果尚未完成）
            bool wasCancelled = tcs.TrySetCanceled();

            if (wasCancelled)
            {
                _logger.LogWarning(
                    "[包裹丢失-取消分配] 包裹 {ParcelId} 丢失，已取消待处理的格口分配请求",
                    e.LostParcelId);
            }
            else
            {
                _logger.LogWarning(
                    "[包裹丢失-取消分配] 包裹 {ParcelId} 丢失，发现TCS已完成，跳过取消操作",
                    e.LostParcelId);
            }
        }

        // 2. 立即移除已创建的包裹记录，避免后续回调在已清理的实体上继续操作
        // 这可以防止 OnChuteAssignmentReceived 在验证通过后尝试更新已删除的包裹记录
        if (_createdParcels.TryRemove(e.LostParcelId, out _))
        {
            _logger.LogInformation(
                "[包裹丢失-清理创建记录] 已从 _createdParcels 中移除包裹 {ParcelId} 的创建记录",
                e.LostParcelId);
        }

        // 3. 异步执行其他清理操作（不阻塞主流程）
        await _safeExecutor.ExecuteAsync(
            async () =>
            {
                // 3. 从所有队列删除丢失包裹的任务
                int removedTasks = 0;
                if (_queueManager != null)
                {
                    removedTasks = _queueManager.RemoveAllTasksForParcel(e.LostParcelId);
                    _logger.LogInformation(
                        "[包裹丢失-清理队列] 已从所有队列移除包裹 {ParcelId} 的 {Count} 个任务",
                        e.LostParcelId, removedTasks);
                }

                // 4. 将受影响的包裹（在丢失包裹创建之后、丢失检测之前创建的包裹）的任务方向改为直行
                List<long> affectedParcelIds = new List<long>();
                if (_queueManager != null && e.ParcelCreatedAt.HasValue)
                {
                    affectedParcelIds = _queueManager.UpdateAffectedParcelsToStraight(
                        e.ParcelCreatedAt.Value,
                        e.DetectedAt);

                    if (affectedParcelIds.Count > 0)
                    {
                        _logger.LogWarning(
                            "[包裹丢失影响] 包裹 {LostParcelId} 丢失影响了 {Count} 个包裹: [{AffectedIds}]，" +
                            "这些包裹的任务已改为直行",
                            e.LostParcelId,
                            affectedParcelIds.Count,
                            string.Join(", ", affectedParcelIds));
                    }
                }

                // 5. 上报丢失包裹到上游（包含受影响包裹信息）
                await NotifyUpstreamParcelLostAsync(e, affectedParcelIds);

                // 6. 记录指标
                {
                    // 记录丢失包裹
                }

                // 记录失败率和统计数据
                _alarmService?.RecordSortingFailure(); // 丢失算作失败
                _statisticsService?.IncrementLost(); // 增加丢失计数

                // 记录受影响的包裹数量
                if (affectedParcelIds.Count > 0)
                {
                    _statisticsService?.IncrementAffected(affectedParcelIds.Count);
                }

                // 7. 清理丢失包裹的其他内存记录（_pendingAssignments和_createdParcels已在上面立即处理）
                _parcelTargetChutes.TryRemove(e.LostParcelId, out _);
                _parcelPaths.TryRemove(e.LostParcelId, out _);
                _parcelBarcodes.TryRemove(e.LostParcelId, out _);
                _timeoutCompensationInserted.TryRemove(e.LostParcelId, out _);
                _intervalTracker?.ClearParcelTracking(e.LostParcelId);

                _logger.LogTrace(
                    "[包裹丢失-内存清理] 已清理包裹 {ParcelId} 在内存中的其他痕迹（目标格口、路径、超时标记、位置追踪）",
                    e.LostParcelId);
            },
            operationName: "HandleParcelLost",
            cancellationToken: CancellationToken.None);
    }

    /// <summary>
    /// 清理包裹在内存中的所有痕迹
    /// </summary>
    /// <param name="parcelId">包裹ID</param>
    /// <remarks>
    /// 在包裹完成分拣或丢失时调用，确保彻底清理包裹的所有内存记录：
    /// <list type="bullet">
    ///   <item>创建记录 (_createdParcels)</item>
    ///   <item>目标格口映射 (_parcelTargetChutes)</item>
    ///   <item>路径信息 (_parcelPaths)</item>
    ///   <item>待处理分配 (_pendingAssignments)</item>
    ///   <item>超时补偿标记 (_timeoutCompensationInserted)</item>
    ///   <item>位置追踪记录 (_intervalTracker)</item>
    ///   <item>条码缓存 (_parcelBarcodes)</item>
    /// </list>
    /// </remarks>
    private void CleanupParcelMemory(long parcelId)
    {
        _createdParcels.TryRemove(parcelId, out _);
        _parcelTargetChutes.TryRemove(parcelId, out _);
        _parcelPaths.TryRemove(parcelId, out _);
        _pendingAssignments.TryRemove(parcelId, out _);
        _timeoutCompensationInserted.TryRemove(parcelId, out _);
        _parcelBarcodes.TryRemove(parcelId, out _);
        _intervalTracker?.ClearParcelTracking(parcelId);

        _logger.LogTrace(
            "已清理包裹 {ParcelId} 在内存中的所有痕迹（创建记录、目标格口、路径、待处理分配、超时标记、位置追踪）",
            parcelId);
    }

    /// <summary>
    /// 通知上游系统包裹丢失
    /// </summary>
    /// <param name="e">包裹丢失事件参数</param>
    /// <param name="affectedParcelIds">受影响的包裹ID列表</param>
    private async Task NotifyUpstreamParcelLostAsync(
        Core.Events.Queue.ParcelLostEventArgs e,
        List<long> affectedParcelIds)
    {
        _logger.LogWarning(
            "[上游通知-准备] 即将向上游发送包裹 {ParcelId} 丢失通知 (Position={Position}, 受影响包裹数={AffectedCount})",
            e.LostParcelId,
            e.DetectedAtPositionIndex,
            affectedParcelIds.Count);

        try
        {
            var systemConfig = _systemConfigRepository.Get();

            // 构建失败原因，包含受影响包裹信息
            var failureReason = $"包裹在 Position {e.DetectedAtPositionIndex} 丢失 " +
                               $"(延迟={e.DelayMs:F0}ms, 阈值={e.LostThresholdMs:F0}ms)";

            if (affectedParcelIds.Count > 0)
            {
                failureReason += $"，影响了 {affectedParcelIds.Count} 个包裹: [{string.Join(", ", affectedParcelIds)}]";
            }

            var notification = new SortingCompletedNotification
            {
                ParcelId = e.LostParcelId,
                ActualChuteId = systemConfig.ExceptionChuteId, // 丢失包裹标记为异常口
                CompletedAt = new DateTimeOffset(e.DetectedAt),
                IsSuccess = false,
                FinalStatus = Core.Enums.Parcel.ParcelFinalStatus.Lost,
                FailureReason = failureReason,
                AffectedParcelIds = affectedParcelIds.Count > 0 ? affectedParcelIds.AsReadOnly() : null // 结构化发送受影响包裹列表
            };

            _logger.LogTrace(
                "[生命周期-丢失] P{ParcelId} 准备上报上游 (C{ExceptionChuteId})",
                e.LostParcelId,
                systemConfig.ExceptionChuteId);

            await _upstreamClient.SendAsync(
                new SortingCompletedMessage { Notification = notification });

            if (affectedParcelIds.Count > 0)
            {
                _logger.LogWarning(
                    "[生命周期-丢失] P{ParcelId} 上报上游✅ 影响{Count}个包裹 [{AffectedIds}]",
                    e.LostParcelId,
                    affectedParcelIds.Count,
                    string.Join(",", affectedParcelIds));
            }
            else
            {
                _logger.LogWarning(
                    "[生命周期-丢失] P{ParcelId} 上报上游✅ 无其他包裹受影响",
                    e.LostParcelId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[生命周期-丢失] P{ParcelId} 上报上游失败❌: {Message}",
                e.LostParcelId,
                ex.Message);

            // 重要：即使发送失败也要记录，不要静默失败
            throw; // 重新抛出异常，让 SafeExecutionService 记录
        }
    }

    #endregion 包裹丢失处理 (TD-LOSS-ORCHESTRATOR-001)

    #region Helper Methods - 性能优化辅助方法

    /// <summary>
    /// 构建传感器到位置的映射缓存（Issue #2: 避免热路径重复查询）
    /// </summary>
    /// <remarks>
    /// 此方法在服务启动时调用，构建传感器ID到位置的映射缓存。
    /// 
    /// <para><b>缓存失效注意事项</b>：</para>
    /// <list type="bullet">
    ///   <item>如果传感器配置或拓扑在运行时更新，需要调用 <see cref="RebuildCaches"/> 方法重建缓存</item>
    ///   <item>缓存不会自动更新，必须手动调用重建方法或重启服务</item>
    ///   <item>缓存未更新时，会自动fallback到实时查询，但会损失性能优化效果</item>
    /// </list>
    /// </remarks>
    private void BuildSensorToPositionCache()
    {
        if (_sensorConfigRepository == null || _topologyRepository == null)
        {
            _logger.LogWarning("无法构建传感器位置缓存：缺少传感器配置或拓扑仓储");
            return;
        }

        try
        {
            var sensorConfig = _sensorConfigRepository.Get();
            var topology = _topologyRepository.Get();

            if (sensorConfig == null || topology == null)
            {
                _logger.LogWarning("无法构建传感器位置缓存：配置或拓扑为空");
                return;
            }

            _sensorToPositionCache.Clear();

            // 只缓存 WheelFront 类型的传感器
            var wheelFrontSensors = sensorConfig.Sensors
                .Where(s => s.IoType == SensorIoType.WheelFront)
                .ToList();

            foreach (var sensor in wheelFrontSensors)
            {
                var node = topology.DiverterNodes.FirstOrDefault(n => n.FrontSensorId == sensor.SensorId);
                if (node != null)
                {
                    _sensorToPositionCache[sensor.SensorId] = (node.DiverterId, node.PositionIndex);
                    _logger.LogDebug(
                        "缓存传感器映射: SensorId={SensorId} -> DiverterId={DiverterId}, PositionIndex={PositionIndex}",
                        sensor.SensorId, node.DiverterId, node.PositionIndex);
                }
            }

            _logger.LogInformation(
                "传感器位置缓存已构建，共 {Count} 个 WheelFront 传感器",
                _sensorToPositionCache.Count);

            // Issue #3: 同时构建后续节点缓存
            BuildSubsequentNodesCache(topology);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "构建传感器位置缓存时发生异常");
        }
    }

    /// <summary>
    /// 构建每个position的后续节点列表缓存（Issue #3: 优化超时补偿性能）
    /// </summary>
    /// <param name="topology">拓扑配置</param>
    private void BuildSubsequentNodesCache(ChutePathTopologyConfig topology)
    {
        if (topology == null || topology.DiverterNodes == null)
        {
            return;
        }

        _subsequentNodesCache.Clear();

        // 获取所有位置索引
        var allPositions = topology.DiverterNodes
            .Select(n => n.PositionIndex)
            .Distinct()
            .OrderBy(p => p)
            .ToList();

        // 为每个位置构建后续节点列表
        foreach (var position in allPositions)
        {
            var subsequentNodes = topology.DiverterNodes
                .Where(n => n.PositionIndex > position)
                .OrderBy(n => n.PositionIndex)
                .ToList();

            _subsequentNodesCache[position] = subsequentNodes;

            _logger.LogDebug(
                "缓存后续节点: Position={Position} -> {Count} 个后续节点",
                position, subsequentNodes.Count);
        }

        _logger.LogInformation(
            "后续节点缓存已构建，共 {Count} 个位置",
            _subsequentNodesCache.Count);
    }

    /// <summary>
    /// 重建所有性能缓存（Issue #2 & #3: 配置更新后的缓存失效处理）
    /// </summary>
    /// <remarks>
    /// <para><b>使用场景</b>：</para>
    /// <list type="bullet">
    ///   <item>传感器配置通过API动态更新后</item>
    ///   <item>拓扑配置通过API动态更新后</item>
    ///   <item>摆轮节点配置发生变化后</item>
    /// </list>
    /// 
    /// <para><b>注意事项</b>：</para>
    /// <list type="bullet">
    ///   <item>此方法是线程安全的（使用ConcurrentDictionary.Clear()）</item>
    ///   <item>重建期间可能有短暂的缓存未命中，会自动fallback到实时查询</item>
    ///   <item>建议在系统空闲时调用，避免重建期间的性能抖动</item>
    /// </list>
    /// </remarks>
    public void RebuildCaches()
    {
        _logger.LogInformation("开始重建性能缓存（传感器位置映射 + 后续节点列表）");
        BuildSensorToPositionCache();
        _logger.LogInformation("性能缓存重建完成");
    }

    /// <summary>
    /// 统一记录分拣失败（Issue #1: 避免重复告警噪声）
    /// </summary>
    /// <param name="elapsedSeconds">已用时间（秒）</param>
    /// <param name="isTimeout">是否为超时</param>
    private void RecordSortingFailure(double elapsedSeconds, bool isTimeout)
    {
        _alarmService?.RecordSortingFailure();
        if (isTimeout)
        {
            _statisticsService?.IncrementTimeout();
        }
    }

    /// <summary>
    /// 统一记录分拣成功（Issue #1: 避免重复代码）
    /// </summary>
    /// <param name="elapsedSeconds">已用时间（秒）</param>
    private void RecordSortingSuccess(double elapsedSeconds)
    {
        _alarmService?.RecordSortingSuccess();
        _statisticsService?.IncrementSuccess();
    }

    /// <summary>
    /// 动态更新下一个position的期望到达时间（避免误差累积）
    /// </summary>
    /// <param name="currentTask">当前触发的任务</param>
    /// <param name="currentPositionIndex">当前position索引</param>
    /// <param name="actualArrivalTime">实际到达时间</param>
    /// <remarks>
    /// <para>当包裹实际到达当前position时，根据实际到达时间重新计算下一个position的期望到达时间。</para>
    /// <para>这样可以避免基于初始计算时间的误差累积，使时间窗口更准确。</para>
    /// <para>计算公式：</para>
    /// <list type="bullet">
    ///   <item>NextExpectedArrivalTime = ActualArrivalTime + SegmentTransitTime</item>
    ///   <item>NextEarliestDequeueTime = Max(CreatedAt, NextExpectedArrivalTime - TimeoutThresholdMs)</item>
    /// </list>
    /// </remarks>
    private void UpdateNextPositionExpectedTime(PositionQueueItem currentTask, int currentPositionIndex, DateTime actualArrivalTime)
    {
        if (_queueManager == null || _topologyRepository == null || _segmentRepository == null)
        {
            return;
        }

        try
        {
            var topology = _topologyRepository.Get();
            if (topology == null)
            {
                return;
            }

            // 找到下一个position的节点
            var nextNode = topology.DiverterNodes
                .Where(n => n.PositionIndex == currentPositionIndex + 1)
                .FirstOrDefault();

            if (nextNode == null)
            {
                // 当前position是最后一个，没有下一个position
                return;
            }

            // 获取到下一个position的线段配置
            var segmentConfig = _segmentRepository.GetById(nextNode.SegmentId);
            if (segmentConfig == null)
            {
                _logger.LogDebug(
                    "无法更新下一个position的期望时间：线段配置不存在 (SegmentId={SegmentId})",
                    nextNode.SegmentId);
                return;
            }

            // 计算传输时间
            var transitTimeMs = segmentConfig.CalculateTransitTimeMs();
            var nextExpectedArrivalTime = actualArrivalTime.AddMilliseconds(transitTimeMs);
            var timeoutThresholdMs = segmentConfig.TimeToleranceMs;

            // 计算下一个position的最早出队时间
            var nextEarliestDequeueTime = nextExpectedArrivalTime.AddMilliseconds(-timeoutThresholdMs);
            if (nextEarliestDequeueTime < currentTask.CreatedAt)
            {
                nextEarliestDequeueTime = currentTask.CreatedAt;
            }

            // 窥视下一个position的队列头部任务
            var nextTask = _queueManager.PeekTask(nextNode.PositionIndex);
            if (nextTask == null || nextTask.ParcelId != currentTask.ParcelId)
            {
                // 队列中没有该包裹的下一个任务，不需要更新
                return;
            }

            // 出队旧任务
            // 注意：此处存在潜在的并发风险，如果在出队和入队之间另一个传感器触发，可能导致顺序错乱
            // 然而实际场景中，由于物理传感器的间隔和包裹移动的时序性，同一position的并发触发极为罕见
            // 如果未来需要解决此问题，可以考虑为每个positionIndex添加独立的锁
            var dequeuedTask = _queueManager.DequeueTask(nextNode.PositionIndex);
            if (dequeuedTask == null || dequeuedTask.ParcelId != currentTask.ParcelId)
            {
                // 出队失败或队列已变化，放弃更新
                if (dequeuedTask != null)
                {
                    // 如果出队了其他包裹的任务，放回队列
                    _queueManager.EnqueuePriorityTask(nextNode.PositionIndex, dequeuedTask);
                }
                return;
            }

            // 创建更新后的任务（仅更新时间相关字段）
            // 使用 record 的 with 表达式确保不可变性，以下字段被保留：
            // - ParcelId, DiverterId, DiverterAction, PositionIndex (核心业务字段)
            // - TimeoutThresholdMs, FallbackAction (超时处理配置)
            // - CreatedAt, LostDetectionTimeoutMs (原始时间戳和配置)
            // 以下字段被更新：
            // - ExpectedArrivalTime (基于实际到达时间重新计算)
            // - EarliestDequeueTime (基于新的期望时间重新计算)
            // - LostDetectionDeadline (基于新的期望时间重新计算，保持一致性)
            var updatedTask = dequeuedTask with
            {
                ExpectedArrivalTime = nextExpectedArrivalTime,
                EarliestDequeueTime = nextEarliestDequeueTime,
                // 也更新丢失检测截止时间，保持一致性
                LostDetectionDeadline = dequeuedTask.LostDetectionTimeoutMs.HasValue
                    ? nextExpectedArrivalTime.AddMilliseconds(dequeuedTask.LostDetectionTimeoutMs.Value)
                    : dequeuedTask.LostDetectionDeadline
            };

            // 将更新后的任务放回队列头部（优先级入队）
            _queueManager.EnqueuePriorityTask(nextNode.PositionIndex, updatedTask);

            _logger.LogDebug(
                "[动态时间更新] 包裹 {ParcelId} 在Position {CurrentPos}触发，" +
                "更新Position {NextPos}的期望到达时间: {OldTime:HH:mm:ss.fff} -> {NewTime:HH:mm:ss.fff}, " +
                "最早出队时间: {EarliestTime:HH:mm:ss.fff}",
                currentTask.ParcelId,
                currentPositionIndex,
                nextNode.PositionIndex,
                dequeuedTask.ExpectedArrivalTime,
                nextExpectedArrivalTime,
                nextEarliestDequeueTime);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "更新下一个position的期望时间时发生异常 (ParcelId={ParcelId}, CurrentPos={CurrentPos})",
                currentTask.ParcelId,
                currentPositionIndex);
        }
    }

    /// <summary>
    /// 获取指定包裹的条码后缀字符串，用于在日志和监控中进行精确追踪。
    /// </summary>
    /// <param name="parcelId">
    /// 本地分拣流程中使用的包裹 ID。该 ID 与上游 DWS 载荷中的条码通过缓存进行关联。
    /// </param>
    /// <returns>
    /// 若在条码缓存中能找到与 <paramref name="parcelId"/> 对应的非空条码，
    /// 则返回形如 <c>",BarCode:xxx"</c> 的后缀字符串；否则返回 <see cref="string.Empty"/>。
    /// </returns>
    /// <remarks>
    /// <para>
    /// 此方法仅用于<strong>日志追踪和问题排查</strong>，不会影响实际分拣决策或硬件控制逻辑。
    /// </para>
    /// <para>
    /// 条码在收到上游格口分配通知（<c>OnChuteAssignmentReceived</c>）时写入缓存：
    /// 当通知中携带的 DWS 载荷 (<c>DwsPayload</c>) 包含非空 <c>Barcode</c> 字段时，
    /// 分拣编排器会将 <c>parcelId</c> 与该条码写入内部的
    /// <see cref="ConcurrentDictionary{TKey,TValue}"/> 缓存字段 <c>_parcelBarcodes</c>。
    /// </para>
    /// <para>
    /// 条码会在包裹生命周期结束后的清理操作中从缓存中移除（例如分拣完成、异常结束或
    /// 显式的缓存清理任务），以避免缓存无限增长并保持内存占用可控。
    /// </para>
    /// <para>
    /// 由于缓存使用的是 <see cref="ConcurrentDictionary{TKey,TValue}"/>，因此
    /// <see cref="GetBarcodeSuffix(long)"/> 的查找操作是线程安全的，
    /// 可以在多个传感器回调 / 分拣任务并发执行的场景下安全调用。
    /// </para>
    /// </remarks>
    private string GetBarcodeSuffix(long parcelId)
    {
        if (_parcelBarcodes.TryGetValue(parcelId, out var barcode) && !string.IsNullOrEmpty(barcode))
        {
            return $",BarCode:{barcode}";
        }
        return string.Empty;
    }

    #endregion Helper Methods - 性能优化辅助方法
}
