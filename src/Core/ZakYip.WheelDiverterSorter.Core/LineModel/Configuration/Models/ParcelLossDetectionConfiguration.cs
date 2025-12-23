namespace ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;

/// <summary>
/// 包裹丢失检测配置模型
/// </summary>
/// <remarks>
/// 存储包裹丢失检测的配置参数，支持运行时热更新
/// </remarks>
public class ParcelLossDetectionConfiguration
{
    /// <summary>
    /// 配置ID（由持久化层自动生成）
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 配置名称（唯一标识符）
    /// </summary>
    /// <remarks>
    /// 使用固定值 "parcel-loss-detection" 确保只有一条配置记录
    /// </remarks>
    public string ConfigName { get; set; } = "parcel-loss-detection";

    /// <summary>
    /// 是否启用包裹丢失检测
    /// </summary>
    /// <remarks>
    /// 控制整个包裹丢失检测功能的开关
    /// - true: 启用丢失检测和超时检测
    /// - false: 关闭所有检测，包裹不会因超时或丢失而被移除
    /// 默认值：true
    /// </remarks>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 监控间隔（毫秒）
    /// </summary>
    /// <remarks>
    /// 包裹丢失监控服务扫描队列的时间间隔
    /// 默认值：60ms
    /// 推荐范围：50-500ms
    /// </remarks>
    public int MonitoringIntervalMs { get; set; } = 60;

    /// <summary>
    /// 自动清空中位数数据的时间间隔（毫秒）
    /// </summary>
    /// <remarks>
    /// 当超过此时间未创建新包裹时，自动清空所有 Position 的中位数统计数据
    /// 设置为 0 表示不自动清空
    /// 默认值：300000ms (5分钟)
    /// 推荐范围：60000-600000ms (1-10分钟)
    /// </remarks>
    public int AutoClearMedianIntervalMs { get; set; } = 300000;

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
    public int AutoClearQueueIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// 丢失检测系数
    /// </summary>
    /// <remarks>
    /// 丢失阈值 = 中位数间隔 * 丢失检测系数
    /// 默认值：1.5
    /// 推荐范围：1.5-2.5
    /// </remarks>
    public double LostDetectionMultiplier { get; set; } = 1.5;

    /// <summary>
    /// 超时检测系数
    /// </summary>
    /// <remarks>
    /// 超时阈值 = 中位数间隔 * 超时检测系数
    /// 默认值：3.0
    /// 推荐范围：2.5-3.5
    /// </remarks>
    public double TimeoutMultiplier { get; set; } = 3.0;

    /// <summary>
    /// 历史窗口大小（最近N个间隔样本）
    /// </summary>
    /// <remarks>
    /// 默认值：10
    /// 推荐范围：10-20
    /// </remarks>
    public int WindowSize { get; set; } = 10;

    /// <summary>
    /// 配置版本号
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// 配置创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 配置最后更新时间
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// 获取默认配置
    /// </summary>
    public static ParcelLossDetectionConfiguration GetDefault()
    {
        var now = ConfigurationDefaults.DefaultTimestamp;
        return new ParcelLossDetectionConfiguration
        {
            ConfigName = "parcel-loss-detection",
            IsEnabled = true,
            MonitoringIntervalMs = 60,
            AutoClearMedianIntervalMs = 300000,
            AutoClearQueueIntervalSeconds = 30,
            LostDetectionMultiplier = 1.5,
            TimeoutMultiplier = 3.0,
            WindowSize = 10,
            Version = 1,
            CreatedAt = now,
            UpdatedAt = now
        };
    }
}
