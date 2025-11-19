namespace ZakYip.WheelDiverterSorter.Core.Sorting.Events;

/// <summary>
/// 包裹正常落格事件载荷，当包裹成功分拣到目标格口时触发
/// </summary>
public readonly record struct ParcelDivertedEventArgs
{
    /// <summary>
    /// 包裹唯一标识符
    /// </summary>
    public required long ParcelId { get; init; }

    /// <summary>
    /// 落格时间（UTC）
    /// </summary>
    public required DateTimeOffset DivertedAt { get; init; }

    /// <summary>
    /// 实际到达的格口ID
    /// </summary>
    public required int ActualChuteId { get; init; }

    /// <summary>
    /// 目标格口ID
    /// </summary>
    public int TargetChuteId { get; init; }

    /// <summary>
    /// 分拣总耗时（毫秒）
    /// </summary>
    public double TotalTimeMs { get; init; }

    /// <summary>
    /// 是否成功（实际格口是否与目标格口一致）
    /// </summary>
    public bool IsSuccess => ActualChuteId == TargetChuteId || TargetChuteId == 0;
}
