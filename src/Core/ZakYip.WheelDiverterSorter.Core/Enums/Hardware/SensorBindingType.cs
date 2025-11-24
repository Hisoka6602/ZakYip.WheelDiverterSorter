using System.ComponentModel;

namespace ZakYip.WheelDiverterSorter.Core.Enums.Hardware;

/// <summary>
/// 传感器绑定类型
/// </summary>
public enum SensorBindingType
{
    /// <summary>
    /// 入口传感器
    /// </summary>
    [Description("入口")]
    Entry,

    /// <summary>
    /// 出口传感器
    /// </summary>
    [Description("出口")]
    Exit,

    /// <summary>
    /// 节点传感器（摆轮位置传感器）
    /// </summary>
    [Description("节点")]
    Node,

    /// <summary>
    /// 格口传感器
    /// </summary>
    [Description("格口")]
    Chute,

    /// <summary>
    /// 其他传感器
    /// </summary>
    [Description("其他")]
    Other
}
