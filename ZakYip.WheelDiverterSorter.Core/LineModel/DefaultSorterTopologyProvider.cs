using ZakYip.WheelDiverterSorter.Core.LineModel.Enums;

namespace ZakYip.WheelDiverterSorter.Core.LineModel;

/// <summary>
/// 提供默认的摆轮拓扑配置
/// <para>
/// 直线摆轮分拣示例：采用内存/硬编码方式实现，描述一条直线输送线上的摆轮节点序列。
/// </para>
/// </summary>
/// <remarks>
/// 此类用于支持 <see cref="ISwitchingPathGenerator"/> 的路径生成逻辑。
/// 当前为示例性实现，实际生产环境应从配置文件或数据库加载拓扑信息。
/// 后续可通过依赖注入或配置系统替换为可配置的实现。
/// </remarks>
public static class DefaultSorterTopologyProvider
{
    /// <summary>
    /// 获取默认的直线摆轮分拣拓扑配置
    /// </summary>
    /// <returns>
    /// 直线摆轮拓扑示例，包含从入口到末端的完整节点序列。
    /// 拓扑结构示例："入口 → 节点A → 节点B → 节点C → 末端"
    /// </returns>
    /// <remarks>
    /// <para><b>拓扑说明：</b></para>
    /// <list type="bullet">
    /// <item><description><b>节点A (DIVERTER_A)</b>: 第一个摆轮节点，支持直行和左转</description></item>
    /// <item><description>  - 直行: 包裹继续前进到下一个节点</description></item>
    /// <item><description>  - 左转: 包裹分拣到格口 CHUTE_A1, CHUTE_A2</description></item>
    /// <item><description><b>节点B (DIVERTER_B)</b>: 第二个摆轮节点，支持直行和右转</description></item>
    /// <item><description>  - 直行: 包裹继续前进到下一个节点</description></item>
    /// <item><description>  - 右转: 包裹分拣到格口 CHUTE_B1</description></item>
    /// <item><description><b>节点C (DIVERTER_C)</b>: 第三个摆轮节点，支持直行、左转和右转</description></item>
    /// <item><description>  - 直行: 包裹到达末端（异常口或默认格口）</description></item>
    /// <item><description>  - 左转: 包裹分拣到格口 CHUTE_C1</description></item>
    /// <item><description>  - 右转: 包裹分拣到格口 CHUTE_C2, CHUTE_C3</description></item>
    /// </list>
    /// <para>
    /// 包裹在输送线上的移动顺序：入口 → 节点A → 节点B → 节点C → 末端
    /// </para>
    /// </remarks>
    public static SorterTopology GetDefaultTopology()
    {
        // 节点A: 第一个摆轮，支持直行和左转
        var nodeA = new DiverterNode
        {
            NodeId = "DIVERTER_A",
            NodeName = "摆轮节点A",
            SupportedActions = new List<DiverterSide>
            {
                DiverterSide.Straight,  // 直行：包裹继续前进
                DiverterSide.Left       // 左转：分拣到格口
            }.AsReadOnly(),
            ChuteMapping = new Dictionary<DiverterSide, IReadOnlyList<string>>
            {
                // 左转可到达的格口
                [DiverterSide.Left] = new List<string> { "CHUTE_A1", "CHUTE_A2" }.AsReadOnly(),
                // 直行表示包裹继续前进到下一个节点，不分拣到格口
                [DiverterSide.Straight] = new List<string>().AsReadOnly()
            }
        };

        // 节点B: 第二个摆轮，支持直行和右转
        var nodeB = new DiverterNode
        {
            NodeId = "DIVERTER_B",
            NodeName = "摆轮节点B",
            SupportedActions = new List<DiverterSide>
            {
                DiverterSide.Straight,  // 直行：包裹继续前进
                DiverterSide.Right      // 右转：分拣到格口
            }.AsReadOnly(),
            ChuteMapping = new Dictionary<DiverterSide, IReadOnlyList<string>>
            {
                // 右转可到达的格口
                [DiverterSide.Right] = new List<string> { "CHUTE_B1" }.AsReadOnly(),
                // 直行表示包裹继续前进到下一个节点
                [DiverterSide.Straight] = new List<string>().AsReadOnly()
            }
        };

        // 节点C: 第三个摆轮，支持直行、左转和右转
        var nodeC = new DiverterNode
        {
            NodeId = "DIVERTER_C",
            NodeName = "摆轮节点C",
            SupportedActions = new List<DiverterSide>
            {
                DiverterSide.Straight,  // 直行：到达末端
                DiverterSide.Left,      // 左转：分拣到格口
                DiverterSide.Right      // 右转：分拣到格口
            }.AsReadOnly(),
            ChuteMapping = new Dictionary<DiverterSide, IReadOnlyList<string>>
            {
                // 左转可到达的格口
                [DiverterSide.Left] = new List<string> { "CHUTE_C1" }.AsReadOnly(),
                // 右转可到达的格口
                [DiverterSide.Right] = new List<string> { "CHUTE_C2", "CHUTE_C3" }.AsReadOnly(),
                // 直行表示包裹到达末端（异常口或默认格口）
                [DiverterSide.Straight] = new List<string> { "CHUTE_END" }.AsReadOnly()
            }
        };

        // 构建完整的拓扑配置：入口 → A → B → C → 末端
        return new SorterTopology
        {
            TopologyId = "DEFAULT_LINEAR_TOPOLOGY",
            Description = "默认直线摆轮分拣拓扑 - 示例配置",
            // 按照物理顺序排列节点：从入口到末端
            Nodes = new List<DiverterNode> { nodeA, nodeB, nodeC }.AsReadOnly()
        };
    }
}
