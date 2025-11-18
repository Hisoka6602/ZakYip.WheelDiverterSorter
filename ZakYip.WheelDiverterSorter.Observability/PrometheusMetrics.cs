using Prometheus;

namespace ZakYip.WheelDiverterSorter.Observability;

/// <summary>
/// Prometheus指标服务
/// Provides Prometheus metrics for the wheel diverter sorter system
/// </summary>
public class PrometheusMetrics
{
    // 分拣成功/失败计数器
    // Sorting success/failure counters
    private static readonly Counter SortingSuccessCounter = Metrics
        .CreateCounter("sorter_sorting_success_total", "分拣成功总数 / Total number of successful sorting operations");

    private static readonly Counter SortingFailureCounter = Metrics
        .CreateCounter("sorter_sorting_failure_total", "分拣失败总数 / Total number of failed sorting operations");

    // 包裹吞吐量（使用计数器，Prometheus会计算速率）
    // Parcel throughput (using counter, Prometheus calculates rate)
    private static readonly Counter ParcelThroughputCounter = Metrics
        .CreateCounter("sorter_parcel_throughput_total", "包裹处理总数 / Total number of parcels processed");

    // 路径生成和执行耗时（直方图）
    // Path generation and execution duration histograms
    private static readonly Histogram PathGenerationDuration = Metrics
        .CreateHistogram("sorter_path_generation_duration_seconds", 
            "路径生成耗时（秒）/ Path generation duration in seconds",
            new HistogramConfiguration
            {
                Buckets = Histogram.ExponentialBuckets(0.001, 2, 12) // 1ms to ~4s
            });

    private static readonly Histogram PathExecutionDuration = Metrics
        .CreateHistogram("sorter_path_execution_duration_seconds",
            "路径执行耗时（秒）/ Path execution duration in seconds",
            new HistogramConfiguration
            {
                Buckets = Histogram.ExponentialBuckets(0.01, 2, 12) // 10ms to ~40s
            });

    private static readonly Histogram SortingDuration = Metrics
        .CreateHistogram("sorter_sorting_duration_seconds",
            "整体分拣耗时（秒）/ Overall sorting duration in seconds",
            new HistogramConfiguration
            {
                Buckets = Histogram.ExponentialBuckets(0.01, 2, 12) // 10ms to ~40s
            });

    // 队列长度和等待时间
    // Queue length and wait time
    private static readonly Gauge QueueLength = Metrics
        .CreateGauge("sorter_queue_length", "当前队列长度 / Current queue length");

    private static readonly Histogram QueueWaitTime = Metrics
        .CreateHistogram("sorter_queue_wait_time_seconds",
            "队列等待时间（秒）/ Queue wait time in seconds",
            new HistogramConfiguration
            {
                Buckets = Histogram.ExponentialBuckets(0.001, 2, 15) // 1ms to ~16s
            });

    // 摆轮状态和使用率
    // Wheel diverter status and utilization
    private static readonly Gauge DiverterActiveCount = Metrics
        .CreateGauge("sorter_diverter_active_count", "活跃摆轮数量 / Number of active diverters",
            new GaugeConfiguration
            {
                LabelNames = new[] { "diverter_id" }
            });

    private static readonly Counter DiverterOperationCounter = Metrics
        .CreateCounter("sorter_diverter_operations_total", "摆轮操作总数 / Total number of diverter operations",
            new CounterConfiguration
            {
                LabelNames = new[] { "diverter_id", "direction" }
            });

    private static readonly Gauge DiverterUtilization = Metrics
        .CreateGauge("sorter_diverter_utilization_ratio", "摆轮使用率 / Diverter utilization ratio (0-1)",
            new GaugeConfiguration
            {
                LabelNames = new[] { "diverter_id" }
            });

    // RuleEngine连接状态
    // RuleEngine connection status
    private static readonly Gauge RuleEngineConnectionStatus = Metrics
        .CreateGauge("sorter_ruleengine_connection_status", 
            "RuleEngine连接状态 (1=已连接, 0=已断开) / RuleEngine connection status (1=connected, 0=disconnected)",
            new GaugeConfiguration
            {
                LabelNames = new[] { "connection_type" }
            });

