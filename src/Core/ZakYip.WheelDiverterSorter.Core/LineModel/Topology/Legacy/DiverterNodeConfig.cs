using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;

namespace ZakYip.WheelDiverterSorter.Core.LineModel.Topology.Legacy;

/// <summary>
/// 摆轮节点配置 - 描述单个摆轮节点的逻辑属性
/// </summary>
/// <remarks>
/// <para>【遗留类型】此类型为遗留拓扑实现，建议使用 <see cref="DiverterNode"/> 替代。</para>
/// <para>只包含拓扑逻辑信息，不包含IO板通道号等硬件细节。</para>
/// <para>硬件细节由IoBinding层处理。</para>
/// </remarks>
[Obsolete("遗留拓扑实现，建议迁移到 LineModel.Topology.DiverterNode，后续版本中将移除")]
public record class DiverterNodeConfig
{
    /// <summary>
    /// 节点唯一标识符
    /// </summary>
    /// <remarks>
    /// 例如: "D1", "D2", "NODE_01"
    /// </remarks>
    public required string NodeId { get; init; }

    /// <summary>
    /// 节点显示名称
    /// </summary>
    public required string NodeName { get; init; }

    /// <summary>
    /// 物理位置索引（从入口开始的顺序，从0开始）
    /// </summary>
    public required int PositionIndex { get; init; }

    /// <summary>
    /// 节点支持的分拣方向
    /// </summary>
    /// <remarks>
    /// 大多数节点支持：Straight（直行）、Left（左转）、Right（右转）
    /// 某些特殊节点可能只支持部分方向
    /// </remarks>
    public IReadOnlyList<DiverterSide> SupportedDirections { get; init; } = 
        new[] { DiverterSide.Straight, DiverterSide.Left, DiverterSide.Right };

    /// <summary>
    /// 左侧关联的格口ID列表
    /// </summary>
    public IReadOnlyList<string> LeftChuteIds { get; init; } = Array.Empty<string>();

    /// <summary>
    /// 右侧关联的格口ID列表
    /// </summary>
    public IReadOnlyList<string> RightChuteIds { get; init; } = Array.Empty<string>();

    /// <summary>
    /// 节点传感器逻辑名称（可选）
    /// </summary>
    /// <remarks>
    /// 某些节点可能有自己的传感器，例如: "D1_Sensor", "NODE_01_SENSOR"
    /// 实际IO点位由IoBindingProfile定义
    /// </remarks>
    public string? SensorLogicalName { get; init; }

    /// <summary>
    /// 左转执行器逻辑名称
    /// </summary>
    /// <remarks>
    /// 例如: "D1_Left", "D1_L"
    /// </remarks>
    public string? LeftActuatorLogicalName { get; init; }

    /// <summary>
    /// 右转执行器逻辑名称
    /// </summary>
    /// <remarks>
    /// 例如: "D1_Right", "D1_R"
    /// </remarks>
    public string? RightActuatorLogicalName { get; init; }

    /// <summary>
    /// 备注信息
    /// </summary>
    public string? Remarks { get; init; }

    /// <summary>
    /// 检查节点是否支持指定方向
    /// </summary>
    public bool SupportsDirection(DiverterSide direction)
    {
        return SupportedDirections.Contains(direction);
    }

    /// <summary>
    /// 左侧是否连接格口
    /// </summary>
    public bool HasLeftChute => LeftChuteIds.Any();

    /// <summary>
    /// 右侧是否连接格口
    /// </summary>
    public bool HasRightChute => RightChuteIds.Any();
}
