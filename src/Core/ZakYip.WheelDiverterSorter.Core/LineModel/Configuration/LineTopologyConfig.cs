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
}
