using ZakYip.WheelDiverterSorter.Core.Enums;

namespace ZakYip.WheelDiverterSorter.Core.IoBinding;

/// <summary>
/// IO点位描述符 - 描述逻辑IO点的抽象信息
/// </summary>
/// <remarks>
/// 这是厂商无关的逻辑IO点定义。
/// 厂商驱动负责将此逻辑描述符映射到实际硬件地址。
/// </remarks>
public record class IoPointDescriptor
{
    /// <summary>
    /// 逻辑名称
    /// </summary>
    /// <remarks>
    /// 例如: "EntrySensor", "D1_Left", "D1_Right", "D2_Sensor"
    /// 这是在拓扑模型中引用的名称
    /// </remarks>
    public required string LogicalName { get; init; }

    /// <summary>
    /// IO类型
    /// </summary>
    public required IoPointType IoType { get; init; }

    /// <summary>
    /// 是否反相
    /// </summary>
    /// <remarks>
    /// true: 高电平=0，低电平=1 (常闭逻辑)
    /// false: 高电平=1，低电平=0 (常开逻辑)
    /// </remarks>
    public bool IsInverted { get; init; } = false;

    /// <summary>
    /// 描述说明
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// 关联的拓扑元素ID（可选）
    /// </summary>
    /// <remarks>
    /// 例如: 节点ID "D1", 格口ID "CHUTE_A"
    /// 用于关联IO点与拓扑元素
    /// </remarks>
    public string? RelatedTopologyElementId { get; init; }

    /// <summary>
    /// IO功能角色
    /// </summary>
    /// <remarks>
    /// 例如: "Sensor", "LeftActuator", "RightActuator", "Conveyor"
    /// </remarks>
    public string? FunctionalRole { get; init; }
}
