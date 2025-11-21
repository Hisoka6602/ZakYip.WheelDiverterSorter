namespace ZakYip.WheelDiverterSorter.Core.Topology;

/// <summary>
/// 线体拓扑 - 描述整条分拣线的物理结构
/// </summary>
/// <remarks>
/// 这是统一的拓扑模型，与厂商实现完全解耦。
/// 定义了分拣线的逻辑结构：节点、格口、传感器位置等。
/// </remarks>
public record class LineTopology
{
    /// <summary>
    /// 拓扑唯一标识符
    /// </summary>
    public required string TopologyId { get; init; }

    /// <summary>
    /// 拓扑名称
    /// </summary>
    public required string TopologyName { get; init; }

    /// <summary>
    /// 拓扑描述
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// 摆轮节点列表（按物理位置顺序）
    /// </summary>
    public required IReadOnlyList<DiverterNodeConfig> DiverterNodes { get; init; }

    /// <summary>
    /// 格口列表
    /// </summary>
    public required IReadOnlyList<ChuteConfig> Chutes { get; init; }

    /// <summary>
    /// 入口传感器逻辑名称
    /// </summary>
    /// <remarks>
    /// 例如: "EntrySensor", "SENSOR_ENTRY"
    /// 实际的IO点位由IoBindingProfile定义
    /// </remarks>
    public string? EntrySensorLogicalName { get; init; }

    /// <summary>
    /// 出口传感器逻辑名称
    /// </summary>
    public string? ExitSensorLogicalName { get; init; }

    /// <summary>
    /// 默认线速（毫米/秒）
    /// </summary>
    public decimal DefaultLineSpeedMmps { get; init; } = 500m;

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// 最后更新时间
    /// </summary>
    public DateTime UpdatedAt { get; init; }

    /// <summary>
    /// 获取异常格口
    /// </summary>
    public ChuteConfig? GetExceptionChute()
    {
        return Chutes.FirstOrDefault(c => c.IsExceptionChute);
    }

    /// <summary>
    /// 根据节点ID查找节点
    /// </summary>
    public DiverterNodeConfig? FindNodeById(string nodeId)
    {
        return DiverterNodes.FirstOrDefault(n => n.NodeId == nodeId);
    }

    /// <summary>
    /// 根据格口ID查找格口
    /// </summary>
    public ChuteConfig? FindChuteById(string chuteId)
    {
        return Chutes.FirstOrDefault(c => c.ChuteId == chuteId);
    }
}
