namespace ZakYip.WheelDiverterSorter.Host.Models;

/// <summary>
/// 包裹丢失检测配置DTO
/// </summary>
public record ParcelLossDetectionConfigDto
{
    /// <summary>
    /// 是否启用包裹丢失检测
    /// </summary>
    /// <remarks>
    /// 控制整个包裹丢失检测功能的开关
    /// - true: 启用丢失检测和超时检测
    /// - false: 关闭所有检测，包裹不会因超时或丢失而被移除
    /// 默认值：true
    /// </remarks>
    /// <example>true</example>
    public required bool IsEnabled { get; init; }
    
    /// <summary>
    /// 监控间隔（毫秒）
    /// </summary>
    /// <remarks>
    /// 包裹丢失监控服务扫描队列的时间间隔
    /// 默认值：60ms
    /// 推荐范围：50-500ms
    /// 值越小检测越及时，但CPU占用越高
    /// </remarks>
    /// <example>60</example>
    public required int MonitoringIntervalMs { get; init; }
    
    /// <summary>
    /// 自动清空中位数数据的时间间隔（毫秒）
    /// </summary>
    /// <remarks>
    /// 当超过此时间未创建新包裹时，自动清空所有 Position 的中位数统计数据
    /// 设置为 0 表示不自动清空
    /// 默认值：300000ms (5分钟)
    /// 推荐范围：60000-600000ms (1-10分钟)
    /// </remarks>
    /// <example>300000</example>
    public required int AutoClearMedianIntervalMs { get; init; }
    
    /// <summary>
    /// 自动清空任务队列的时间间隔（秒）
    /// </summary>
    /// <remarks>
    /// 当超过此时间未创建新包裹时，自动清空所有 Position 的任务队列
    /// 设置为 0 表示不自动清空
    /// 默认值：30秒
    /// 推荐范围：10-600秒 (10秒-10分钟)
    /// 
    /// 注意：此功能用于处理长时间无包裹进入后的队列清理，避免旧任务影响新包裹分拣
    /// </remarks>
    /// <example>30</example>
    public required int AutoClearQueueIntervalSeconds { get; init; }
    
    /// <summary>
    /// 丢失检测系数
    /// </summary>
    /// <remarks>
    /// ⚠️ 已废弃：此系数仅用于中位数统计观测，不再用于实际丢失判断逻辑。
    /// 实际丢失判断使用：LostDetectionTimeoutMs = TimeoutThresholdMs × 1.5（基于输送线配置）
    /// 默认值：1.5（保留为向后兼容）
    /// </remarks>
    /// <example>1.5</example>
    public required double LostDetectionMultiplier { get; init; }
    
    /// <summary>
    /// 超时检测系数
    /// </summary>
    /// <remarks>
    /// ⚠️ 已废弃：此系数仅用于中位数统计观测，不再用于实际超时判断逻辑。
    /// 实际超时判断使用：TimeoutThresholdMs（来自 ConveyorSegmentConfiguration.TimeToleranceMs）
    /// 默认值：3.0（保留为向后兼容）
    /// </remarks>
    /// <example>3.0</example>
    public required double TimeoutMultiplier { get; init; }
    
    /// <summary>
    /// 历史窗口大小（最近N个间隔样本）
    /// </summary>
    /// <remarks>
    /// 默认值：10
    /// 推荐范围：10-10000（新增：支持大窗口）
    /// </remarks>
    /// <example>10</example>
    public required int WindowSize { get; init; }
}
