namespace ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;

/// <summary>
/// 线体拓扑配置
/// </summary>
/// <remarks>
/// 描述整条分拣线的完整拓扑结构，包括线体段、摆轮节点、格口及其关系。
/// 
/// **拓扑组成规则**：
/// 一个最简的摆轮分拣拓扑由以下元素组成：
/// - 创建包裹感应IO -> 线体段 -> 摆轮 -> 格口（摆轮方向=格口）
/// 
/// **多段线体规则**：
/// - 第一段线体的起点IO必须是创建包裹的IO（ParcelCreation类型）
/// - 最后一段线体的终点IO Id应该是0（表示已到达末端）
/// - 中间线体段的起点/终点IO通常是摆轮前感应IO（WheelFront类型）
/// 
/// **计算用途**：
/// - 根据线体长度和速度计算包裹从上一个IO到下一个IO的理论时间
/// - 用于超时检测和丢包判断逻辑
/// </remarks>
public record class LineTopologyConfig
{
    /// <summary>
    /// 入口节点ID常量（向后兼容）
    /// </summary>
    [Obsolete("使用新的感应IO模型，请通过 LineSegments 中的 StartIoId/EndIoId 定义拓扑")]
    public const string EntryNodeId = "ENTRY";

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
    /// 线体段配置列表
    /// </summary>
    /// <remarks>
    /// 定义所有线体段，每段由起点IO和终点IO定义：
    /// - 第一段的起点IO是创建包裹感应IO
    /// - 中间段的起点/终点IO通常是摆轮前感应IO
    /// - 最后一段的终点IO Id为0（末端）
    /// </remarks>
    public IReadOnlyList<LineSegmentConfig> LineSegments { get; init; } = Array.Empty<LineSegmentConfig>();

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

    #region 向后兼容 - 废弃字段

    /// <summary>
    /// [废弃] 入口传感器ID - 使用 LineSegments 中第一段的 StartIoId
    /// </summary>
    [Obsolete("请使用 LineSegments 中第一段的 StartIoId")]
    public string? EntrySensorId { get; init; }

    /// <summary>
    /// [废弃] 出口传感器ID - 使用 LineSegments 中最后一段的 EndIoId
    /// </summary>
    [Obsolete("请使用 LineSegments 中最后一段的 EndIoId")]
    public string? ExitSensorId { get; init; }

    #endregion

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
    /// 根据起点IO Id查找线体段
    /// </summary>
    /// <param name="startIoId">起点感应IO的Id</param>
    /// <returns>匹配的线体段，如不存在则返回null</returns>
    public LineSegmentConfig? FindSegmentByStartIoId(long startIoId)
    {
        return LineSegments.FirstOrDefault(s => s.StartIoId == startIoId);
    }

    /// <summary>
    /// 根据终点IO Id查找线体段
    /// </summary>
    /// <param name="endIoId">终点感应IO的Id</param>
    /// <returns>匹配的线体段，如不存在则返回null</returns>
    public LineSegmentConfig? FindSegmentByEndIoId(long endIoId)
    {
        return LineSegments.FirstOrDefault(s => s.EndIoId == endIoId);
    }

    /// <summary>
    /// 获取从起点IO到目标IO的所有线体段路径
    /// </summary>
    /// <param name="startIoId">起点感应IO的Id</param>
    /// <param name="endIoId">终点感应IO的Id（0表示到末端）</param>
    /// <returns>路径上的所有线体段，如果路径不存在则返回null</returns>
    public IReadOnlyList<LineSegmentConfig>? GetPathBetweenIos(long startIoId, long endIoId)
    {
        var path = new List<LineSegmentConfig>();
        var currentIoId = startIoId;
        var visited = new HashSet<long>();

        while (currentIoId != endIoId)
        {
            if (visited.Contains(currentIoId))
            {
                return null; // 检测到循环，返回null
            }
            visited.Add(currentIoId);

            var segment = FindSegmentByStartIoId(currentIoId);
            if (segment == null)
            {
                return null; // 路径不完整
            }

            path.Add(segment);
            currentIoId = segment.EndIoId;

            // 如果到达末端（EndIoId为0），且目标也是0，则结束
            if (currentIoId == 0 && endIoId == 0)
            {
                break;
            }
        }

        return path;
    }

