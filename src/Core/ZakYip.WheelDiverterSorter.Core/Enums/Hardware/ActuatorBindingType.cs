using System.ComponentModel;

namespace ZakYip.WheelDiverterSorter.Core.Enums.Hardware;

/// <summary>
/// 执行器绑定类型
/// </summary>
public enum ActuatorBindingType
{
    /// <summary>
    /// 摆轮左转执行器
    /// </summary>
    [Description("摆轮左转")]
    DiverterLeft,

    /// <summary>
    /// 摆轮右转执行器
    /// </summary>
    [Description("摆轮右转")]
    DiverterRight,

    /// <summary>
    /// 输送带控制
    /// </summary>
    [Description("输送带")]
    Conveyor,

    /// <summary>
    /// 指示灯
    /// </summary>
    [Description("指示灯")]
    Indicator,

    /// <summary>
    /// 其他执行器
    /// </summary>
    [Description("其他")]
    Other
}
