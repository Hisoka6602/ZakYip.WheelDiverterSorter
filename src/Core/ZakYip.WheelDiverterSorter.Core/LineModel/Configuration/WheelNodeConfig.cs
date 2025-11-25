using ZakYip.WheelDiverterSorter.Core.Enums;

namespace ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;

/// <summary>
/// 摆轮节点配置
/// </summary>
/// <remarks>
/// 描述摆轮节点的逻辑结构，包括摆轮前感应IO的引用
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
    /// 摆轮前感应IO的Id（引用感应IO配置中的SensorId）
    /// </summary>
    /// <remarks>
    /// 必须引用一个 WheelFront 类型的感应IO。
    /// 用于检测包裹即将到达摆轮，触发摆轮提前动作。
    /// </remarks>
    public long? FrontIoId { get; init; }

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
