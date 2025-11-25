using ZakYip.WheelDiverterSorter.Core.LineModel.Runtime.Health;
using ZakYip.WheelDiverterSorter.Core.Enums.System;
using ZakYip.WheelDiverterSorter.Core.Enums.Monitoring;

namespace ZakYip.WheelDiverterSorter.Observability.Runtime.Health;

/// <summary>
/// 线体健康快照 / Line Health Snapshot
/// 包含线体可用性、异常口比例、最近告警计数等聚合健康数据
/// Contains line availability, exception chute ratio, recent alert count and other aggregated health data
/// </summary>
public record LineHealthSnapshot
{
    /// <summary>
    /// 系统状态 / System State
    /// </summary>
    public required SystemState SystemState { get; init; }

    /// <summary>
    /// 自检是否成功 / Whether self-test was successful
    /// </summary>
    public bool IsSelfTestSuccess { get; init; }

    /// <summary>
    /// 最近一次自检时间 / Last self-test timestamp
    /// </summary>
    public DateTimeOffset? LastSelfTestAt { get; init; }

    /// <summary>
    /// 驱动器健康状态列表 / Driver health status list
    /// </summary>
    public List<DriverHealthStatus>? Drivers { get; init; }

    /// <summary>
    /// 上游系统健康状态列表 / Upstream system health status list
    /// </summary>
    public List<UpstreamHealthStatus>? Upstreams { get; init; }

    /// <summary>
    /// 配置健康状态 / Configuration health status
    /// </summary>
    public ConfigHealthStatus? Config { get; init; }

    /// <summary>
    /// 降级模式 / Degradation mode
    /// </summary>
    public DegradationMode? DegradationMode { get; init; }

    /// <summary>
    /// 降级节点数量 / Degraded node count
    /// </summary>
    public int? DegradedNodesCount { get; init; }

    /// <summary>
    /// 降级节点列表 / Degraded nodes list
    /// </summary>
    public List<NodeHealthStatus>? DegradedNodes { get; init; }

    /// <summary>
    /// 当前诊断级别 / Current diagnostics level
    /// </summary>
    public DiagnosticsLevel? DiagnosticsLevel { get; init; }

    /// <summary>
    /// 配置版本号 / Configuration version
    /// </summary>
    public string? ConfigVersion { get; init; }

    /// <summary>
    /// 最近Critical告警数量 / Recent critical alert count
    /// </summary>
    public int RecentCriticalAlertCount { get; init; }

    /// <summary>
    /// 当前拥堵级别 / Current congestion level
    /// </summary>
    public CongestionLevel? CurrentCongestionLevel { get; init; }

    /// <summary>
    /// 线体是否可用 / Whether the line is available
    /// </summary>
    public bool IsLineAvailable { get; init; }

    /// <summary>
    /// 异常口比例 / Exception chute ratio (0.0 to 1.0)
    /// </summary>
    public double? ExceptionChuteRatio { get; init; }
}
