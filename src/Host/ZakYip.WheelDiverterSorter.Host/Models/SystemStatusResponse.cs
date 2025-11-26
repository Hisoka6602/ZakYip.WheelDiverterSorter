using ZakYip.WheelDiverterSorter.Core.Enums.System;

namespace ZakYip.WheelDiverterSorter.Host.Models;

/// <summary>
/// 系统状态响应 - System Status Response
/// </summary>
/// <remarks>
/// 用于快速查询系统当前状态和运行环境，支持高并发查询（可能100ms调用一次）
/// </remarks>
public sealed record SystemStatusResponse
{
    /// <summary>
    /// 当前系统状态 - Current system state
    /// </summary>
    /// <example>Ready</example>
    public required SystemState SystemState { get; init; }

    /// <summary>
    /// 系统状态显示名称（中文）- System state display name in Chinese
    /// </summary>
    /// <remarks>
    /// 状态的中文描述，便于前端显示
    /// </remarks>
    /// <example>就绪</example>
    public string SystemStateDisplayName { get; init; } = string.Empty;

    /// <summary>
    /// 运行环境模式 - Environment mode
    /// </summary>
    /// <remarks>
    /// 指示当前是正式环境还是仿真环境
    /// </remarks>
    /// <example>Simulation</example>
    public required EnvironmentMode EnvironmentMode { get; init; }

    /// <summary>
    /// 是否处于异常状态 - Whether the system is in abnormal state
    /// </summary>
    /// <remarks>
    /// 当系统状态为 Faulted 或 EmergencyStop 时为 true
    /// </remarks>
    /// <example>false</example>
    public bool IsAbnormal { get; init; }

    /// <summary>
    /// 异常原因（中文）- Error reason in Chinese
    /// </summary>
    /// <remarks>
    /// 当系统处于异常状态（Faulted 或 EmergencyStop）时，提供中文异常原因描述。
    /// 正常状态时为 null。
    /// </remarks>
    /// <example>null</example>
    public string? ErrorReason { get; init; }

    /// <summary>
    /// 查询时间戳 - Query timestamp
    /// </summary>
    /// <example>2025-11-24T15:04:05.536+08:00</example>
    public DateTimeOffset Timestamp { get; init; }
}
