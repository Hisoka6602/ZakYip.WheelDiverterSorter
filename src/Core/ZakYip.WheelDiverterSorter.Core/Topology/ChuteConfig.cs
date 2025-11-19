namespace ZakYip.WheelDiverterSorter.Core.Topology;

/// <summary>
/// 格口配置 - 描述分拣格口的逻辑信息
/// </summary>
/// <remarks>
/// 只包含格口的逻辑属性，不涉及硬件IO绑定
/// </remarks>
public record class ChuteConfig
{
    /// <summary>
    /// 格口唯一标识符
    /// </summary>
    /// <remarks>
    /// 例如: "CHUTE_A", "CHUTE_EXCEPTION", "格口01"
    /// </remarks>
    public required string ChuteId { get; init; }

    /// <summary>
    /// 格口显示名称
    /// </summary>
    public required string ChuteName { get; init; }

    /// <summary>
    /// 是否为异常格口
    /// </summary>
    /// <remarks>
    /// 异常格口用于接收无法分拣到目标格口的包裹
    /// 一条线通常只有一个异常格口
    /// </remarks>
    public bool IsExceptionChute { get; init; }

    /// <summary>
    /// 绑定的节点ID
    /// </summary>
    /// <remarks>
    /// 格口必须绑定到某个摆轮节点
    /// </remarks>
    public required string BoundNodeId { get; init; }

    /// <summary>
    /// 绑定的节点方向
    /// </summary>
    /// <remarks>
    /// 可选值: "Left", "Right", "Straight"
    /// </remarks>
    public required string BoundDirection { get; init; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; init; } = true;

    /// <summary>
    /// 格口传感器逻辑名称（可选）
    /// </summary>
    /// <remarks>
    /// 某些格口可能有自己的传感器，例如: "CHUTE_A_SENSOR"
    /// 实际IO点位由IoBindingProfile定义
    /// </remarks>
    public string? SensorLogicalName { get; init; }

    /// <summary>
    /// 备注信息
    /// </summary>
    public string? Remarks { get; init; }
}
