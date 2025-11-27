using ZakYip.WheelDiverterSorter.Core.Enums.Sorting;

namespace ZakYip.WheelDiverterSorter.Core.Topology.Services;

/// <summary>
/// JSON配置的线体拓扑服务实现
/// </summary>
/// <remarks>
/// <para>从配置（JSON/appsettings）加载拓扑节点和边的信息。</para>
/// <para>此实现提供了基于内存的拓扑访问能力。</para>
/// </remarks>
public sealed class JsonLineTopologyService : ILineTopologyService
{
    private readonly List<TopologyNode> _nodes;
    private readonly List<TopologyEdge> _edges;
    private readonly Dictionary<string, TopologyNode> _nodeIndex;
    private readonly Dictionary<string, List<TopologyNode>> _successorIndex;

    /// <summary>
    /// 使用指定的节点和边初始化拓扑服务
    /// </summary>
    /// <param name="nodes">拓扑节点列表</param>
    /// <param name="edges">拓扑边列表</param>
    public JsonLineTopologyService(IEnumerable<TopologyNode>? nodes = null, IEnumerable<TopologyEdge>? edges = null)
    {
        _nodes = nodes?.ToList() ?? new List<TopologyNode>();
        _edges = edges?.ToList() ?? new List<TopologyEdge>();
        
        // 构建节点索引
        _nodeIndex = _nodes.ToDictionary(n => n.NodeId, n => n);
        
        // 构建后继节点索引
        _successorIndex = new Dictionary<string, List<TopologyNode>>();
        foreach (var edge in _edges)
        {
            if (!_successorIndex.TryGetValue(edge.FromNodeId, out var successors))
            {
                successors = new List<TopologyNode>();
                _successorIndex[edge.FromNodeId] = successors;
            }
            
            if (_nodeIndex.TryGetValue(edge.ToNodeId, out var toNode))
            {
                successors.Add(toNode);
            }
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<TopologyNode> Nodes => _nodes.AsReadOnly();

    /// <inheritdoc/>
    public IReadOnlyList<TopologyEdge> Edges => _edges.AsReadOnly();

    /// <inheritdoc/>
    public TopologyNode? FindNode(string nodeId)
    {
        if (string.IsNullOrEmpty(nodeId))
        {
            return null;
        }
        
        return _nodeIndex.TryGetValue(nodeId, out var node) ? node : null;
    }

    /// <inheritdoc/>
    public IReadOnlyList<TopologyNode> GetSuccessors(string nodeId)
    {
        if (string.IsNullOrEmpty(nodeId))
        {
            return Array.Empty<TopologyNode>();
        }
        
        return _successorIndex.TryGetValue(nodeId, out var successors) 
            ? successors.AsReadOnly() 
            : Array.Empty<TopologyNode>();
    }

    /// <summary>
    /// 创建默认的拓扑服务实例（用于测试或示例）
    /// </summary>
    /// <returns>包含示例拓扑的服务实例</returns>
    public static JsonLineTopologyService CreateDefault()
    {
        var nodes = new List<TopologyNode>
        {
            new TopologyNode { NodeId = "ENTRY", NodeType = TopologyNodeType.Induction, DisplayName = "入口" },
            new TopologyNode { NodeId = "D1", NodeType = TopologyNodeType.WheelDiverter, DisplayName = "摆轮D1" },
            new TopologyNode { NodeId = "D2", NodeType = TopologyNodeType.WheelDiverter, DisplayName = "摆轮D2" },
            new TopologyNode { NodeId = "D3", NodeType = TopologyNodeType.WheelDiverter, DisplayName = "摆轮D3" },
            new TopologyNode { NodeId = "CHUTE_1", NodeType = TopologyNodeType.Chute, DisplayName = "格口1" },
            new TopologyNode { NodeId = "CHUTE_2", NodeType = TopologyNodeType.Chute, DisplayName = "格口2" },
            new TopologyNode { NodeId = "CHUTE_3", NodeType = TopologyNodeType.Chute, DisplayName = "格口3" },
            new TopologyNode { NodeId = "CHUTE_EXCEPTION", NodeType = TopologyNodeType.Chute, DisplayName = "异常格口" }
        };

        var edges = new List<TopologyEdge>
        {
            new TopologyEdge { FromNodeId = "ENTRY", ToNodeId = "D1" },
            new TopologyEdge { FromNodeId = "D1", ToNodeId = "D2" },
            new TopologyEdge { FromNodeId = "D1", ToNodeId = "CHUTE_1" },
            new TopologyEdge { FromNodeId = "D2", ToNodeId = "D3" },
            new TopologyEdge { FromNodeId = "D2", ToNodeId = "CHUTE_2" },
            new TopologyEdge { FromNodeId = "D3", ToNodeId = "CHUTE_3" },
            new TopologyEdge { FromNodeId = "D3", ToNodeId = "CHUTE_EXCEPTION" }
        };

        return new JsonLineTopologyService(nodes, edges);
    }
}
