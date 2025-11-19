namespace ZakYip.WheelDiverterSorter.Core.LineModel.Runtime.Health;

/// <summary>
/// 节点健康状态
/// Node health status for individual system components (diverters, stations, IO)
/// </summary>
public record struct NodeHealthStatus
{
    /// <summary>
    /// 节点ID
    /// Node identifier
    /// </summary>
    public required int NodeId { get; init; }

    /// <summary>
    /// 是否健康
    /// Whether the node is healthy
    /// </summary>
    public required bool IsHealthy { get; init; }

    /// <summary>
    /// 错误代码（如果不健康）
    /// Error code when node is unhealthy
    /// </summary>
    public string? ErrorCode { get; init; }

    /// <summary>
    /// 错误消息（中文）
    /// Error message in Chinese
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// 节点类型（如：摆轮、基站、IO设备）
    /// Node type (e.g., diverter, station, IO device)
    /// </summary>
    public string? NodeType { get; init; }

    /// <summary>
    /// 检查时间
    /// Time when health status was checked
    /// </summary>
    public required DateTimeOffset CheckedAt { get; init; }
}
