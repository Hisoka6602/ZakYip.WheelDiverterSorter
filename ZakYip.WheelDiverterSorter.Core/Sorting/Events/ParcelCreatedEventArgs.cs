namespace ZakYip.Sorting.Core.Events;

/// <summary>
/// 包裹创建事件载荷，当入口光电检测到包裹时触发
/// </summary>
public readonly record struct ParcelCreatedEventArgs
{
    /// <summary>
    /// 包裹唯一标识符
    /// </summary>
    public required long ParcelId { get; init; }

    /// <summary>
    /// 包裹条码（可选，来自扫码器）
    /// </summary>
    public string? Barcode { get; init; }

    /// <summary>
    /// 创建时间（UTC）
    /// </summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// 传感器编号
    /// </summary>
    public string? SensorId { get; init; }
}
