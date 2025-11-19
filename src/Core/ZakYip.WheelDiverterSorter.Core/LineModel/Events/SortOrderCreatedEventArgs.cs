using ZakYip.WheelDiverterSorter.Core.LineModel.Enums;

namespace ZakYip.WheelDiverterSorter.Core.LineModel.Events;

/// <summary>
/// 分拣指令创建事件载荷，当系统为包裹生成分拣指令时触发
/// </summary>
public readonly record struct SortOrderCreatedEventArgs
{
    /// <summary>
    /// 包裹标识
    /// </summary>
    public required long ParcelId { get; init; }

    /// <summary>
    /// 目标分拣方向
    /// </summary>
    public required DiverterSide TargetSide { get; init; }

    /// <summary>
    /// 指令创建时间
    /// </summary>
    public required DateTimeOffset CreatedAt { get; init; }
}
