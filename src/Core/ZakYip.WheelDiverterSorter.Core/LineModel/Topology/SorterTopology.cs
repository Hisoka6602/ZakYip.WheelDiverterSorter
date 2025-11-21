using ZakYip.WheelDiverterSorter.Core.Enums.Communication;
using ZakYip.WheelDiverterSorter.Core.Enums.Conveyor;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Enums.Routing;
using ZakYip.WheelDiverterSorter.Core.Enums.Sensors;
using ZakYip.WheelDiverterSorter.Core.Enums.System;

namespace ZakYip.WheelDiverterSorter.Core.LineModel.Topology;

/// <summary>
/// 表示摆轮分拣系统的拓扑结构配置
/// <para>
/// 描述一条直线输送线上的摆轮节点顺序，以及每个节点能分到哪些Chute。
/// 例如："入口 → 节点A → 节点B → 节点C → 末端"
/// </para>
/// </summary>
/// <remarks>
/// 此类用于支持 <see cref="ISwitchingPathGenerator"/> 的路径生成逻辑。
/// 当前为直线摆轮分拣的示例实现，采用内存/硬编码方式，后续可改为可配置方式。
/// </remarks>
public record class SorterTopology
{
    /// <summary>
    /// 拓扑配置的唯一标识符
    /// </summary>
    public required string TopologyId { get; init; }

    /// <summary>
    /// 拓扑配置的描述信息
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// 摆轮节点的有序列表，按照包裹在输送线上的移动顺序排列
    /// <para>
    /// 索引0为入口最近的第一个节点，依次类推到末端
    /// </para>
    /// </summary>
    /// <remarks>
    /// 列表顺序对应物理设备在输送线上的实际位置顺序。
    /// </remarks>
    public required IReadOnlyList<DiverterNode> Nodes { get; init; }

    /// <summary>
    /// 获取指定节点在拓扑中的位置索引
    /// </summary>
    /// <param name="nodeId">节点标识符</param>
    /// <returns>节点索引，如果不存在返回-1</returns>
    public int GetNodeIndex(string nodeId)
    {
        for (int i = 0; i < Nodes.Count; i++)
        {
            if (Nodes[i].NodeId == nodeId)
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>
    /// 根据格口ID查找能够到达该格口的节点和分拣方向
    /// </summary>
    /// <param name="chuteId">目标格口ID</param>
    /// <returns>
    /// 返回元组列表，每个元组包含（节点, 分拣方向）。
    /// 如果没有节点能到达该格口，返回空列表。
    /// </returns>
    public IReadOnlyList<(DiverterNode Node, DiverterSide Side)> FindRoutesToChute(string chuteId)
    {
        var routes = new List<(DiverterNode, DiverterSide)>();

        foreach (var node in Nodes)
        {
            foreach (var (side, chutes) in node.ChuteMapping)
            {
                if (chutes.Contains(chuteId))
                {
                    routes.Add((node, side));
                }
            }
        }

        return routes.AsReadOnly();
    }
}
