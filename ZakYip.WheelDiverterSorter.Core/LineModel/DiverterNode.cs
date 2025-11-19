using ZakYip.WheelDiverterSorter.Core.LineModel.Enums;

namespace ZakYip.WheelDiverterSorter.Core.LineModel;

/// <summary>
/// 表示摆轮分拣系统中的单个摆轮节点
/// <para>
/// 直线摆轮分拣示例中，每个节点代表一个摆轮设备，可以执行不同的分拣动作。
/// </para>
/// </summary>
/// <remarks>
/// 此类用于支持 <see cref="ISwitchingPathGenerator"/> 的路径生成逻辑。
/// </remarks>
public record class DiverterNode
{
    /// <summary>
    /// 摆轮节点的唯一标识符
    /// </summary>
    public required string NodeId { get; init; }

    /// <summary>
    /// 摆轮节点的显示名称
    /// </summary>
    public required string NodeName { get; init; }

    /// <summary>
    /// 该节点支持的分拣动作列表（直行/左转/右转）
    /// </summary>
    /// <remarks>
    /// 路径生成器使用此列表判断是否能够通过该节点到达目标格口。
    /// 例如：某些节点可能只支持直行和左转，不支持右转。
    /// </remarks>
    public required IReadOnlyList<DiverterSide> SupportedActions { get; init; }

    /// <summary>
    /// 从该节点可以分拣到的格口列表，按分拣方向分组
    /// </summary>
    /// <remarks>
    /// Key: 分拣方向（直行/左转/右转）
    /// Value: 该方向可到达的格口ID列表
    /// </remarks>
    public required IReadOnlyDictionary<DiverterSide, IReadOnlyList<string>> ChuteMapping { get; init; }
}
