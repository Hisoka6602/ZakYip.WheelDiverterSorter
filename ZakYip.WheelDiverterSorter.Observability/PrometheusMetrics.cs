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
}
