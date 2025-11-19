namespace ZakYip.WheelDiverterSorter.Tools.Reporting.Models;

/// <summary>
/// 告警日志记录（预留用于未来扩展）
/// Alert log record (reserved for future expansion)
/// </summary>
public record struct AlertLogRecord
{
    /// <summary>
    /// 告警代码
    /// Alert code
    /// </summary>
    public required string AlertCode { get; init; }

    /// <summary>
    /// 严重程度
    /// Severity level
    /// </summary>
    public required string Severity { get; init; }

    /// <summary>
    /// 告警触发时间
    /// Alert raised timestamp
    /// </summary>
    public required DateTimeOffset RaisedAt { get; init; }

    /// <summary>
    /// 告警消息
    /// Alert message
    /// </summary>
    public required string Message { get; init; }
}
