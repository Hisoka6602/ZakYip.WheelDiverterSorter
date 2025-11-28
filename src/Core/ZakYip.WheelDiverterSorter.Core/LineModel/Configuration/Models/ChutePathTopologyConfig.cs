using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;

namespace ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;

/// <summary>
/// 格口路径拓扑配置
/// </summary>
/// <remarks>
/// <para>描述从入口到各个格口的完整路径拓扑结构。</para>
/// <para>本配置通过引用其他配置中已定义的ID来组织路径关系：</para>
/// <list type="bullet">
/// <item>IO配置 - 引用 SensorConfiguration 中的 SensorId</item>
/// <item>线体段配置 - 引用 LineSegmentConfig 中的 SegmentId</item>
/// <item>摆轮配置 - 引用 WheelDiverterConfiguration 中的 DiverterId</item>
/// </list>
/// 
/// <para><b>拓扑结构示例：</b></para>
/// <code>
///       格口B     格口D     格口F
///         ↑         ↑         ↑
/// 入口 → 摆轮D1 → 摆轮D2 → 摆轮D3 → 末端(默认异常口)
///   ↓     ↓         ↓         ↓
/// 传感器  格口A      格口C     格口E
/// </code>
/// </remarks>
public record class ChutePathTopologyConfig
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
    /// 入口传感器ID（引用 SensorConfiguration 中类型为 ParcelCreation 的传感器）
    /// </summary>
    /// <remarks>
    /// 必须引用一个已配置的 ParcelCreation 类型的感应IO
    /// </remarks>
    public required long EntrySensorId { get; init; }

    /// <summary>
    /// 摆轮路径节点列表（按物理位置顺序排列）
    /// </summary>
    /// <remarks>
    /// 每个节点描述一个摆轮及其关联的格口和线体段
    /// </remarks>
    public required IReadOnlyList<DiverterPathNode> DiverterNodes { get; init; }

    /// <summary>
    /// 末端异常格口ID
    /// </summary>
    /// <remarks>
    /// 当包裹无法分拣到任何目标格口时，将被导向此异常格口
    /// </remarks>
    public required long ExceptionChuteId { get; init; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// 最后更新时间
    /// </summary>
    public DateTime UpdatedAt { get; init; }

    /// <summary>
    /// 根据摆轮ID查找路径节点
    /// </summary>
    /// <param name="diverterId">摆轮ID</param>
    /// <returns>匹配的路径节点，如不存在则返回null</returns>
    public DiverterPathNode? FindNodeByDiverterId(long diverterId)
    {
        return DiverterNodes.FirstOrDefault(n => n.DiverterId == diverterId);
    }

    /// <summary>
    /// 根据格口ID查找对应的摆轮路径节点
    /// </summary>
    /// <param name="chuteId">格口ID</param>
    /// <returns>包含该格口的路径节点，如不存在则返回null</returns>
    public DiverterPathNode? FindNodeByChuteId(long chuteId)
    {
        return DiverterNodes.FirstOrDefault(n => 
            n.LeftChuteIds.Contains(chuteId) || 
            n.RightChuteIds.Contains(chuteId));
    }

    /// <summary>
    /// 获取到达指定格口需要经过的所有摆轮节点
    /// </summary>
    /// <param name="chuteId">目标格口ID</param>
    /// <returns>从入口到目标格口的摆轮路径，如不存在则返回null</returns>
    public IReadOnlyList<DiverterPathNode>? GetPathToChute(long chuteId)
    {
        var targetNode = FindNodeByChuteId(chuteId);
        if (targetNode == null)
        {
            return null;
        }

        // 返回从入口到目标摆轮的所有节点（按位置索引排序）
        return DiverterNodes
            .Where(n => n.PositionIndex <= targetNode.PositionIndex)
            .OrderBy(n => n.PositionIndex)
            .ToList();
    }

    /// <summary>
    /// 获取指定格口的分拣方向
    /// </summary>
    /// <param name="chuteId">格口ID</param>
    /// <returns>分拣方向（Left/Right），如格口不存在则返回null</returns>
    public DiverterDirection? GetChuteDirection(long chuteId)
    {
        var node = FindNodeByChuteId(chuteId);
        if (node == null)
        {
            return null;
        }

        if (node.LeftChuteIds.Contains(chuteId))
        {
            return DiverterDirection.Left;
        }
        
        if (node.RightChuteIds.Contains(chuteId))
        {
            return DiverterDirection.Right;
        }

        return null;
    }

    /// <summary>
    /// 获取所有格口的总数
    /// </summary>
    public int TotalChuteCount => DiverterNodes.Sum(n => n.LeftChuteIds.Count + n.RightChuteIds.Count);
}

/// <summary>
/// 摆轮路径节点
/// </summary>
/// <remarks>
/// <para>描述单个摆轮在路径中的配置，包括：</para>
/// <list type="bullet">
/// <item>摆轮ID - 引用已配置的摆轮设备</item>
/// <item>前置线体段ID - 到达此摆轮需要经过的线体段</item>
/// <item>左右侧格口ID - 此摆轮左转/右转对应的格口</item>
/// <item>摆轮前感应IO ID - 可选，用于检测包裹即将到达摆轮</item>
/// </list>
/// </remarks>
public record class DiverterPathNode
{
    /// <summary>
    /// 摆轮ID（引用 WheelDiverterConfiguration 中的摆轮设备）
    /// </summary>
    public required long DiverterId { get; init; }

    /// <summary>
    /// 摆轮显示名称
    /// </summary>
    public string? DiverterName { get; init; }

    /// <summary>
    /// 物理位置索引（从入口开始的顺序，从1开始）
    /// </summary>
    public required int PositionIndex { get; init; }

    /// <summary>
    /// 前置线体段ID（引用 LineSegmentConfig 中的 SegmentId）
    /// </summary>
    /// <remarks>
    /// 从上一个节点（入口或上一个摆轮）到本摆轮的线体段
    /// </remarks>
    public required long SegmentId { get; init; }

    /// <summary>
    /// 摆轮前感应IO的ID（引用 SensorConfiguration 中的 SensorId，必须配置）
    /// </summary>
    /// <remarks>
    /// 类型必须为 WheelFront，用于检测包裹是否已经到达摆轮前。
    /// 此字段为必填项，因为需要依靠感应器来判断包裹是否已经到达摆轮前。
    /// </remarks>
    public required long FrontSensorId { get; init; }

    /// <summary>
    /// 左侧格口ID列表
    /// </summary>
    /// <remarks>
    /// 摆轮左转时可分拣到的格口
    /// </remarks>
    public IReadOnlyList<long> LeftChuteIds { get; init; } = Array.Empty<long>();

    /// <summary>
    /// 右侧格口ID列表
    /// </summary>
    /// <remarks>
    /// 摆轮右转时可分拣到的格口
    /// </remarks>
    public IReadOnlyList<long> RightChuteIds { get; init; } = Array.Empty<long>();

    /// <summary>
    /// 备注信息
    /// </summary>
    public string? Remarks { get; init; }

    /// <summary>
    /// 左侧是否有格口
    /// </summary>
    public bool HasLeftChute => LeftChuteIds.Count > 0;

    /// <summary>
    /// 右侧是否有格口
    /// </summary>
    public bool HasRightChute => RightChuteIds.Count > 0;
}
