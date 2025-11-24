namespace ZakYip.WheelDiverterSorter.Core.Sorting.Models;

/// <summary>
/// 包裹描述符
/// </summary>
/// <remarks>
/// 包含包裹的标识、条码、入口时间等基本信息
/// </remarks>
public record ParcelDescriptor
{
    /// <summary>
    /// 包裹ID（通常为毫秒时间戳）
    /// </summary>
    public required long ParcelId { get; init; }

    /// <summary>
    /// 包裹条码
    /// </summary>
    /// <remarks>
    /// 可选字段，用于与上游系统关联
    /// </remarks>
    public string? Barcode { get; init; }

    /// <summary>
    /// 入口时间
    /// </summary>
    public DateTimeOffset IngressTime { get; init; }

    /// <summary>
    /// 检测传感器ID
    /// </summary>
    /// <remarks>
    /// 记录检测到该包裹的传感器标识
    /// </remarks>
    public string? SensorId { get; init; }
}
