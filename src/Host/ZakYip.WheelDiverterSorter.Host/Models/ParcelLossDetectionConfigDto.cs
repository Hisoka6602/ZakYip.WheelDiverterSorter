namespace ZakYip.WheelDiverterSorter.Host.Models;

/// <summary>
/// 包裹丢失检测配置DTO
/// </summary>
public record ParcelLossDetectionConfigDto
{
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
    /// 丢失检测系数
    /// </summary>
    /// <remarks>
    /// 丢失阈值 = 中位数间隔 * 丢失检测系数
    /// 默认值：1.5
    /// 推荐范围：1.5-2.5
    /// </remarks>
    /// <example>1.5</example>
    public required double LostDetectionMultiplier { get; init; }
    
    /// <summary>
    /// 超时检测系数
    /// </summary>
    /// <remarks>
    /// 超时阈值 = 中位数间隔 * 超时检测系数
    /// 默认值：3.0
    /// 推荐范围：2.5-3.5
    /// </remarks>
    /// <example>3.0</example>
    public required double TimeoutMultiplier { get; init; }
    
    /// <summary>
    /// 历史窗口大小（最近N个间隔样本）
    /// </summary>
    /// <remarks>
    /// 默认值：10
    /// 推荐范围：10-20
    /// </remarks>
    /// <example>10</example>
    public required int WindowSize { get; init; }
    
    /// <summary>
    /// 最小阈值（毫秒）
    /// </summary>
    /// <remarks>
    /// 防止阈值过低导致误判
    /// 默认值：1000ms (1秒)
    /// </remarks>
    /// <example>1000</example>
    public required double MinThresholdMs { get; init; }
    
    /// <summary>
    /// 最大阈值（毫秒）
    /// </summary>
    /// <remarks>
    /// 防止阈值过高导致检测延迟过长
    /// 默认值：10000ms (10秒)
    /// </remarks>
    /// <example>10000</example>
    public required double MaxThresholdMs { get; init; }
}
