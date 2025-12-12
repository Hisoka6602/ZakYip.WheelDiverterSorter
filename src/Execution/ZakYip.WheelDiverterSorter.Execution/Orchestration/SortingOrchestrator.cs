using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Execution;
using ZakYip.WheelDiverterSorter.Core.Enums.System;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Ingress;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Upstream;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Events.Sensor;
using ZakYip.WheelDiverterSorter.Core.Events.Sorting;
using ZakYip.WheelDiverterSorter.Core.LineModel;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;
using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using ZakYip.WheelDiverterSorter.Core.LineModel.Topology;
using ZakYip.WheelDiverterSorter.Core.Events.Monitoring;
using ZakYip.WheelDiverterSorter.Core.LineModel.Tracing;
using ZakYip.WheelDiverterSorter.Core.Sorting.Interfaces;
using ZakYip.WheelDiverterSorter.Core.Sorting.Models;
using ZakYip.WheelDiverterSorter.Core.Sorting.Orchestration;
using ZakYip.WheelDiverterSorter.Core.Sorting.Overload;
using ZakYip.WheelDiverterSorter.Core.Sorting.Policies;
using ZakYip.WheelDiverterSorter.Core.Sorting.Runtime;
using ZakYip.WheelDiverterSorter.Core.Sorting.Strategy;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Execution.Health;
using ZakYip.WheelDiverterSorter.Execution.PathExecution;
using ZakYip.WheelDiverterSorter.Execution.Queues;
using ZakYip.WheelDiverterSorter.Observability;
using ZakYip.WheelDiverterSorter.Observability.Utilities;
using ZakYip.WheelDiverterSorter.Core.Enums.Sorting;
using ZakYip.WheelDiverterSorter.Core.Enums.Monitoring;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;

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
    private readonly PrometheusMetrics? _metrics;
    private readonly IParcelTraceSink? _traceSink;
    private readonly PathHealthChecker? _pathHealthChecker;
    private readonly IChuteAssignmentTimeoutCalculator? _timeoutCalculator;
    private readonly ISortingExceptionHandler _exceptionHandler;
    private readonly IChuteSelectionService? _chuteSelectionService;
    
    // 新的 Position-Index 队列系统依赖
    private readonly IPositionIndexQueueManager? _queueManager;
    private readonly IChutePathTopologyRepository? _topologyRepository;
    private readonly IConveyorSegmentRepository? _segmentRepository;
    private readonly ISensorConfigurationRepository? _sensorConfigRepository;
    private readonly ISafeExecutionService? _safeExecutor;
    
    // 包裹路由相关的状态 - 使用线程安全集合 (PR-44)
    private readonly ConcurrentDictionary<long, TaskCompletionSource<long>> _pendingAssignments;
    private readonly ConcurrentDictionary<long, SwitchingPath> _parcelPaths;
    private readonly ConcurrentDictionary<long, ParcelCreationRecord> _createdParcels; // PR-42: Track created parcels
    private readonly object _lockObject = new object(); // 保留用于 RoundRobin 索引和连接状态
    private int _roundRobinIndex = 0;
    private bool _isConnected;

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
        PrometheusMetrics? metrics = null,
        IParcelTraceSink? traceSink = null,
        PathHealthChecker? pathHealthChecker = null,
        IChuteAssignmentTimeoutCalculator? timeoutCalculator = null,
        IChuteSelectionService? chuteSelectionService = null,
        IPositionIndexQueueManager? queueManager = null, // 新的 Position-Index 队列管理器
        IChutePathTopologyRepository? topologyRepository = null, // TD-062: 拓扑配置仓储
        IConveyorSegmentRepository? segmentRepository = null, // TD-062: 线体段配置仓储
        ISensorConfigurationRepository? sensorConfigRepository = null, // TD-062: 传感器配置仓储
        ISafeExecutionService? safeExecutor = null) // TD-062: 安全执行服务
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
        _metrics = metrics;
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
        
        _pendingAssignments = new ConcurrentDictionary<long, TaskCompletionSource<long>>();
        _parcelPaths = new ConcurrentDictionary<long, SwitchingPath>();
        _createdParcels = new ConcurrentDictionary<long, ParcelCreationRecord>();

        // 订阅包裹检测事件
        _sensorEventProvider.ParcelDetected += OnParcelDetected;
        _sensorEventProvider.DuplicateTriggerDetected += OnDuplicateTriggerDetected;
        
        // PR-UPSTREAM02: 订阅格口分配事件（从 ChuteAssignmentReceived 改为 ChuteAssigned）
        _upstreamClient.ChuteAssigned += OnChuteAssignmentReceived;
    }

    /// <summary>
    /// 启动编排服务
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("正在启动分拣编排服务...");

        // 启动传感器事件监听
        _logger.LogInformation("正在启动传感器事件监听...");
        await _sensorEventProvider.StartAsync(cancellationToken);
        _logger.LogInformation("传感器事件监听已启动");

        // 连接到上游系统
        _isConnected = await _upstreamClient.ConnectAsync(cancellationToken);

        if (_isConnected)
        {
            _logger.LogInformation("成功连接到上游系统，分拣编排服务已启动");
        }
        else
        {
            _logger.LogWarning("无法连接到上游系统，将在包裹检测时尝试重新连接");
        }
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
        await _upstreamClient.DisconnectAsync();
        _isConnected = false;

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
                "[开始] 开始处理包裹: ParcelId={ParcelId}, SensorId={SensorId}, StartTime={StartTime:o}",
                parcelId,
                sensorId,
                _clock.LocalNow);

            // 步骤 1: 创建本地包裹实体（PR-42 Parcel-First）
            _logger.LogDebug("[步骤 1/5] 创建本地包裹实体");
            await CreateParcelEntityAsync(parcelId, sensorId);

            // 步骤 2: 验证系统状态
            _logger.LogDebug("[步骤 2/5] 验证系统状态");
            var stateValidation = await ValidateSystemStateAsync(parcelId);
            if (!stateValidation.IsValid)
            {
                _logger.LogWarning(
                    "[系统状态验证失败] 包裹 {ParcelId} 被拒绝: {Reason}",
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
            _logger.LogDebug("[步骤 3/5] 拥堵检测与超载评估");
            var overloadDecision = await DetectCongestionAndOverloadAsync(parcelId);
            
            // 步骤 4: 确定目标格口
            _logger.LogDebug("[步骤 4/5] 确定目标格口");
            var targetChuteId = await DetermineTargetChuteAsync(parcelId, overloadDecision);
            _logger.LogInformation(
                "[目标格口确定] 包裹 {ParcelId} 的目标格口: {TargetChuteId}",
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
                _metrics?.RecordSortingFailedParcel("QueueManagerMissing");
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
                parcelId.ToString(),
                targetChuteId,
                _clock.LocalNow);
            
            // 如果生成失败或为空，生成异常格口任务
            if (queueTasks == null || queueTasks.Count == 0)
            {
                _logger.LogWarning(
                    "[队列任务生成失败] 包裹 {ParcelId} 无法生成到目标格口 {TargetChuteId} 的任务，生成异常格口任务",
                    parcelId, targetChuteId);
                
                queueTasks = _pathGenerator.GenerateQueueTasks(
                    parcelId.ToString(),
                    exceptionChuteId,
                    _clock.LocalNow);
                
                if (queueTasks == null || queueTasks.Count == 0)
                {
                    // 连异常格口任务都无法生成
                    stopwatch.Stop();
                    _metrics?.RecordSortingFailedParcel("QueueTaskGenerationFailed");
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
            _logger.LogDebug(
                "[任务入队] 开始将 {TaskCount} 个任务加入队列",
                queueTasks.Count);
            
            foreach (var task in queueTasks)
            {
                _queueManager.EnqueueTask(task.PositionIndex, task);
                
                _logger.LogDebug(
                    "[任务入队] ParcelId={ParcelId}, Position={Position}, DiverterId={DiverterId}, " +
                    "Action={Action}, ExpectedArrival={ExpectedTime:HH:mm:ss.fff}, Timeout={TimeoutMs}ms",
                    task.ParcelId,
                    task.PositionIndex,
                    task.DiverterId,
                    task.DiverterAction,
                    task.ExpectedArrivalTime,
                    task.TimeoutThresholdMs);
            }
            
            _logger.LogInformation(
                "[入队完成] 包裹 {ParcelId} 的 {TaskCount} 个任务已加入队列，目标格口={TargetChuteId}, ElapsedMs={ElapsedMs:F0}",
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
                _metrics?.RecordSortingSuccess(stopwatch.Elapsed.TotalSeconds);
            }
            else
            {
                _logger.LogError(
                    "调试分拣失败: 包裹ID={ParcelId}, 失败原因={FailureReason}, 实际到达格口={ActualChuteId}",
                    parcelId,
                    executionResult.FailureReason,
                    executionResult.ActualChuteId);
                _metrics?.RecordSortingFailure(stopwatch.Elapsed.TotalSeconds);
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
            "[PR-42 Parcel-First] 本地创建包裹: ParcelId={ParcelId}, CreatedAt={CreatedAt:o}, 来源传感器={SensorId}",
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
        // PR-42: Invariant 1 - 上游请求必须引用已存在的本地包裹
        // PR-44: ConcurrentDictionary.ContainsKey 是线程安全的
        if (!_createdParcels.ContainsKey(parcelId))
        {
            _logger.LogError(
                "[PR-42 Invariant Violation] 尝试为不存在的包裹 {ParcelId} 发送上游通知。" +
                "通知已阻止，不发送到上游。",
                parcelId);
            return;
        }

        // 发送上游通知
        var upstreamRequestSentAt = new DateTimeOffset(_clock.LocalNow);
        
        // PR-44: 使用 TryGetValue 是线程安全的
        if (_createdParcels.TryGetValue(parcelId, out var parcel))
        {
            parcel.UpstreamRequestSentAt = upstreamRequestSentAt;
        }
        
        _logger.LogInformation(
            "[PR-42 Parcel-First] 发送上游包裹检测通知: ParcelId={ParcelId}, SentAt={SentAt:o}, ClientType={ClientType}, ClientFullName={ClientFullName}, IsConnected={IsConnected}",
            parcelId,
            upstreamRequestSentAt,
            _upstreamClient.GetType().Name,
            _upstreamClient.GetType().FullName,
            _upstreamClient.IsConnected);
        
        var notificationSent = await _upstreamClient.NotifyParcelDetectedAsync(parcelId, CancellationToken.None);
        
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
    /// <remarks>
    /// 在所有分拣模式下都向上游发送分拣完成通知。
    /// 通知失败时记录错误日志，但不阻止后续流程。
    /// </remarks>
    private async Task NotifyUpstreamSortingCompletedAsync(
        long parcelId, 
        long actualChuteId, 
        bool isSuccess, 
        string? failureReason)
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
                FinalStatus = isSuccess 
                    ? Core.Enums.Parcel.ParcelFinalStatus.Success 
                    : Core.Enums.Parcel.ParcelFinalStatus.ExecutionError
            };

            _logger.LogTrace(
                "发送分拣完成通知到上游: ParcelId={ParcelId}, ActualChuteId={ActualChuteId}, IsSuccess={IsSuccess}, ClientType={ClientType}",
                parcelId,
                actualChuteId,
                isSuccess,
                _upstreamClient.GetType().Name);

            var notificationSent = await _upstreamClient.NotifySortingCompletedAsync(notification, CancellationToken.None);

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
        
        // PR-44: ConcurrentDictionary 索引器赋值是线程安全的
        _pendingAssignments[parcelId] = tcs;

        try
        {
            // 计算动态超时时间
            var timeoutSeconds = CalculateChuteAssignmentTimeout(systemConfig);
            var timeoutMs = (int)(timeoutSeconds * 1000);
            
            var startTime = _clock.LocalNow;
            using var cts = new CancellationTokenSource(timeoutMs);
            var targetChuteId = await tcs.Task.WaitAsync(cts.Token);
            var elapsedMs = (_clock.LocalNow - startTime).TotalMilliseconds;
            
            _logger.LogInformation(
                "包裹 {ParcelId} 从上游系统分配到格口 {ChuteId}（耗时 {ElapsedMs:F0}ms，超时限制 {TimeoutMs}ms）", 
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
            return await HandleRoutingTimeoutAsync(parcelId, systemConfig, exceptionChuteId, "Timeout");
        }
        catch (OperationCanceledException)
        {
            return await HandleRoutingTimeoutAsync(parcelId, systemConfig, exceptionChuteId, "Cancelled");
        }
        finally
        {
            // PR-44: ConcurrentDictionary.TryRemove 是线程安全的
            _pendingAssignments.TryRemove(parcelId, out _);
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

        // PR-UPSTREAM02: 发送落格完成通知给上游系统
        var notification = new SortingCompletedNotification
        {
            ParcelId = parcelId,
            ActualChuteId = result.ActualChuteId,
            CompletedAt = new DateTimeOffset(_clock.LocalNow),
            IsSuccess = result.IsSuccess && !isOverloadException,
            FailureReason = isOverloadException ? "超载重定向到异常格口" : result.FailureReason
        };

        var notificationSent = await _upstreamClient.NotifySortingCompletedAsync(notification, CancellationToken.None);
        
        if (!notificationSent)
        {
            _logger.LogWarning(
                "[PR-UPSTREAM02] 落格完成通知发送失败 | ParcelId={ParcelId} | ChuteId={ChuteId} | IsSuccess={IsSuccess}",
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
    }

    #endregion

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

            // 检查是否为 WheelFront 传感器触发
            if (_sensorConfigRepository != null && _queueManager != null && _topologyRepository != null)
            {
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
        _logger.LogDebug(
            "Position {PositionIndex} 传感器 {SensorId} 触发，从队列取出任务",
            positionIndex, sensorId);

        // 从 Position-Index 队列取出任务
        var task = _queueManager!.DequeueTask(positionIndex);
        
        if (task == null)
        {
            _logger.LogWarning(
                "Position {PositionIndex} 队列为空，但传感器 {SensorId} 被触发 (摆轮ID={WheelDiverterId})",
                positionIndex, sensorId, boundWheelDiverterId);
            
            _metrics?.RecordSortingFailure(0);
            return;
        }
        
        // 检查超时
        var currentTime = _clock.LocalNow;
        var isTimeout = currentTime > task.ExpectedArrivalTime.AddMilliseconds(task.TimeoutThresholdMs);
        
        DiverterDirection actionToExecute;
        
        if (isTimeout)
        {
            var delayMs = (currentTime - task.ExpectedArrivalTime).TotalMilliseconds;
            _logger.LogWarning(
                "包裹 {ParcelId} 在 Position {PositionIndex} 超时 (延迟 {DelayMs}ms)，使用回退动作 {FallbackAction}",
                task.ParcelId, positionIndex, delayMs, task.FallbackAction);
            
            actionToExecute = task.FallbackAction;
            
            // 超时包裹需要在后续所有 position 插入 Straight 任务到队列头部
            // 因为超时包裹虽然超时，但仍排在后续包裹前面
            if (_topologyRepository != null)
            {
                var topology = _topologyRepository.Get();
                if (topology != null)
                {
                    // 找到所有比当前 position 大的节点
                    var subsequentNodes = topology.DiverterNodes
                        .Where(n => n.PositionIndex > positionIndex)
                        .OrderBy(n => n.PositionIndex)
                        .ToList();
                    
                    if (subsequentNodes.Any())
                    {
                        _logger.LogWarning(
                            "包裹 {ParcelId} 超时，在后续 {Count} 个 position 插入 Straight 任务: [{Positions}]",
                            task.ParcelId,
                            subsequentNodes.Count,
                            string.Join(", ", subsequentNodes.Select(n => n.PositionIndex)));
                        
                        foreach (var node in subsequentNodes)
                        {
                            var straightTask = new PositionQueueItem
                            {
                                ParcelId = task.ParcelId,
                                DiverterId = node.DiverterId,
                                DiverterAction = DiverterDirection.Straight,
                                ExpectedArrivalTime = _clock.LocalNow, // 已超时，使用当前时间
                                TimeoutThresholdMs = task.TimeoutThresholdMs,
                                FallbackAction = DiverterDirection.Straight,
                                PositionIndex = node.PositionIndex,
                                CreatedAt = _clock.LocalNow
                            };
                            
                            // 使用优先入队，插入到队列头部
                            _queueManager!.EnqueuePriorityTask(node.PositionIndex, straightTask);
                        }
                    }
                }
            }
            
            _metrics?.RecordSortingFailure(0);
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
            TargetChuteId = 0, // 单段执行时不关心目标格口
            Segments = new List<SwitchingPathSegment>
            {
                new SwitchingPathSegment
                {
                    SequenceNumber = 1,
                    DiverterId = task.DiverterId,
                    TargetDirection = actionToExecute,
                    TtlMilliseconds = 5000 // 单个动作执行超时5秒
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
                    "包裹 {ParcelId} 在 Position {PositionIndex} 执行摆轮动作失败: {ErrorMessage}",
                    task.ParcelId, positionIndex, executionResult.FailureReason);
                _metrics?.RecordSortingFailure(0);
                return;
            }
            
            _logger.LogDebug(
                "包裹 {ParcelId} 在 Position {PositionIndex} 摆轮动作执行成功",
                task.ParcelId, positionIndex);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "包裹 {ParcelId} 在 Position {PositionIndex} 执行摆轮动作时发生异常",
                task.ParcelId, positionIndex);
            _metrics?.RecordSortingFailure(0);
            return;
        }
        
        if (!isTimeout)
        {
            _metrics?.RecordSortingSuccess(0);
        }
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
            await _upstreamClient.NotifyParcelDetectedAsync(parcelId, CancellationToken.None);

            // 使用新的队列系统将包裹发送到异常格口
            if (_queueManager != null)
            {
                var queueTasks = _pathGenerator.GenerateQueueTasks(
                    parcelId.ToString(),
                    exceptionChuteId,
                    _clock.LocalNow);
                
                if (queueTasks != null && queueTasks.Count > 0)
                {
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
    /// 处理格口分配通知
    /// </summary>
    private void OnChuteAssignmentReceived(object? sender, ChuteAssignmentEventArgs e)
    {
        // PR-42: Invariant 2 - 上游响应必须匹配已存在的本地包裹
        // PR-44: ConcurrentDictionary.ContainsKey 是线程安全的
        if (!_createdParcels.ContainsKey(e.ParcelId))
        {
            _logger.LogError(
                "[PR-42 Invariant Violation] 收到未知包裹 {ParcelId} 的路由响应 (ChuteId={ChuteId})，" +
                "本地不存在此包裹实体。响应已丢弃，不创建幽灵包裹。",
                e.ParcelId,
                e.ChuteId);
            return;
        }

        // 记录上游响应接收时间
        _createdParcels[e.ParcelId].UpstreamReplyReceivedAt = new DateTimeOffset(_clock.LocalNow);
        
        if (_pendingAssignments.TryGetValue(e.ParcelId, out var tcs))
        {
            // 正常情况：在超时前收到响应
            _logger.LogDebug("收到包裹 {ParcelId} 的格口分配: {ChuteId}", e.ParcelId, e.ChuteId);
            
            // 记录路由绑定时间
            _createdParcels[e.ParcelId].RouteBoundAt = new DateTimeOffset(_clock.LocalNow);
            
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
            // PR-44: ConcurrentDictionary.TryRemove 是线程安全的
            _pendingAssignments.TryRemove(e.ParcelId, out _);
        }
        else
        {
            // 迟到的响应：包裹已经超时并被路由到异常口
            _logger.LogInformation(
                "【迟到路由响应】收到包裹 {ParcelId} 的格口分配 (ChuteId={ChuteId})，" +
                "但该包裹已因超时被路由到异常口，不再改变去向。" +
                "接收时间={ReceivedAt:yyyy-MM-dd HH:mm:ss.fff}",
                e.ParcelId,
                e.ChuteId,
                _clock.LocalNow);
        }
    }

    #endregion

    #region Helper Methods - 辅助方法

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

    #endregion

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
                _metrics?.RecordSortingFailedParcel("PathGenerationFailed");
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

                // TODO: 从executionResult获取实际执行时间而非传入0
                _metrics?.RecordSortingSuccess(0);
                
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

                var notificationSent = await _upstreamClient.NotifySortingCompletedAsync(notification, cancellationToken);
                
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
                _metrics?.RecordSortingFailedParcel(executionResult.FailureReason ?? "Unknown");
                
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

                await _upstreamClient.NotifySortingCompletedAsync(notification, cancellationToken);
            }

            // 记录拥堵数据（如果启用）
            _congestionCollector?.RecordParcelCompletion(parcelId, _clock.LocalNow, executionResult.IsSuccess);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理超时包裹 {ParcelId} 时发生异常", parcelId);
            _metrics?.RecordSortingFailedParcel($"Exception: {ex.Message}");
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
        // PR-UPSTREAM02: 从 ChuteAssignmentReceived 改为 ChuteAssigned
        _upstreamClient.ChuteAssigned -= OnChuteAssignmentReceived;

        // 断开连接
        StopAsync().GetAwaiter().GetResult();
    }
}