    private static readonly Counter RuleEngineSendCounter = Metrics
        .CreateCounter("sorter_ruleengine_messages_sent_total", 
            "发送到RuleEngine的消息总数 / Total messages sent to RuleEngine",
            new CounterConfiguration
            {
                LabelNames = new[] { "connection_type", "message_type" }
            });

    private static readonly Counter RuleEngineReceiveCounter = Metrics
        .CreateCounter("sorter_ruleengine_messages_received_total",
            "从RuleEngine接收的消息总数 / Total messages received from RuleEngine",
            new CounterConfiguration
            {
                LabelNames = new[] { "connection_type", "message_type" }
            });

    // 传感器健康状态
    // Sensor health status
    private static readonly Gauge SensorHealthStatus = Metrics
        .CreateGauge("sorter_sensor_health_status",
            "传感器健康状态 (1=健康, 0=故障) / Sensor health status (1=healthy, 0=faulty)",
            new GaugeConfiguration
            {
                LabelNames = new[] { "sensor_id", "sensor_type" }
            });

    private static readonly Counter SensorErrorCounter = Metrics
        .CreateCounter("sorter_sensor_errors_total",
            "传感器错误总数 / Total number of sensor errors",
            new CounterConfiguration
            {
                LabelNames = new[] { "sensor_id", "sensor_type" }
            });

    private static readonly Counter SensorDetectionCounter = Metrics
        .CreateCounter("sorter_sensor_detections_total",
            "传感器检测总数 / Total number of sensor detections",
            new CounterConfiguration
            {
                LabelNames = new[] { "sensor_id", "sensor_type" }
            });

    // 当前活跃请求数
    // Current active requests
    private static readonly Gauge ActiveRequests = Metrics
        .CreateGauge("sorter_active_requests", "当前活跃的分拣请求数 / Number of currently active sorting requests");

    // PR-08: 超载包裹计数器（新增）
    // PR-08: Overload parcels counter (new)
    
    /// <summary>
    /// 超载包裹计数器（带原因标签）
    /// </summary>
    private static readonly Counter OverloadParcelsCounter = Metrics
        .CreateCounter("sorting_overload_parcels_total", 
            "超载包裹总数 / Total number of overload parcels",
            new CounterConfiguration
            {
                LabelNames = new[] { "reason" }
            });

    /// <summary>
    /// 推荐产能指标（包裹/分钟）
    /// </summary>
    private static readonly Gauge RecommendedCapacityGauge = Metrics
        .CreateGauge("sorting_capacity_recommended_parcels_per_minute", 
            "推荐安全产能（包裹/分钟）/ Recommended safe capacity (parcels per minute)");

    /// <summary>
    /// 平均分拣延迟（毫秒）
    /// </summary>
    private static readonly Gauge AverageLatencyGauge = Metrics
        .CreateGauge("sorting_average_latency_ms", "平均分拣延迟（毫秒）/ Average sorting latency in milliseconds");




    /// <summary>
    /// 记录分拣成功
    /// Record a successful sorting operation
    /// </summary>
    /// <param name="durationSeconds">分拣耗时（秒）</param>
    public void RecordSortingSuccess(double durationSeconds)
    {
        SortingSuccessCounter.Inc();
        ParcelThroughputCounter.Inc();
        SortingDuration.Observe(durationSeconds);
    }

    /// <summary>
    /// 记录分拣失败
    /// Record a failed sorting operation
    /// </summary>
    /// <param name="durationSeconds">分拣耗时（秒）</param>
    public void RecordSortingFailure(double durationSeconds)
    {
        SortingFailureCounter.Inc();
        SortingDuration.Observe(durationSeconds);
    }

    /// <summary>
    /// 记录路径生成耗时
    /// Record path generation duration
    /// </summary>
    /// <param name="durationSeconds">耗时（秒）</param>
    public void RecordPathGeneration(double durationSeconds)
    {
        PathGenerationDuration.Observe(durationSeconds);
    }

