using ZakYip.WheelDiverterSorter.Core.LineModel.Enums;
using ZakYip.WheelDiverterSorter.Core.LineModel.Topology;

namespace ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;

/// <summary>
/// 拓扑配置转换器
/// </summary>
/// <remarks>
/// 提供在新旧拓扑配置模型之间转换的工具方法
/// </remarks>
public static class TopologyConfigConverter
{
    /// <summary>
    /// 将 LineTopologyConfig 转换为 SorterTopology
    /// </summary>
    /// <param name="lineTopology">线体拓扑配置</param>
    /// <returns>摆轮分拣拓扑</returns>
    public static SorterTopology ToSorterTopology(LineTopologyConfig lineTopology)
    {
        if (lineTopology == null)
        {
            throw new ArgumentNullException(nameof(lineTopology));
        }

        var nodes = lineTopology.WheelNodes
            .OrderBy(n => n.PositionIndex)
            .Select(wheelNode => new DiverterNode
            {
                NodeId = wheelNode.NodeId,
                NodeName = wheelNode.NodeName,
                SupportedActions = wheelNode.SupportedSides.ToList().AsReadOnly(),
                ChuteMapping = CreateChuteMapping(wheelNode)
            })
            .ToList()
            .AsReadOnly();

        return new SorterTopology
        {
            TopologyId = lineTopology.TopologyId,
            Description = lineTopology.Description ?? lineTopology.TopologyName,
            Nodes = nodes
        };
    }

    /// <summary>
    /// 将 SorterTopology 转换为 LineTopologyConfig
    /// </summary>
    /// <param name="sorterTopology">摆轮分拣拓扑</param>
    /// <param name="topologyName">拓扑名称（可选）</param>
    /// <returns>线体拓扑配置</returns>
    public static LineTopologyConfig FromSorterTopology(SorterTopology sorterTopology, string? topologyName = null)
    {
        if (sorterTopology == null)
        {
            throw new ArgumentNullException(nameof(sorterTopology));
        }

        var wheelNodes = new List<WheelNodeConfig>();
        var chutes = new List<ChuteConfig>();

        for (int i = 0; i < sorterTopology.Nodes.Count; i++)
        {
            var node = sorterTopology.Nodes[i];
            
            var leftChuteIds = node.ChuteMapping.TryGetValue(DiverterSide.Left, out var leftChutes) 
                ? leftChutes.ToList() 
                : new List<string>();
            var rightChuteIds = node.ChuteMapping.TryGetValue(DiverterSide.Right, out var rightChutes) 
                ? rightChutes.ToList() 
                : new List<string>();
            var straightChuteIds = node.ChuteMapping.TryGetValue(DiverterSide.Straight, out var straightChutes) 
                ? straightChutes.ToList() 
                : new List<string>();

            wheelNodes.Add(new WheelNodeConfig
            {
                NodeId = node.NodeId,
                NodeName = node.NodeName,
                PositionIndex = i,
                HasLeftChute = leftChuteIds.Count > 0,
                HasRightChute = rightChuteIds.Count > 0,
                LeftChuteIds = leftChuteIds,
                RightChuteIds = rightChuteIds,
                SupportedSides = node.SupportedActions.ToList()
            });

            // 创建格口配置
            foreach (var chuteId in leftChuteIds)
            {
                chutes.Add(new ChuteConfig
                {
                    ChuteId = chuteId,
                    ChuteName = chuteId,
                    BoundNodeId = node.NodeId,
                    BoundDirection = "Left",
                    IsExceptionChute = false
                });
            }

            foreach (var chuteId in rightChuteIds)
            {
                chutes.Add(new ChuteConfig
                {
                    ChuteId = chuteId,
                    ChuteName = chuteId,
                    BoundNodeId = node.NodeId,
                    BoundDirection = "Right",
                    IsExceptionChute = false
                });
            }

            foreach (var chuteId in straightChuteIds)
            {
                chutes.Add(new ChuteConfig
                {
                    ChuteId = chuteId,
                    ChuteName = chuteId,
                    BoundNodeId = node.NodeId,
                    BoundDirection = "Straight",
                    IsExceptionChute = true // 直行通常是异常格口
                });
            }
        }

        return new LineTopologyConfig
        {
            TopologyId = sorterTopology.TopologyId,
            TopologyName = topologyName ?? sorterTopology.Description,
            Description = sorterTopology.Description,
            WheelNodes = wheelNodes,
            Chutes = chutes
        };
    }

    private static IReadOnlyDictionary<DiverterSide, IReadOnlyList<string>> CreateChuteMapping(WheelNodeConfig wheelNode)
    {
        var mapping = new Dictionary<DiverterSide, IReadOnlyList<string>>();

        if (wheelNode.LeftChuteIds.Any())
        {
            mapping[DiverterSide.Left] = wheelNode.LeftChuteIds;
        }
        else if (wheelNode.SupportedSides.Contains(DiverterSide.Left))
        {
            mapping[DiverterSide.Left] = Array.Empty<string>();
        }

        if (wheelNode.RightChuteIds.Any())
        {
            mapping[DiverterSide.Right] = wheelNode.RightChuteIds;
        }
        else if (wheelNode.SupportedSides.Contains(DiverterSide.Right))
        {
            mapping[DiverterSide.Right] = Array.Empty<string>();
        }

        if (wheelNode.SupportedSides.Contains(DiverterSide.Straight))
        {
            mapping[DiverterSide.Straight] = Array.Empty<string>();
        }

        return mapping;
    }
}
