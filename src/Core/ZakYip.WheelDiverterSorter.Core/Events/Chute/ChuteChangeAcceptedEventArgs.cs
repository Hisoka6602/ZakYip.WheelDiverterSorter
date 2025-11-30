namespace ZakYip.WheelDiverterSorter.Core.Events.Chute;

/// <summary>
/// 改口接受事件参数。
/// </summary>
public record struct ChuteChangeAcceptedEventArgs
{
    /// <summary>
    /// 包裹ID
    /// </summary>
    public required long ParcelId { get; init; }

    /// <summary>
    /// 原目标格口ID
    /// </summary>
    public required long OriginalChuteId { get; init; }

    /// <summary>
    /// 新目标格口ID
    /// </summary>
    public required long NewChuteId { get; init; }

    /// <summary>
    /// 接受时间
    /// </summary>
    public required DateTimeOffset AcceptedAt { get; init; }
}
