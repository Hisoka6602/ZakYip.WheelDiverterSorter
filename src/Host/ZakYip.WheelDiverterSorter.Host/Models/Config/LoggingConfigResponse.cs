using Swashbuckle.AspNetCore.Annotations;

namespace ZakYip.WheelDiverterSorter.Host.Models.Config;

/// <summary>
/// 日志配置响应模型
/// Logging configuration response model
/// </summary>
/// <remarks>
/// 返回当前日志开关配置状态
/// </remarks>
[SwaggerSchema(Description = "日志配置响应，包含所有日志开关的当前状态")]
public record LoggingConfigResponse
{
    /// <summary>
    /// 配置ID
    /// Configuration ID
    /// </summary>
    /// <example>1</example>
    [SwaggerSchema(Description = "配置的唯一标识符")]
    public required int Id { get; init; }

    /// <summary>
    /// 是否启用包裹生命周期日志
    /// Whether parcel lifecycle log is enabled
    /// </summary>
    /// <example>true</example>
    [SwaggerSchema(Description = "记录包裹从创建到完成的完整生命周期事件")]
    public required bool EnableParcelLifecycleLog { get; init; }

    /// <summary>
    /// 是否启用包裹追踪日志
    /// Whether parcel trace log is enabled
    /// </summary>
    /// <example>true</example>
    [SwaggerSchema(Description = "记录包裹的详细追踪信息，用于调试和分析")]
    public required bool EnableParcelTraceLog { get; init; }

    /// <summary>
    /// 是否启用路径执行日志
    /// Whether path execution log is enabled
    /// </summary>
    /// <example>true</example>
    [SwaggerSchema(Description = "记录分拣路径的生成和执行过程")]
    public required bool EnablePathExecutionLog { get; init; }

    /// <summary>
    /// 是否启用通信日志
    /// Whether communication log is enabled
    /// </summary>
    /// <example>true</example>
    [SwaggerSchema(Description = "记录与上游规则引擎的通信过程")]
    public required bool EnableCommunicationLog { get; init; }

    /// <summary>
    /// 是否启用硬件驱动日志
    /// Whether driver log is enabled
    /// </summary>
    /// <example>true</example>
    [SwaggerSchema(Description = "记录硬件设备操作日志")]
    public required bool EnableDriverLog { get; init; }

    /// <summary>
    /// 是否启用性能监控日志
    /// Whether performance log is enabled
    /// </summary>
    /// <example>true</example>
    [SwaggerSchema(Description = "记录系统性能指标和监控数据")]
    public required bool EnablePerformanceLog { get; init; }

    /// <summary>
    /// 是否启用告警日志
    /// Whether alarm log is enabled
    /// </summary>
    /// <example>true</example>
    [SwaggerSchema(Description = "记录系统告警事件")]
    public required bool EnableAlarmLog { get; init; }

    /// <summary>
    /// 是否启用调试日志
    /// Whether debug log is enabled
    /// </summary>
    /// <example>false</example>
    [SwaggerSchema(Description = "记录调试级别的详细日志信息")]
    public required bool EnableDebugLog { get; init; }

    /// <summary>
    /// 配置版本号
    /// Configuration version number
    /// </summary>
    /// <example>1</example>
    [SwaggerSchema(Description = "配置的版本号，每次更新递增")]
    public required int Version { get; init; }

    /// <summary>
    /// 创建时间（本地时间）
    /// Creation time (local time)
    /// </summary>
    [SwaggerSchema(Description = "配置创建的时间")]
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// 更新时间（本地时间）
    /// Update time (local time)
    /// </summary>
    [SwaggerSchema(Description = "配置最后更新的时间")]
    public required DateTime UpdatedAt { get; init; }
}