    /// <summary>
    /// 记录路径执行耗时
    /// Record path execution duration
    /// </summary>
    /// <param name="durationSeconds">耗时（秒）</param>
    public void RecordPathExecution(double durationSeconds)
    {
        PathExecutionDuration.Observe(durationSeconds);
    }

    /// <summary>
    /// 设置队列长度
    /// Set the current queue length
    /// </summary>
    /// <param name="length">队列长度</param>
    public void SetQueueLength(int length)
    {
        QueueLength.Set(length);
    }

    /// <summary>
    /// 记录队列等待时间
    /// Record queue wait time
    /// </summary>
    /// <param name="waitTimeSeconds">等待时间（秒）</param>
    public void RecordQueueWaitTime(double waitTimeSeconds)
    {
        QueueWaitTime.Observe(waitTimeSeconds);
    }

    /// <summary>
    /// 设置摆轮活跃状态
    /// Set diverter active status
    /// </summary>
    /// <param name="diverterId">摆轮ID</param>
    /// <param name="isActive">是否活跃</param>
    public void SetDiverterActive(string diverterId, bool isActive)
    {
        DiverterActiveCount.WithLabels(diverterId).Set(isActive ? 1 : 0);
    }

    /// <summary>
    /// 记录摆轮操作
    /// Record a diverter operation
    /// </summary>
    /// <param name="diverterId">摆轮ID</param>
    /// <param name="direction">方向（left/right/straight）</param>
    public void RecordDiverterOperation(string diverterId, string direction)
    {
        DiverterOperationCounter.WithLabels(diverterId, direction).Inc();
    }

    /// <summary>
    /// 设置摆轮使用率
    /// Set diverter utilization ratio
    /// </summary>
    /// <param name="diverterId">摆轮ID</param>
    /// <param name="utilizationRatio">使用率（0-1）</param>
    public void SetDiverterUtilization(string diverterId, double utilizationRatio)
    {
        DiverterUtilization.WithLabels(diverterId).Set(utilizationRatio);
    }

    /// <summary>
    /// 设置RuleEngine连接状态
    /// Set RuleEngine connection status
    /// </summary>
    /// <param name="connectionType">连接类型（tcp/signalr/mqtt/http）</param>
    /// <param name="isConnected">是否已连接</param>
    public void SetRuleEngineConnectionStatus(string connectionType, bool isConnected)
    {
        RuleEngineConnectionStatus.WithLabels(connectionType).Set(isConnected ? 1 : 0);
    }

    /// <summary>
    /// 记录发送到RuleEngine的消息
    /// Record a message sent to RuleEngine
    /// </summary>
    /// <param name="connectionType">连接类型</param>
    /// <param name="messageType">消息类型</param>
    public void RecordRuleEngineSend(string connectionType, string messageType)
    {
        RuleEngineSendCounter.WithLabels(connectionType, messageType).Inc();
    }

    /// <summary>
    /// 记录从RuleEngine接收的消息
    /// Record a message received from RuleEngine
    /// </summary>
    /// <param name="connectionType">连接类型</param>
    /// <param name="messageType">消息类型</param>
    public void RecordRuleEngineReceive(string connectionType, string messageType)
    {
        RuleEngineReceiveCounter.WithLabels(connectionType, messageType).Inc();
    }

    /// <summary>
    /// 设置传感器健康状态
    /// Set sensor health status
    /// </summary>
    /// <param name="sensorId">传感器ID</param>
    /// <param name="sensorType">传感器类型</param>
    /// <param name="isHealthy">是否健康</param>
    public void SetSensorHealthStatus(string sensorId, string sensorType, bool isHealthy)
    {
        SensorHealthStatus.WithLabels(sensorId, sensorType).Set(isHealthy ? 1 : 0);
    }

    /// <summary>
    /// 记录传感器错误
    /// Record a sensor error
    /// </summary>
    /// <param name="sensorId">传感器ID</param>
    /// <param name="sensorType">传感器类型</param>
    public void RecordSensorError(string sensorId, string sensorType)
    {
        SensorErrorCounter.WithLabels(sensorId, sensorType).Inc();
    }

