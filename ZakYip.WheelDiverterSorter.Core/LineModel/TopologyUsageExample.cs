using ZakYip.WheelDiverterSorter.Core.LineModel.Enums;

namespace ZakYip.WheelDiverterSorter.Core.LineModel;

/// <summary>
/// 摆轮拓扑配置的使用示例
/// <para>
/// 演示如何使用 <see cref="SorterTopology"/> 和 <see cref="DiverterNode"/> 
/// 来描述直线摆轮分拣系统的拓扑结构
/// </para>
/// </summary>
/// <remarks>
/// 这是一个示例类，展示了拓扑配置的基本用法。
/// 在实际应用中，路径生成器会使用这些配置来生成包裹的分拣路径。
/// </remarks>
public static class TopologyUsageExample
{
    /// <summary>
    /// 演示如何加载和使用默认拓扑配置
    /// </summary>
    /// <example>
    /// <code>
    /// // 获取默认拓扑配置
    /// var topology = DefaultSorterTopologyProvider.GetDefaultTopology();
    /// 
    /// // 打印拓扑信息
    /// Console.WriteLine($"拓扑ID: {topology.TopologyId}");
    /// Console.WriteLine($"描述: {topology.Description}");
    /// Console.WriteLine($"节点数量: {topology.Nodes.Count}");
    /// 
    /// // 遍历所有节点
    /// foreach (var node in topology.Nodes)
    /// {
    ///     Console.WriteLine($"\n节点: {node.NodeName} ({node.NodeId})");
    ///     Console.WriteLine($"  支持的动作: {string.Join(", ", node.SupportedActions)}");
    ///     
    ///     // 显示每个方向的格口映射
    ///     foreach (var (side, chutes) in node.ChuteMapping)
    ///     {
    ///         if (chutes.Count > 0)
    ///         {
    ///             Console.WriteLine($"  {side}: {string.Join(", ", chutes)}");
    ///         }
    ///     }
    /// }
    /// 
    /// // 查找到达特定格口的路径
    /// var routes = topology.FindRoutesToChute("CHUTE_A1");
    /// foreach (var (node, side) in routes)
    /// {
    ///     Console.WriteLine($"格口 CHUTE_A1 可通过节点 {node.NodeName} 的 {side} 方向到达");
    /// }
    /// </code>
    /// </example>
    public static void DemonstrateBasicUsage()
    {
        // 获取默认拓扑配置
        var topology = DefaultSorterTopologyProvider.GetDefaultTopology();

        // 示例1: 查看拓扑基本信息
        // topology.TopologyId: "DEFAULT_LINEAR_TOPOLOGY"
        // topology.Description: "默认直线摆轮分拣拓扑 - 示例配置"
        // topology.Nodes.Count: 3 (A, B, C)

        // 示例2: 检查特定节点的能力
        var nodeA = topology.Nodes[0]; // 第一个节点
        // nodeA.NodeId: "DIVERTER_A"
        // nodeA.SupportedActions: [Straight, Left]
        // nodeA.ChuteMapping[Left]: ["CHUTE_A1", "CHUTE_A2"]

        // 示例3: 查找到达特定格口的所有可能路径
        var routesToA1 = topology.FindRoutesToChute("CHUTE_A1");
        // 返回: [(DiverterNode(DIVERTER_A), DiverterSide.Left)]

        // 示例4: 获取节点在拓扑中的位置
        var indexOfB = topology.GetNodeIndex("DIVERTER_B");
        // 返回: 1 (第二个位置，索引从0开始)
    }

    /// <summary>
    /// 演示路径生成器如何使用拓扑配置
    /// </summary>
    /// <remarks>
    /// 这个示例展示了路径生成器的典型使用场景：
    /// 1. 根据目标格口查找合适的节点
    /// 2. 确定需要的分拣动作
    /// 3. 生成摆轮指令序列
    /// </remarks>
    public static void DemonstratePathGeneratorUsage()
    {
        var topology = DefaultSorterTopologyProvider.GetDefaultTopology();

        // 假设目标是格口 "CHUTE_C2"
        string targetChute = "CHUTE_C2";

        // 步骤1: 查找能到达该格口的所有路径
        var routes = topology.FindRoutesToChute(targetChute);

        if (routes.Count == 0)
        {
            // 没有找到路径，包裹应该走异常口
            return;
        }

        // 步骤2: 选择第一条路径（实际场景可能需要更复杂的选择逻辑）
        var (targetNode, targetSide) = routes[0];
        // targetNode: DiverterNode(DIVERTER_C)
        // targetSide: DiverterSide.Right

        // 步骤3: 生成路径
        // 从入口到目标节点，之前的所有节点都应该设置为 "直行"
        // 只有目标节点设置为需要的分拣方向
        var nodeIndex = topology.GetNodeIndex(targetNode.NodeId);

        // 生成指令序列（简化示例）：
        // - 节点A (索引0): 直行
        // - 节点B (索引1): 直行  
        // - 节点C (索引2): 右转 -> 到达 CHUTE_C2
    }

    /// <summary>
    /// 演示如何验证路径的可行性
    /// </summary>
    public static bool ValidatePath(SorterTopology topology, string targetChute)
    {
        // 检查1: 格口是否存在于拓扑中
        var routes = topology.FindRoutesToChute(targetChute);
        if (routes.Count == 0)
        {
            // 没有节点能到达该格口
            return false;
        }

        // 检查2: 确保至少有一个节点支持所需的分拣动作
        foreach (var (node, side) in routes)
        {
            if (node.SupportedActions.Contains(side))
            {
                // 找到至少一条可行路径
                return true;
            }
        }

        // 所有路径都不可行（理论上不应该发生，因为ChuteMapping已经定义了映射）
        return false;
    }
}
