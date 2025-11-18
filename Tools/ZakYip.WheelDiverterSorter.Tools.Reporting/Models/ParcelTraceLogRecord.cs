namespace ZakYip.WheelDiverterSorter.Tools.Reporting.Models;

/// <summary>
/// 包裹追踪日志记录
/// Parcel trace log record parsed from JSON log files
/// </summary>
public record struct ParcelTraceLogRecord
{
    /// <summary>
    /// 包裹唯一标识符
    /// Parcel unique identifier
    /// </summary>
    public required long ItemId { get; init; }

    /// <summary>
    /// 包裹条码（可选）
    /// Parcel barcode (optional)
    /// </summary>
    public string? BarCode { get; init; }

    /// <summary>
    /// 事件发生时间
    /// Event occurrence timestamp
    /// </summary>
    public required DateTimeOffset OccurredAt { get; init; }

    /// <summary>
    /// 事件阶段（如 Created, OverloadDecision, ExceptionDiverted 等）
    /// Event stage (e.g., Created, OverloadDecision, ExceptionDiverted, etc.)
    /// </summary>
    public required string Stage { get; init; }

    /// <summary>
    /// 事件来源（如 Ingress, Execution, OverloadPolicy 等）
    /// Event source (e.g., Ingress, Execution, OverloadPolicy, etc.)
    /// </summary>
    public required string Source { get; init; }

    /// <summary>
    /// 事件详细信息（可能包含 JSON 或 key=value 格式）
    /// Event details (may contain JSON or key=value format)
    /// </summary>
    public string? Details { get; init; }
}