    /// <summary>
    /// 记录传感器检测
    /// Record a sensor detection
    /// </summary>
    /// <param name="sensorId">传感器ID</param>
    /// <param name="sensorType">传感器类型</param>
    public void RecordSensorDetection(string sensorId, string sensorType)
    {
        SensorDetectionCounter.WithLabels(sensorId, sensorType).Inc();
    }

    /// <summary>
    /// 增加活跃请求数
    /// Increment active requests count
    /// </summary>
    public void IncrementActiveRequests()
    {
        ActiveRequests.Inc();
    }

    /// <summary>
    /// 减少活跃请求数
    /// Decrement active requests count
    /// </summary>
    public void DecrementActiveRequests()
    {
        ActiveRequests.Dec();
    }

    // ========== 仿真专用指标 / Simulation-specific Metrics ==========

    // 仿真包裹总数（按状态分类）
    // Simulation parcel count by status
    private static readonly Counter SimulationParcelCounter = Metrics
        .CreateCounter("simulation_parcel_total", 
            "仿真包裹总数（按状态分类）/ Total simulation parcels by status",
            new CounterConfiguration
            {
                LabelNames = new[] { "status" }
            });

    // 仿真错分总数（应始终为 0）
    // Simulation mis-sort count (should always be 0)
    private static readonly Counter SimulationMisSortCounter = Metrics
        .CreateCounter("simulation_mis_sort_total", 
            "仿真错分总数（应始终为 0）/ Total mis-sorts in simulation (should always be 0)");

    // 仿真包裹行程时间
    // Simulation parcel travel time
    private static readonly Histogram SimulationTravelTime = Metrics
        .CreateHistogram("simulation_travel_time_seconds",
            "仿真包裹行程时间（秒）/ Simulation parcel travel time in seconds",
            new HistogramConfiguration
            {
                Buckets = Histogram.ExponentialBuckets(0.1, 1.5, 15) // 0.1s to ~40s
            });

    /// <summary>
    /// 记录仿真包裹结果
    /// Record simulation parcel result
    /// </summary>
    /// <param name="status">包裹状态</param>
    /// <param name="travelTimeSeconds">行程时间（秒），可选</param>
    public void RecordSimulationParcel(string status, double? travelTimeSeconds = null)
    {
        SimulationParcelCounter.WithLabels(status).Inc();
        
        if (travelTimeSeconds.HasValue)
        {
            SimulationTravelTime.Observe(travelTimeSeconds.Value);
        }
    }

    /// <summary>
    /// 记录仿真错分
    /// Record simulation mis-sort (should never happen!)
    /// </summary>
    public void RecordSimulationMisSort()
    {
        SimulationMisSortCounter.Inc();
    }

    /// <summary>
    /// 获取当前仿真错分计数
    /// Get current simulation mis-sort count
    /// </summary>
    /// <returns>错分计数</returns>
    public static double GetSimulationMisSortCount()
    {
        // Note: prometheus-net doesn't expose counter values directly in a clean way
        // This is a workaround for monitoring purposes
        // In production, you'd query Prometheus directly
        return SimulationMisSortCounter.Value;
    }

    // 仿真高密度包裹总数（按策略分类）
    // Simulation dense parcel count by strategy
    private static readonly Counter SimulationDenseParcelCounter = Metrics
        .CreateCounter("simulation_dense_parcel_total",
            "仿真高密度包裹总数（按策略分类）/ Total dense parcels in simulation by strategy",
            new CounterConfiguration
            {
                LabelNames = new[] { "scenario", "strategy" }
            });

    // 仿真高密度包裹路由到异常格口总数
    // Simulation dense parcels routed to exception chute
    private static readonly Counter SimulationDenseParcelRoutedToExceptionCounter = Metrics
        .CreateCounter("simulation_dense_parcel_routed_to_exception_total",
            "仿真高密度包裹路由到异常格口总数 / Total dense parcels routed to exception chute",
            new CounterConfiguration
            {
                LabelNames = new[] { "scenario" }
            });

