using ZakYip.WheelDiverterSorter.Core.Enums.Sorting;

namespace ZakYip.WheelDiverterSorter.Core.LineModel.Topology.Legacy;

/// <summary>
/// 拓扑节点 - 描述线体拓扑中的单个节点
/// </summary>
/// <remarks>
/// <para>【遗留类型】此类型为遗留拓扑实现，后续版本中将移除。</para>
/// <para>拓扑节点只描述节点的逻辑信息，不包含具体的硬件设备地址。</para>
/// <para>硬件设备的绑定通过 <see cref="DeviceBinding"/> 完成。</para>
/// </remarks>
[Obsolete("遗留拓扑实现，后续版本中将移除")]
public sealed record TopologyNode
{
    /// <summary>
    /// 节点唯一标识符
    /// </summary>
    /// <remarks>
    /// 例如: "N001", "DIVERTER_A", "CHUTE_01"
    /// </remarks>
    /// <example>N001</example>
    public required string NodeId { get; init; }

    /// <summary>
    /// 节点类型
    /// </summary>
    public required TopologyNodeType NodeType { get; init; }

    /// <summary>
    /// 节点显示名称（可选）
    /// </summary>
    /// <remarks>
    /// 用于界面显示的友好名称
    /// </remarks>
    /// <example>摆轮节点A</example>
    public string? DisplayName { get; init; }
}
