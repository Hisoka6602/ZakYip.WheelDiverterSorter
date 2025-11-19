using ZakYip.WheelDiverterSorter.Core.Sorting.Runtime;

namespace ZakYip.WheelDiverterSorter.Core.Sorting.Overload;

/// <summary>
/// 超载上下文：描述当前包裹的分拣环境，用于超载策略判断。
/// </summary>
public readonly record struct OverloadContext
{
    /// <summary>
    /// 包裹ID。
    /// </summary>
    public string ParcelId { get; init; }

    /// <summary>
    /// 目标格口ID。
    /// </summary>
    public long TargetChuteId { get; init; }

    /// <summary>
    /// 当前线速（毫米/秒）。
    /// </summary>
    public decimal CurrentLineSpeed { get; init; }

    /// <summary>
    /// 当前所在位置（节点ID）。
    /// </summary>
    public string? CurrentPosition { get; init; }

    /// <summary>
    /// 预计到达可分拣节点的时间窗口（毫秒）。
    /// </summary>
    public double EstimatedArrivalWindowMs { get; init; }

    /// <summary>
    /// 当前拥堵等级。
    /// </summary>
    public CongestionLevel CurrentCongestionLevel { get; init; }

    /// <summary>
    /// 剩余可用TTL（毫秒）。
    /// </summary>
    public double RemainingTtlMs { get; init; }

    /// <summary>
    /// 当前在途包裹数。
    /// </summary>
    public int InFlightParcels { get; init; }
}