    // 仿真高密度包裹头距分布（时间）
    // Simulation dense parcel headway distribution (time)
    private static readonly Histogram SimulationDenseParcelHeadwayTime = Metrics
        .CreateHistogram("simulation_dense_parcel_headway_time_seconds",
            "仿真高密度包裹头距分布（时间，秒）/ Dense parcel headway time distribution in seconds",
            new HistogramConfiguration
            {
                Buckets = Histogram.LinearBuckets(0, 0.1, 20) // 0 to 2s in 0.1s increments
            });

    // 仿真高密度包裹头距分布（空间）
    // Simulation dense parcel headway distribution (space)
    private static readonly Histogram SimulationDenseParcelHeadwayDistance = Metrics
        .CreateHistogram("simulation_dense_parcel_headway_distance_mm",
            "仿真高密度包裹头距分布（空间，mm）/ Dense parcel headway distance distribution in mm",
            new HistogramConfiguration
            {
                Buckets = Histogram.LinearBuckets(0, 50, 20) // 0 to 1000mm in 50mm increments
            });

    /// <summary>
    /// 记录仿真高密度包裹
    /// Record simulation dense parcel
    /// </summary>
    /// <param name="scenario">场景名称</param>
    /// <param name="strategy">处理策略</param>
    /// <param name="headwayTimeSeconds">头距时间（秒），可选</param>
    /// <param name="headwayDistanceMm">头距距离（mm），可选</param>
    public void RecordSimulationDenseParcel(
        string scenario, 
        string strategy,
        double? headwayTimeSeconds = null,
        double? headwayDistanceMm = null)
    {
        SimulationDenseParcelCounter.WithLabels(scenario, strategy).Inc();

        if (headwayTimeSeconds.HasValue)
        {
            SimulationDenseParcelHeadwayTime.Observe(headwayTimeSeconds.Value);
        }

        if (headwayDistanceMm.HasValue)
        {
            SimulationDenseParcelHeadwayDistance.Observe(headwayDistanceMm.Value);
        }

        // 如果策略是路由到异常格口，也记录到专门的计数器
        if (strategy == "RouteToException")
        {
            SimulationDenseParcelRoutedToExceptionCounter.WithLabels(scenario).Inc();
        }
    }

    // ========== PR-05 验收指标 / PR-05 Acceptance Metrics ==========

    // 总处理包裹数
    // Total processed parcels
    private static readonly Counter SortingTotalParcels = Metrics
        .CreateCounter("sorting_total_parcels", 
            "总处理包裹数 / Total number of processed parcels");

    // 失败包裹数（按原因分类）
    // Failed parcels by reason
    private static readonly Counter SortingFailedParcelsTotal = Metrics
        .CreateCounter("sorting_failed_parcels_total",
            "失败包裹总数（按原因分类）/ Total number of failed parcels by reason",
            new CounterConfiguration
            {
                LabelNames = new[] { "reason" }
            });

    // 成功包裹从入口传感器到落格的延迟直方图
    // Success parcel latency from entry sensor to chute drop
    private static readonly Histogram SortingSuccessLatencySeconds = Metrics
        .CreateHistogram("sorting_success_latency_seconds",
            "成功包裹从入口传感器到落格的延迟（秒）/ Success parcel latency from entry to chute drop in seconds",
            new HistogramConfiguration
            {
                Buckets = Histogram.ExponentialBuckets(0.1, 1.5, 20) // 0.1s to ~400s
            });

    // 状态机状态切换计数
    // State machine state change counter
    private static readonly Counter SystemStateChangesTotal = Metrics
        .CreateCounter("system_state_changes_total",
            "状态机状态切换总数 / Total number of state machine state changes",
            new CounterConfiguration
            {
                LabelNames = new[] { "from_state", "to_state" }
            });

    /// <summary>
    /// 记录总处理包裹数
    /// Record total processed parcels
    /// </summary>
    public void RecordSortingTotalParcels()
    {
        SortingTotalParcels.Inc();
    }

    /// <summary>
    /// 记录失败包裹（按原因分类）
    /// Record failed parcel by reason
    /// </summary>
    /// <param name="reason">失败原因：upstream_timeout, ttl_failure, topology_unreachable, sensor_fault, etc.</param>
    public void RecordSortingFailedParcel(string reason)
    {
        SortingFailedParcelsTotal.WithLabels(reason).Inc();
    }

