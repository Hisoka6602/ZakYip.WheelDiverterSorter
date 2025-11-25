using Swashbuckle.AspNetCore.Annotations;

namespace ZakYip.WheelDiverterSorter.Host.Models.Config;

/// <summary>
/// 日志配置请求模型
/// </summary>
/// <remarks>
/// 用于配置各类日志的开关，异常日志始终输出不受控制
/// </remarks>
[SwaggerSchema(Description = "日志开关配置参数，用于控制各类日志的输出。异常日志始终输出")]
public record LoggingConfigRequest
{
    /// <summary>
    /// 是否启用包裹生命周期日志
    /// </summary>
    /// <example>true</example>
    [SwaggerSchema(Description = "记录包裹从创建到完成的完整生命周期事件")]
    public bool EnableParcelLifecycleLog { get; init; } = true;

    /// <summary>
    /// 是否启用包裹追踪日志
    /// </summary>
    /// <example>true</example>
    [SwaggerSchema(Description = "记录包裹的详细追踪信息，用于调试和分析")]
    public bool EnableParcelTraceLog { get; init; } = true;

    /// <summary>
    /// 是否启用路径执行日志
    /// </summary>
    /// <example>true</example>
    [SwaggerSchema(Description = "记录分拣路径的生成和执行过程")]
    public bool EnablePathExecutionLog { get; init; } = true;

    /// <summary>
    /// 是否启用通信日志
    /// </summary>
    /// <example>true</example>
    [SwaggerSchema(Description = "记录与上游规则引擎的通信过程")]
    public bool EnableCommunicationLog { get; init; } = true;

    /// <summary>
    /// 是否启用硬件驱动日志
    /// </summary>
    /// <example>true</example>
    [SwaggerSchema(Description = "记录硬件设备（摆轮、传感器等）的操作日志")]
    public bool EnableDriverLog { get; init; } = true;

    /// <summary>
    /// 是否启用性能监控日志
    /// </summary>
    /// <example>true</example>
    [SwaggerSchema(Description = "记录系统性能指标和监控数据")]
    public bool EnablePerformanceLog { get; init; } = true;

    /// <summary>
    /// 是否启用告警日志
    /// </summary>
    /// <example>true</example>
    [SwaggerSchema(Description = "记录系统告警事件（非异常级别告警）")]
    public bool EnableAlarmLog { get; init; } = true;

    /// <summary>
    /// 是否启用调试日志
    /// </summary>
    /// <example>false</example>
    [SwaggerSchema(Description = "记录调试级别的详细日志信息")]
    public bool EnableDebugLog { get; init; } = false;
}
