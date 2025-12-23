using System.ComponentModel.DataAnnotations;

namespace ZakYip.WheelDiverterSorter.Host.Models;

/// <summary>
/// 更新包裹丢失检测配置请求
/// </summary>
public record UpdateParcelLossDetectionConfigRequest
{
    /// <summary>
    /// 是否启用包裹丢失检测（可选）
    /// </summary>
    /// <remarks>
    /// 控制整个包裹丢失检测功能的开关
    /// - true: 启用丢失检测和超时检测
    /// - false: 关闭所有检测，包裹不会因超时或丢失而被移除
    /// </remarks>
    /// <example>true</example>
    public bool? IsEnabled { get; init; }
    
    /// <summary>
    /// 监控间隔（毫秒）
    /// </summary>
    /// <remarks>
    /// 包裹丢失监控服务扫描队列的时间间隔
    /// 默认值：60ms
    /// 推荐范围：50-500ms
    /// </remarks>
    /// <example>60</example>
    [Range(50, 1000, ErrorMessage = "监控间隔必须在50-1000ms之间")]
    public int? MonitoringIntervalMs { get; init; }
    
    /// <summary>
    /// 自动清空中位数数据的时间间隔（毫秒，可选）
    /// </summary>
    /// <remarks>
    /// 当超过此时间未创建新包裹时，自动清空所有 Position 的中位数统计数据
    /// 设置为 0 表示不自动清空
    /// 默认值：300000ms (5分钟)
    /// 推荐范围：0-3600000ms (0-60分钟)
    /// </remarks>
    /// <example>300000</example>
    [Range(0, 3600000, ErrorMessage = "自动清空间隔必须在0-3600000ms之间")]
    public int? AutoClearMedianIntervalMs { get; init; }
    
    /// <summary>
    /// 丢失检测系数（可选）
    /// </summary>
    /// <remarks>
    /// 丢失阈值 = 中位数间隔 * 丢失检测系数
    /// 默认值：1.5
    /// 推荐范围：1.5-2.5
    /// </remarks>
    /// <example>1.5</example>
    [Range(1.0, 5.0, ErrorMessage = "丢失检测系数必须在1.0-5.0之间")]
    public double? LostDetectionMultiplier { get; init; }
    
    /// <summary>
    /// 超时检测系数（可选）
    /// </summary>
    /// <remarks>
    /// 超时阈值 = 中位数间隔 * 超时检测系数
    /// 默认值：3.0
    /// 推荐范围：2.5-3.5
    /// </remarks>
    /// <example>3.0</example>
    [Range(1.5, 10.0, ErrorMessage = "超时检测系数必须在1.5-10.0之间")]
    public double? TimeoutMultiplier { get; init; }
    
    /// <summary>
    /// 历史窗口大小（可选）
    /// </summary>
    /// <remarks>
    /// 默认值：10
    /// 推荐范围：10-10000（支持大窗口）
    /// </remarks>
    /// <example>10</example>
    [Range(10, 10000, ErrorMessage = "历史窗口大小必须在10-10000之间")]
    public int? WindowSize { get; init; }
}
