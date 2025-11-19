namespace ZakYip.WheelDiverterSorter.Host.Commands;

/// <summary>
/// 改口命令：请求更改包裹的目标格口
/// </summary>
public sealed record class ChangeParcelChuteCommand
{
    /// <summary>
    /// 包裹ID
    /// </summary>
    public required long ParcelId { get; init; }

    /// <summary>
    /// 请求的新目标格口ID
    /// </summary>
    public required int RequestedChuteId { get; init; }

    /// <summary>
    /// 请求时间（可选，默认为服务器当前时间）
    /// </summary>
    public DateTimeOffset? RequestedAt { get; init; }
}