    /// <summary>
    /// 记录成功包裹的延迟
    /// Record success parcel latency
    /// </summary>
    /// <param name="latencySeconds">从入口传感器到落格的延迟（秒）</param>
    public void RecordSortingSuccessLatency(double latencySeconds)
    {
        SortingSuccessLatencySeconds.Observe(latencySeconds);
    }

    /// <summary>
    /// 记录状态机状态切换
    /// Record state machine state change
    /// </summary>
    /// <param name="fromState">源状态</param>
    /// <param name="toState">目标状态</param>
    public void RecordSystemStateChange(string fromState, string toState)
    {
        SystemStateChangesTotal.WithLabels(fromState, toState).Inc();
    }

    // ========== PR-07 路径失败与重规划指标 / PR-07 Path Failure and Rerouting Metrics ==========

    // 路径失败总数（按原因分类）
    // Path failures by reason
    private static readonly Counter PathFailuresTotal = Metrics
        .CreateCounter("sorting_path_failures_total",
            "路径失败总数（按原因分类）/ Total number of path failures by reason",
            new CounterConfiguration
            {
                LabelNames = new[] { "reason" }
            });

    // 路径重规划总数
    // Path reroutes total
    private static readonly Counter PathReroutesTotal = Metrics
        .CreateCounter("sorting_path_reroutes_total",
            "路径重规划总数 / Total number of path reroutes");

    // 重规划成功总数
    // Reroute success total
    private static readonly Counter RerouteSuccessTotal = Metrics
        .CreateCounter("sorting_reroute_success_total",
            "通过重规划成功进入正常格口的总数 / Total number of successful reroutes to normal chute");

    /// <summary>
    /// 记录路径失败（按原因分类）
    /// Record path failure by reason
    /// </summary>
    /// <param name="reason">失败原因：SensorTimeout, UnexpectedDirection, UpstreamBlocked, PhysicalConstraint, etc.</param>
    public void RecordPathFailure(string reason)
    {
        PathFailuresTotal.WithLabels(reason).Inc();
    }

    /// <summary>
    /// 记录路径重规划尝试
    /// Record path reroute attempt
    /// </summary>
    public void RecordPathReroute()
    {
        PathReroutesTotal.Inc();
    }

    /// <summary>
    /// 记录重规划成功
    /// Record reroute success
    /// </summary>
    public void RecordRerouteSuccess()
    {
        RerouteSuccessTotal.Inc();
    }

    // ========== PR-08: 拥堵检测与背压控制指标 / Congestion Detection and Throttling Metrics ==========

    /// <summary>
    /// 拥堵级别 (0=Normal, 1=Warning, 2=Severe)
    /// Congestion level gauge
    /// </summary>
    private static readonly Gauge CongestionLevelGauge = Metrics
        .CreateGauge("sorting_congestion_level", 
            "拥堵级别 (0=正常, 1=警告, 2=严重) / Congestion level (0=Normal, 1=Warning, 2=Severe)");

    /// <summary>
    /// 当前放包间隔（毫秒）
    /// Current release interval in milliseconds
    /// </summary>
    private static readonly Gauge ReleaseIntervalGauge = Metrics
        .CreateGauge("sorting_release_interval_ms",
            "当前放包间隔（毫秒）/ Current release interval in milliseconds");

    /// <summary>
    /// 节流事件总数
    /// Total number of throttle events
    /// </summary>
    private static readonly Counter ThrottleEventsCounter = Metrics
        .CreateCounter("sorting_throttle_events_total",
            "节流/暂停事件总数 / Total number of throttle/pause events",
            new CounterConfiguration
            {
                LabelNames = new[] { "action" } // "throttle", "pause", "resume"
            });

    /// <summary>
    /// 当前在途包裹数
    /// Current number of in-flight parcels
    /// </summary>
    private static readonly Gauge InFlightParcelsGauge = Metrics
        .CreateGauge("sorting_inflight_parcels",
            "当前在途包裹数（已进入但未完成分拣）/ Current number of in-flight parcels (entered but not sorted)");

