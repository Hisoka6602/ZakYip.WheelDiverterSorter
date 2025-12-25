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
    /// <para><b>控制范围</b>：</para>
    /// <list type="bullet">
    /// <item><description>后台主动巡检服务（ParcelLossMonitoringService）：巡检是否执行</description></item>
    /// <item><description>队列任务的丢失判定字段（LostDetectionDeadline）：是否设置和使用</description></item>
    /// </list>
    /// <para><b>启用（true）</b>：</para>
    /// <list type="bullet">
    /// <item><description>后台服务定期巡检队列，检测超时丢失的包裹</description></item>
    /// <item><description>队列任务在生成和入队时设置丢失判定阈值（基于中位数×系数）</description></item>
    /// <item><description>包裹超过阈值未到达时，触发丢失处理（清除任务、记录日志、触发事件）</description></item>
    /// </list>
    /// <para><b>禁用（false）</b>：</para>
    /// <list type="bullet">
    /// <item><description>后台服务跳过所有巡检逻辑，不执行任何检测</description></item>
    /// <item><description>队列任务的 LostDetectionTimeoutMs 和 LostDetectionDeadline 字段被清空为 null</description></item>
    /// <item><description>包裹不会因为超时/丢失而被自动移除，只能通过正常到达或手动清空队列</description></item>
    /// </list>
    /// <para><b>默认值</b>：false（禁用检测，不自动清理丢失包裹）</para>
    /// <para><b>使用场景</b>：</para>
    /// <list type="bullet">
    /// <item><description>启用：生产环境，需要自动检测和清理丢失包裹</description></item>
    /// <item><description>禁用：测试/调试环境，需要手动控制队列清空时机</description></item>
    /// </list>
    /// </remarks>
    public bool IsEnabled { get; set; } = false;

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
    /// ⚠️ 已废弃：此系数仅用于中位数统计观测，不再用于实际丢失判断逻辑。
    /// 实际丢失判断使用：LostDetectionTimeoutMs = TimeoutThresholdMs × 1.5（基于输送线配置）
    /// 默认值：1.5（保留为向后兼容）
    /// </remarks>
    public double LostDetectionMultiplier { get; set; } = 1.5;

    /// <summary>
    /// 超时检测系数
    /// </summary>
    /// <remarks>
    /// ⚠️ 已废弃：此系数仅用于中位数统计观测，不再用于实际超时判断逻辑。
    /// 实际超时判断使用：TimeoutThresholdMs（来自 ConveyorSegmentConfiguration.TimeToleranceMs）
    /// 默认值：3.0（保留为向后兼容）
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
            IsEnabled = false,
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
