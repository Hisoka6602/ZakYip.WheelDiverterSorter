namespace ZakYip.WheelDiverterSorter.Host.Models.Config;

/// <summary>
/// 日志配置响应模型
/// </summary>
/// <remarks>
/// 返回当前日志开关配置状态
/// </remarks>
public record LoggingConfigResponse
{
    /// <summary>
    /// 配置ID
    /// </summary>
    public required int Id { get; init; }

    /// <summary>
    /// 是否启用包裹生命周期日志
    /// </summary>
    public required bool EnableParcelLifecycleLog { get; init; }

    /// <summary>
    /// 是否启用包裹追踪日志
    /// </summary>
    public required bool EnableParcelTraceLog { get; init; }

    /// <summary>
    /// 是否启用路径执行日志
    /// </summary>
    public required bool EnablePathExecutionLog { get; init; }

    /// <summary>
    /// 是否启用通信日志
    /// </summary>
    public required bool EnableCommunicationLog { get; init; }

    /// <summary>
    /// 是否启用硬件驱动日志
    /// </summary>
    public required bool EnableDriverLog { get; init; }

    /// <summary>
    /// 是否启用性能监控日志
    /// </summary>
    public required bool EnablePerformanceLog { get; init; }

    /// <summary>
    /// 是否启用告警日志
    /// </summary>
    public required bool EnableAlarmLog { get; init; }

    /// <summary>
    /// 是否启用调试日志
    /// </summary>
    public required bool EnableDebugLog { get; init; }

    /// <summary>
    /// 配置版本号
    /// </summary>
    public required int Version { get; init; }

    /// <summary>
    /// 创建时间（本地时间）
    /// </summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// 更新时间（本地时间）
    /// </summary>
    public required DateTime UpdatedAt { get; init; }
}
