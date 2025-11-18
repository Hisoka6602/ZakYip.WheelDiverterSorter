namespace ZakYip.WheelDiverterSorter.Core.Runtime.Health;

/// <summary>
/// 系统自检报告
/// </summary>
public record SystemSelfTestReport
{
    /// <summary>
    /// 自检是否成功
    /// </summary>
    public required bool IsSuccess { get; init; }

    /// <summary>
    /// 驱动器健康状态列表
    /// </summary>
    public required IReadOnlyList<DriverHealthStatus> Drivers { get; init; }

    /// <summary>
    /// 上游系统健康状态列表
    /// </summary>
    public required IReadOnlyList<UpstreamHealthStatus> Upstreams { get; init; }

    /// <summary>
    /// 配置健康状态
    /// </summary>
    public required ConfigHealthStatus Config { get; init; }

    /// <summary>
    /// 自检执行时间
    /// </summary>
    public required DateTimeOffset PerformedAt { get; init; }

    /// <summary>
    /// 节点健康状态列表（PR-14：节点级降级）
    /// Node health statuses for individual components
    /// </summary>
    public IReadOnlyList<NodeHealthStatus>? NodeStatuses { get; init; }

    /// <summary>
    /// 当前降级模式（PR-14：节点级降级）
    /// Current degradation mode
    /// </summary>
    public DegradationMode DegradationMode { get; init; } = DegradationMode.None;
}
