namespace ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;

/// <summary>
/// 格口配置
/// </summary>
/// <remarks>
/// 描述格口的逻辑信息，包括是否为异常格口等
/// </remarks>
public record class ChuteConfig
{
    /// <summary>
    /// 格口唯一标识符
    /// </summary>
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
    /// </remarks>
    public bool IsExceptionChute { get; init; }

    /// <summary>
    /// 绑定的节点ID
    /// </summary>
    public required string BoundNodeId { get; init; }

    /// <summary>
    /// 绑定的节点方向（Left/Right）
    /// </summary>
    public required string BoundDirection { get; init; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; init; } = true;

    /// <summary>
    /// 备注信息
    /// </summary>
    public string? Remarks { get; init; }
}
