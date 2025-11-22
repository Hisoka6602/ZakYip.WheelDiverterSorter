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
    /// 线体段配置列表（描述节点之间的连接）
    /// </summary>
    /// <remarks>
    /// 定义从入口、摆轮到格口之间的所有线体段，包括长度和速度信息
    /// </remarks>
    public IReadOnlyList<LineSegmentConfig> LineSegments { get; init; } = Array.Empty<LineSegmentConfig>();

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
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// 最后更新时间
    /// </summary>
    public DateTime UpdatedAt { get; init; }

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

    /// <summary>
    /// 根据起始和目标节点ID查找线体段
    /// </summary>
    public LineSegmentConfig? FindSegment(string fromNodeId, string toNodeId)
    {
        return LineSegments.FirstOrDefault(s => 
            s.FromNodeId == fromNodeId && s.ToNodeId == toNodeId);
    }

    /// <summary>
    /// 获取指定格口的完整路径（从入口到格口的所有线体段）
    /// </summary>
    /// <param name="chuteId">格口ID</param>
    /// <returns>路径上的所有线体段，如果路径不存在则返回null</returns>
    public IReadOnlyList<LineSegmentConfig>? GetPathToChute(string chuteId)
    {
        var chute = FindChuteById(chuteId);
        if (chute == null)
        {
            return null;
        }

        var path = new List<LineSegmentConfig>();
        var currentNodeId = "ENTRY"; // 假设入口节点ID为 "ENTRY"
        var targetNodeId = chute.BoundNodeId;

        // 遍历摆轮节点，按位置索引顺序
        var sortedNodes = WheelNodes.OrderBy(n => n.PositionIndex).ToList();
        
        // 找到目标摆轮节点的位置
        var targetNode = FindNodeById(targetNodeId);
        if (targetNode == null)
        {
            return null;
        }

        // 从入口到目标摆轮，添加所有线体段
        foreach (var node in sortedNodes)
        {
            if (node.PositionIndex > targetNode.PositionIndex)
            {
                break;
            }

            var segment = FindSegment(currentNodeId, node.NodeId);
            if (segment == null)
            {
                return null; // 路径不完整
            }
            
            path.Add(segment);
            currentNodeId = node.NodeId;
        }

        // 添加从最后一个摆轮到格口的线体段（如果存在）
        var finalSegment = FindSegment(targetNodeId, chuteId);
        if (finalSegment != null)
        {
            path.Add(finalSegment);
        }

        return path;
    }

    /// <summary>
    /// 计算指定格口的路径总距离（毫米）
    /// </summary>
    /// <param name="chuteId">格口ID</param>
    /// <returns>路径总距离（毫米），如果路径不存在则返回null</returns>
    public double? CalculateTotalDistance(string chuteId)
    {
        var path = GetPathToChute(chuteId);
        if (path == null)
        {
            return null;
        }

        var chute = FindChuteById(chuteId);
        var dropOffsetMm = chute?.DropOffsetMm ?? 0;

        return path.Sum(s => s.LengthMm) + dropOffsetMm;
    }
}