    /// <summary>
    /// 设置拥堵级别
    /// Set congestion level
    /// </summary>
    /// <param name="level">拥堵级别 (0=Normal, 1=Warning, 2=Severe)</param>
    public void SetCongestionLevel(int level)
    {
        CongestionLevelGauge.Set(level);
    }

    /// <summary>
    /// 设置当前放包间隔
    /// Set current release interval
    /// </summary>
    /// <param name="intervalMs">放包间隔（毫秒）</param>
    public void SetReleaseInterval(int intervalMs)
    {
        ReleaseIntervalGauge.Set(intervalMs);
    }

    /// <summary>
    /// 记录节流事件
    /// Record throttle event
    /// </summary>
    /// <param name="action">动作类型：throttle/pause/resume</param>
    public void RecordThrottleEvent(string action)
    {
        ThrottleEventsCounter.WithLabels(action).Inc();
    }

    /// <summary>
    /// 设置当前在途包裹数
    /// Set current in-flight parcels count
    /// </summary>
    /// <param name="count">在途包裹数</param>
    public void SetInFlightParcels(int count)
    {
        InFlightParcelsGauge.Set(count);
    }

    // PR-08: 超载指标方法（新增）
    // PR-08: Overload metrics methods (new)

    /// <summary>
    /// 记录超载包裹
    /// Record overload parcel
    /// </summary>
    /// <param name="reason">超载原因，如 "Timeout", "WindowMiss", "CapacityExceeded"</param>
    public void RecordOverloadParcel(string reason)
    {
        OverloadParcelsCounter.WithLabels(reason).Inc();
    }

    /// <summary>
    /// 设置推荐产能
    /// Set recommended capacity
    /// </summary>
    /// <param name="parcelsPerMinute">推荐的包裹/分钟</param>
    public void SetRecommendedCapacity(double parcelsPerMinute)
    {
        RecommendedCapacityGauge.Set(parcelsPerMinute);
    }

    /// <summary>
    /// 设置平均延迟
    /// Set average latency
    /// </summary>
    /// <param name="latencyMs">平均延迟（毫秒）</param>
    public void SetAverageLatency(double latencyMs)
    {
        AverageLatencyGauge.Set(latencyMs);
    }

    // ========== PR-09: 系统健康与自检指标 / System Health and Self-Test Metrics ==========

    /// <summary>
    /// 系统状态 (0=Booting, 1=Ready, 2=Running, 3=Paused, 4=Faulted, 5=EmergencyStop)
    /// </summary>
    private static readonly Gauge SystemStateGauge = Metrics
        .CreateGauge("system_state", 
            "系统状态 (0=Booting, 1=Ready, 2=Running, 3=Paused, 4=Faulted, 5=EmergencyStop) / System state");

    /// <summary>
    /// 最近一次自检成功时间（Unix时间戳）
    /// </summary>
    private static readonly Gauge SystemSelfTestLastSuccessTimestamp = Metrics
        .CreateGauge("system_selftest_last_success_timestamp",
            "最近一次自检成功时间（Unix时间戳）/ Last successful self-test timestamp (Unix)");

    /// <summary>
    /// 自检失败总次数
    /// </summary>
    private static readonly Counter SystemSelfTestFailuresTotal = Metrics
        .CreateCounter("system_selftest_failures_total",
            "自检失败总次数 / Total number of self-test failures");

    /// <summary>
    /// 设置系统状态
    /// Set system state
    /// </summary>
    /// <param name="state">系统状态枚举值</param>
    public void SetSystemState(int state)
    {
        SystemStateGauge.Set(state);
    }

    /// <summary>
    /// 记录自检成功
    /// Record successful self-test
    /// </summary>
    public void RecordSelfTestSuccess()
    {
        SystemSelfTestLastSuccessTimestamp.Set(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
    }

    /// <summary>
    /// 记录自检失败
    /// Record self-test failure
    /// </summary>
    public void RecordSelfTestFailure()
    {
        SystemSelfTestFailuresTotal.Inc();
    }
}


