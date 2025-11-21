namespace ZakYip.WheelDiverterSorter.Core.Enums.IoBinding;

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
