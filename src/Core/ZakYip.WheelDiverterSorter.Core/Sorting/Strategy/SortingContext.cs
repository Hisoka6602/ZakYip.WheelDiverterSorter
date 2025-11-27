using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Sorting.Models;

namespace ZakYip.WheelDiverterSorter.Core.Sorting.Strategy;

/// <summary>
/// 分拣上下文
/// </summary>
/// <remarks>
/// 包含格口选择所需的所有信息，统一传递给策略实现。
/// 将分拣模式、条码、可用格口列表、异常格口 ID、超时时间等信息封装在一起。
/// </remarks>
public record SortingContext
{
    /// <summary>
    /// 包裹ID
    /// </summary>
    public required long ParcelId { get; init; }

    /// <summary>
    /// 条码（可选）
    /// </summary>
    public string? Barcode { get; init; }

    /// <summary>
    /// 当前分拣模式
    /// </summary>
    public required SortingMode SortingMode { get; init; }

    /// <summary>
    /// 异常格口ID
    /// </summary>
    public required long ExceptionChuteId { get; init; }

    /// <summary>
    /// 固定格口ID（仅在 FixedChute 模式下有效）
    /// </summary>
    public long? FixedChuteId { get; init; }

    /// <summary>
    /// 可用格口ID列表（仅在 RoundRobin 模式下有效）
    /// </summary>
    public IReadOnlyList<long> AvailableChuteIds { get; init; } = EmptyAvailableChuteIds;

    /// <summary>
    /// 空的可用格口列表（静态共享实例）
    /// </summary>
    private static readonly IReadOnlyList<long> EmptyAvailableChuteIds = Array.Empty<long>();

    /// <summary>
    /// 异常路由策略
    /// </summary>
    public ExceptionRoutingPolicy? ExceptionRoutingPolicy { get; init; }

    /// <summary>
    /// 是否因超载强制路由到异常格口
    /// </summary>
    public bool IsOverloadForced { get; init; }
}