    /// <summary>
    /// 计算两个IO之间的总距离（毫米）
    /// </summary>
    /// <param name="startIoId">起点感应IO的Id</param>
    /// <param name="endIoId">终点感应IO的Id（0表示到末端）</param>
    /// <returns>总距离（毫米），如果路径不存在则返回null</returns>
    public double? CalculateDistanceBetweenIos(long startIoId, long endIoId)
    {
        var path = GetPathBetweenIos(startIoId, endIoId);
        if (path == null || path.Count == 0)
        {
            return null;
        }

        return path.Sum(s => s.LengthMm);
    }

    /// <summary>
    /// 计算两个IO之间的理论通过时间（毫秒）
    /// </summary>
    /// <param name="startIoId">起点感应IO的Id</param>
    /// <param name="endIoId">终点感应IO的Id（0表示到末端）</param>
    /// <returns>理论通过时间（毫秒），如果路径不存在则返回null</returns>
    public double? CalculateTransitTimeBetweenIos(long startIoId, long endIoId)
    {
        var path = GetPathBetweenIos(startIoId, endIoId);
        if (path == null || path.Count == 0)
        {
            return null;
        }

        return path.Sum(s => s.CalculateTransitTimeMs());
    }

    #region 向后兼容方法

    /// <summary>
    /// [废弃] 根据起始和目标节点ID查找线体段
    /// </summary>
    [Obsolete("请使用 FindSegmentByStartIoId 或 FindSegmentByEndIoId")]
    public LineSegmentConfig? FindSegment(string fromNodeId, string toNodeId)
    {
#pragma warning disable CS0618 // Type or member is obsolete
        return LineSegments.FirstOrDefault(s => 
            s.FromNodeId == fromNodeId && s.ToNodeId == toNodeId);
#pragma warning restore CS0618 // Type or member is obsolete
    }

    /// <summary>
    /// [废弃] 获取指定格口的完整路径（从入口到格口的所有线体段）
    /// </summary>
    /// <param name="chuteId">格口ID</param>
    /// <returns>路径上的所有线体段，如果路径不存在则返回null</returns>
    [Obsolete("请使用 GetPathBetweenIos，通过感应IO Id定义路径")]
    public IReadOnlyList<LineSegmentConfig>? GetPathToChute(string chuteId)
    {
        var chute = FindChuteById(chuteId);
        if (chute == null)
        {
            return null;
        }

        var path = new List<LineSegmentConfig>();
#pragma warning disable CS0618 // Type or member is obsolete
        var currentNodeId = EntryNodeId;
#pragma warning restore CS0618 // Type or member is obsolete
        var targetNodeId = chute.BoundNodeId;

        // 遍历摆轮节点，按位置索引顺序
        var sortedNodes = WheelNodes.OrderBy(n => n.PositionIndex).ToList();
        
        // 找到目标摆轮节点的位置
        var targetNode = WheelNodes.FirstOrDefault(n => n.NodeId == targetNodeId);
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

#pragma warning disable CS0618 // Type or member is obsolete
            var segment = FindSegment(currentNodeId, node.NodeId);
#pragma warning restore CS0618 // Type or member is obsolete
            if (segment == null)
            {
                return null; // 路径不完整
            }
            
            path.Add(segment);
            currentNodeId = node.NodeId;
        }

        // 添加从最后一个摆轮到格口的线体段（如果存在）
#pragma warning disable CS0618 // Type or member is obsolete
        var finalSegment = FindSegment(targetNodeId, chuteId);
#pragma warning restore CS0618 // Type or member is obsolete
        if (finalSegment != null)
        {
            path.Add(finalSegment);
        }

        return path;
    }

    /// <summary>
    /// [废弃] 计算指定格口的路径总距离（毫米）
    /// </summary>
    /// <param name="chuteId">格口ID</param>
    /// <returns>路径总距离（毫米），如果路径不存在则返回null</returns>
    [Obsolete("请使用 CalculateDistanceBetweenIos，通过感应IO Id计算距离")]
    public double? CalculateTotalDistance(string chuteId)
    {
#pragma warning disable CS0618 // Type or member is obsolete
        var path = GetPathToChute(chuteId);
#pragma warning restore CS0618 // Type or member is obsolete
        if (path == null)
        {
            return null;
        }

        var chute = FindChuteById(chuteId);
        var dropOffsetMm = chute?.DropOffsetMm ?? 0;

        return path.Sum(s => s.LengthMm) + dropOffsetMm;
    }

    #endregion
}
