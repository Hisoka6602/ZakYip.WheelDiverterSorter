namespace ZakYip.WheelDiverterSorter.Core.LineModel.Topology.Legacy;

/// <summary>
/// 拓扑边 - 描述两个拓扑节点之间的连接关系
/// </summary>
/// <remarks>
/// <para>【遗留类型】此类型为遗留拓扑实现，后续版本中将移除。</para>
/// <para>拓扑边表示节点之间的逻辑连接，不包含物理设备信息。</para>
/// <para>边是有方向的，从 <see cref="FromNodeId"/> 指向 <see cref="ToNodeId"/>。</para>
/// </remarks>
[Obsolete("遗留拓扑实现，后续版本中将移除")]
public sealed record TopologyEdge
{
    /// <summary>
    /// 起始节点ID
    /// </summary>
    /// <remarks>
    /// 必须是拓扑中已存在的节点ID
    /// </remarks>
    /// <example>N001</example>
    public required string FromNodeId { get; init; }

    /// <summary>
    /// 目标节点ID
    /// </summary>
    /// <remarks>
    /// 必须是拓扑中已存在的节点ID
    /// </remarks>
    /// <example>N002</example>
    public required string ToNodeId { get; init; }
}
