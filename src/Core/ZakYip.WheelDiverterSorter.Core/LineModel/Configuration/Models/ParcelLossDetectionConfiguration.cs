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
    /// 监控间隔（毫秒）
    /// </summary>
    /// <remarks>
    /// 包裹丢失监控服务扫描队列的时间间隔
    /// 默认值：60ms
    /// 推荐范围：50-500ms
    /// </remarks>
    public int MonitoringIntervalMs { get; set; } = 60;

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
            MonitoringIntervalMs = 60,
            LostDetectionMultiplier = 1.5,
            TimeoutMultiplier = 3.0,
            WindowSize = 10,
            Version = 1,
            CreatedAt = now,
            UpdatedAt = now
        };
    }
}
