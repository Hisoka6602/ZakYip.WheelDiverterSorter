using ZakYip.WheelDiverterSorter.Core.Enums.Communication;
using ZakYip.WheelDiverterSorter.Core.Enums.Conveyor;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Enums.Routing;
using ZakYip.WheelDiverterSorter.Core.Enums.Sensors;
using ZakYip.WheelDiverterSorter.Core.Enums.System;

namespace ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;

/// <summary>
/// 摆轮节点配置
/// </summary>
/// <remarks>
/// 描述摆轮节点的逻辑结构，不包含IO板通道号等硬件细节
/// </remarks>
public record class WheelNodeConfig
{
    /// <summary>
    /// 节点唯一标识符
    /// </summary>
    public required string NodeId { get; init; }

    /// <summary>
    /// 节点显示名称
    /// </summary>
    public required string NodeName { get; init; }

    /// <summary>
    /// 物理位置索引（从入口开始的顺序）
    /// </summary>
    public required int PositionIndex { get; init; }

    /// <summary>
    /// 左侧出口是否连接格口
    /// </summary>
    public bool HasLeftChute { get; init; }

    /// <summary>
    /// 右侧出口是否连接格口
    /// </summary>
    public bool HasRightChute { get; init; }

    /// <summary>
    /// 左侧关联的格口ID列表
    /// </summary>
    public IReadOnlyList<string> LeftChuteIds { get; init; } = Array.Empty<string>();

    /// <summary>
    /// 右侧关联的格口ID列表
    /// </summary>
    public IReadOnlyList<string> RightChuteIds { get; init; } = Array.Empty<string>();

    /// <summary>
    /// 节点支持的分拣方向
    /// </summary>
    public IReadOnlyList<DiverterSide> SupportedSides { get; init; } = new[] { DiverterSide.Straight, DiverterSide.Left, DiverterSide.Right };

    /// <summary>
    /// 备注信息
    /// </summary>
    public string? Remarks { get; init; }
}
