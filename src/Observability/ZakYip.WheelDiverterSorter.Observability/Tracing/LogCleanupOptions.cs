namespace ZakYip.WheelDiverterSorter.Observability.Tracing;

/// <summary>
/// 日志清理配置选项
/// </summary>
public sealed class LogCleanupOptions
{
    /// <summary>
    /// 配置节点名称
    /// </summary>
    public const string SectionName = "LogCleanup";

    /// <summary>
    /// 日志根目录
    /// </summary>
    /// <remarks>
    /// 默认为 "logs"
    /// </remarks>
    public string LogDirectory { get; init; } = "logs";

    /// <summary>
    /// 日志保留天数
    /// </summary>
    /// <remarks>
    /// 默认为 14 天
    /// </remarks>
    public int RetentionDays { get; init; } = 14;

    /// <summary>
    /// 日志总大小上限（MB）
    /// </summary>
    /// <remarks>
    /// 默认为 1024 MB (1 GB)。
    /// 当日志总大小超过此值时，会删除最旧的文件直到降到阈值以下。
    /// </remarks>
    public int MaxTotalSizeMb { get; init; } = 1024;

    /// <summary>
    /// 清理间隔（小时）
    /// </summary>
    /// <remarks>
    /// 默认为 24 小时（每天执行一次）
    /// </remarks>
    public int CleanupIntervalHours { get; init; } = 24;
}
