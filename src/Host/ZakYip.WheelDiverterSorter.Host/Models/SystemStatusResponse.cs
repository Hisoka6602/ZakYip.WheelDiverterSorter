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
    public required string SystemState { get; init; }

    /// <summary>
    /// 运行环境模式 - Environment mode
    /// </summary>
    /// <remarks>
    /// 指示当前是正式环境还是仿真环境
    /// - "Production": 正式环境（使用真实硬件驱动）
    /// - "Simulation": 仿真环境（使用模拟驱动）
    /// </remarks>
    /// <example>Simulation</example>
    public required string EnvironmentMode { get; init; }

    /// <summary>
    /// 查询时间戳 - Query timestamp
    /// </summary>
    /// <example>2025-11-24T15:04:05.536+08:00</example>
    public DateTimeOffset Timestamp { get; init; }
}
