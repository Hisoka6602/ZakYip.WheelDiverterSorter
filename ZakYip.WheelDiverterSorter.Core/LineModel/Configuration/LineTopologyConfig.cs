namespace ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;

/// <summary>
/// 线体拓扑配置
/// </summary>
/// <remarks>
/// 描述整条分拣线的完整拓扑结构，包括所有节点、格口及其关系
/// </remarks>
public record class LineTopologyConfig
{
    /// <summary>
    /// 拓扑配置唯一标识符
    /// </summary>
    public required string TopologyId { get; init; }

    /// <summary>
    /// 拓扑配置名称
    /// </summary>
    public required string TopologyName { get; init; }

    /// <summary>
    /// 拓扑描述
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// 摆轮节点配置列表（按物理位置顺序排列）
    /// </summary>
    public required IReadOnlyList<WheelNodeConfig> WheelNodes { get; init; }

    /// <summary>
    /// 格口配置列表
    /// </summary>
    public required IReadOnlyList<ChuteConfig> Chutes { get; init; }

    /// <summary>
    /// 入口传感器ID
    /// </summary>
    public string? EntrySensorId { get; init; }

    /// <summary>
    /// 出口传感器ID
    /// </summary>
    public string? ExitSensorId { get; init; }

    /// <summary>
    /// 线速（毫米/秒）
    /// </summary>
    public decimal DefaultLineSpeedMmps { get; init; } = 500m;

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// 最后更新时间
    /// </summary>
    public DateTime UpdatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// 获取异常格口配置
    /// </summary>
    public ChuteConfig? GetExceptionChute()
    {
        return Chutes.FirstOrDefault(c => c.IsExceptionChute);
    }

    /// <summary>
    /// 根据节点ID查找节点配置
    /// </summary>
    public WheelNodeConfig? FindNodeById(string nodeId)
    {
        return WheelNodes.FirstOrDefault(n => n.NodeId == nodeId);
    }

    /// <summary>
    /// 根据格口ID查找格口配置
    /// </summary>
    public ChuteConfig? FindChuteById(string chuteId)
    {
        return Chutes.FirstOrDefault(c => c.ChuteId == chuteId);
    }
}
