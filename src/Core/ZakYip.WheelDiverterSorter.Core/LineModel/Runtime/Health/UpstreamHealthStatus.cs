namespace ZakYip.WheelDiverterSorter.Core.LineModel.Runtime.Health;

/// <summary>
/// 上游系统健康状态
/// </summary>
public record UpstreamHealthStatus
{
    /// <summary>
    /// 上游端点名称
    /// </summary>
    public required string EndpointName { get; init; }

    /// <summary>
    /// 是否健康
    /// </summary>
    public required bool IsHealthy { get; init; }

    /// <summary>
    /// 错误代码（如果不健康）
    /// </summary>
    public string? ErrorCode { get; init; }

    /// <summary>
    /// 错误消息（中文）
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// 检查时间
    /// </summary>
    public required DateTimeOffset CheckedAt { get; init; }
}
