namespace ZakYip.WheelDiverterSorter.Core.LineModel.Events;

/// <summary>
/// 改口请求事件参数。
/// </summary>
public record struct ChuteChangeRequestedEventArgs
{
    /// <summary>
    /// 包裹ID
    /// </summary>
    public required long ParcelId { get; init; }

    /// <summary>
    /// 原目标格口ID
    /// </summary>
    public required int OriginalChuteId { get; init; }

    /// <summary>
    /// 请求的新目标格口ID
    /// </summary>
    public required int RequestedChuteId { get; init; }

    /// <summary>
    /// 请求时间
    /// </summary>
    public required DateTimeOffset RequestedAt { get; init; }
}
