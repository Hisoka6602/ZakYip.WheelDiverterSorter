using ZakYip.WheelDiverterSorter.Core.Sorting.Runtime;
using ZakYip.WheelDiverterSorter.Core.Enums.Sorting;

namespace ZakYip.WheelDiverterSorter.Core.Sorting.Events;

/// <summary>
/// 超载评估事件载荷，当超载策略评估包裹时触发
/// </summary>
public readonly record struct OverloadEvaluatedEventArgs
{
    /// <summary>
    /// 包裹唯一标识符
    /// </summary>
    public required long ParcelId { get; init; }

    /// <summary>
    /// 评估时间（UTC）
    /// </summary>
    public required DateTimeOffset EvaluatedAt { get; init; }

    /// <summary>
    /// 评估阶段（Entry/RoutePlanning/NodeArrival）
    /// </summary>
    public required string Stage { get; init; }

    /// <summary>
    /// 当前拥堵等级
    /// </summary>
    public CongestionLevel CongestionLevel { get; init; }

    /// <summary>
    /// 是否应强制发送到异常口
    /// </summary>
    public bool ShouldForceException { get; init; }

    /// <summary>
    /// 是否标记为潜在超载风险
    /// </summary>
    public bool ShouldMarkAsOverflow { get; init; }

    /// <summary>
    /// 决策原因
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    /// 剩余TTL（毫秒）
    /// </summary>
    public double RemainingTtlMs { get; init; }

    /// <summary>
    /// 在途包裹数
    /// </summary>
    public int InFlightParcels { get; init; }
}
