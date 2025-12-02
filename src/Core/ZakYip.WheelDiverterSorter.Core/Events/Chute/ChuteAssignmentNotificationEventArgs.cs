namespace ZakYip.WheelDiverterSorter.Core.Events.Chute;

/// <summary>
/// 格口分配通知事件参数（用于JSON反序列化）
/// PR-PERF-EVENTS01: 转换为 sealed record class 以优化性能
/// </summary>
/// <remarks>
/// 当RuleEngine推送格口分配时触发此事件。
/// PR-UPSTREAM02: 扩展以支持 DWS 数据和新的分配时间字段。
/// </remarks>
public sealed record class ChuteAssignmentNotificationEventArgs
{
    /// <summary>
    /// 包裹ID
    /// </summary>
    public required long ParcelId { get; init; }

    /// <summary>
    /// 目标格口ID
    /// </summary>
    public required long ChuteId { get; init; }

    /// <summary>
    /// 分配时间
    /// </summary>
    /// <remarks>
    /// PR-UPSTREAM02: 从 NotificationTime 重命名为 AssignedAt
    /// </remarks>
    public required DateTimeOffset AssignedAt { get; init; }

    /// <summary>
    /// DWS 数据（可选）
    /// </summary>
    public DwsMeasurementEventArgs? DwsPayload { get; init; }

    /// <summary>
    /// 额外的元数据（可选）
    /// </summary>
    public Dictionary<string, string>? Metadata { get; init; }
}

/// <summary>
/// DWS 测量数据事件参数
/// PR-PERF-EVENTS01: 转换为 sealed record class 以优化性能
/// </summary>
/// <remarks>
/// PR-UPSTREAM02: 新增类型，用于事件中传递 DWS 数据。
/// </remarks>
public sealed record class DwsMeasurementEventArgs
{
    /// <summary>
    /// 重量（克）
    /// </summary>
    public decimal WeightGrams { get; init; }

    /// <summary>
    /// 长度（毫米）
    /// </summary>
    public decimal LengthMm { get; init; }

    /// <summary>
    /// 宽度（毫米）
    /// </summary>
    public decimal WidthMm { get; init; }

    /// <summary>
    /// 高度（毫米）
    /// </summary>
    public decimal HeightMm { get; init; }

    /// <summary>
    /// 体积重量（克），可选
    /// </summary>
    public decimal? VolumetricWeightGrams { get; init; }

    /// <summary>
    /// 条码（可选）
    /// </summary>
    public string? Barcode { get; init; }

    /// <summary>
    /// 测量时间
    /// </summary>
    public DateTimeOffset MeasuredAt { get; init; }
}
