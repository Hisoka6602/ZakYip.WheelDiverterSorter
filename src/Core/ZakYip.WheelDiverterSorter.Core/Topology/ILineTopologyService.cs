namespace ZakYip.WheelDiverterSorter.Core.Topology;

/// <summary>
/// 线体拓扑服务接口 - 提供拓扑节点和边的访问能力
/// </summary>
/// <remarks>
/// <para>此接口只描述拓扑的逻辑结构，不涉及具体硬件设备。</para>
/// <para>路径规划、分拣逻辑应该依赖此接口，而不是直接依赖设备地址。</para>
/// </remarks>
public interface ILineTopologyService
{
    /// <summary>
    /// 获取所有拓扑节点
    /// </summary>
    IReadOnlyList<TopologyNode> Nodes { get; }

    /// <summary>
    /// 获取所有拓扑边
    /// </summary>
    IReadOnlyList<TopologyEdge> Edges { get; }

    /// <summary>
    /// 根据节点ID查找节点
    /// </summary>
    /// <param name="nodeId">节点ID</param>
    /// <returns>找到的节点，如果不存在返回 <see langword="null"/></returns>
    TopologyNode? FindNode(string nodeId);

    /// <summary>
    /// 获取指定节点的后继节点列表
    /// </summary>
    /// <param name="nodeId">节点ID</param>
    /// <returns>后继节点列表，如果节点不存在或无后继则返回空列表</returns>
    IReadOnlyList<TopologyNode> GetSuccessors(string nodeId);
}
