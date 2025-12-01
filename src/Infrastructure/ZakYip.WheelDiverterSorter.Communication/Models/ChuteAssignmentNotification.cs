namespace ZakYip.WheelDiverterSorter.Communication.Models;

/// <summary>
/// 格口分配通知（上游主动推送）
/// </summary>
/// <remarks>
/// PR-UPSTREAM02: 从 ChuteAssignmentResponse 重命名为 ChuteAssignmentNotification，
/// 体现"上游主动推送"的语义，而非"请求-响应"模式。
/// </remarks>
public record ChuteAssignmentNotification
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
    public required DateTimeOffset AssignedAt { get; init; }

    /// <summary>
    /// DWS（尺寸重量扫描）数据（可选）
    /// </summary>
    public DwsMeasurementDto? DwsPayload { get; init; }

    /// <summary>
    /// 额外的元数据（可选）
    /// </summary>
    public Dictionary<string, string>? Metadata { get; init; }
}

/// <summary>
/// DWS（尺寸重量扫描）测量数据 DTO
/// </summary>
/// <remarks>
/// PR-UPSTREAM02: 新增类型，用于通信层传输 DWS 数据。
/// 与 Core 层的 DwsMeasurement 对应。
/// </remarks>
public record DwsMeasurementDto
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
