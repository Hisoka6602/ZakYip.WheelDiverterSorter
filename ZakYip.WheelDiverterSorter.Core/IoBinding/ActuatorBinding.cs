using ZakYip.WheelDiverterSorter.Core.LineModel.Enums;

namespace ZakYip.WheelDiverterSorter.Core.IoBinding;

/// <summary>
/// 执行器绑定类型
/// </summary>
public enum ActuatorBindingType
{
    /// <summary>
    /// 摆轮左转执行器
    /// </summary>
    DiverterLeft,

    /// <summary>
    /// 摆轮右转执行器
    /// </summary>
    DiverterRight,

    /// <summary>
    /// 输送带控制
    /// </summary>
    Conveyor,

    /// <summary>
    /// 指示灯
    /// </summary>
    Indicator,

    /// <summary>
    /// 其他执行器
    /// </summary>
    Other
}

/// <summary>
/// 执行器绑定 - 描述执行器的逻辑绑定
/// </summary>
public record class ActuatorBinding
{
    /// <summary>
    /// IO点描述符
    /// </summary>
    public required IoPointDescriptor IoPoint { get; init; }

    /// <summary>
    /// 执行器类型
    /// </summary>
    public required ActuatorBindingType ActuatorType { get; init; }

    /// <summary>
    /// 关联的节点ID（仅当ActuatorType为DiverterLeft或DiverterRight时）
    /// </summary>
    public string? NodeId { get; init; }

    /// <summary>
    /// 控制方向（仅当ActuatorType为DiverterLeft或DiverterRight时）
    /// </summary>
    public DiverterSide? ControlDirection { get; init; }

    /// <summary>
    /// 默认状态（true=激活，false=未激活）
    /// </summary>
    public bool DefaultState { get; init; } = false;

    /// <summary>
    /// 动作延迟（毫秒）
    /// </summary>
    /// <remarks>
    /// 执行器动作后等待的时间
    /// </remarks>
    public int ActionDelayMs { get; init; } = 100;

    /// <summary>
    /// 备注信息
    /// </summary>
    public string? Remarks { get; init; }
}
