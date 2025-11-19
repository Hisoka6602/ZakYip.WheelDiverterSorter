namespace ZakYip.WheelDiverterSorter.Execution.Concurrency;

/// <summary>
/// 并发控制配置选项
/// </summary>
public class ConcurrencyOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "Concurrency";

    /// <summary>
    /// 最大并发包裹处理数量
    /// </summary>
    /// <remarks>
    /// 限制同时处理的包裹数量，防止系统过载。
    /// 默认值：10
    /// </remarks>
    public int MaxConcurrentParcels { get; set; } = 10;

    /// <summary>
    /// 包裹队列容量
    /// </summary>
    /// <remarks>
    /// 包裹队列的最大容量，超过此容量会阻塞入队操作。
    /// -1表示无限制。
    /// 默认值：100
    /// </remarks>
    public int ParcelQueueCapacity { get; set; } = 100;

    /// <summary>
    /// 批量处理的最大批次大小
    /// </summary>
    /// <remarks>
    /// 相同目标格口的包裹批量处理时的最大数量。
    /// 默认值：5
    /// </remarks>
    public int MaxBatchSize { get; set; } = 5;

    /// <summary>
    /// 是否启用批量处理优化
    /// </summary>
    /// <remarks>
    /// 启用后，系统会尝试批量处理相同目标格口的包裹，减少摆轮切换次数。
    /// 默认值：true
    /// </remarks>
    public bool EnableBatchProcessing { get; set; } = true;

    /// <summary>
    /// 摆轮锁等待超时时间（毫秒）
    /// </summary>
    /// <remarks>
    /// 等待获取摆轮锁的最大时间，超时后会放弃并返回失败。
    /// 默认值：5000ms（5秒）
    /// </remarks>
    public int DiverterLockTimeoutMs { get; set; } = 5000;
}
