namespace ZakYip.WheelDiverterSorter.Core.Events;

/// <summary>
/// 包裹扫描事件载荷，当包裹通过扫描器时触发
/// </summary>
public record ParcelScannedEventArgs
{
    /// <summary>
    /// 包裹条码
    /// </summary>
    public required string Barcode { get; init; }

    /// <summary>
    /// 扫描时间
    /// </summary>
    public required DateTimeOffset ScannedAt { get; init; }
}
