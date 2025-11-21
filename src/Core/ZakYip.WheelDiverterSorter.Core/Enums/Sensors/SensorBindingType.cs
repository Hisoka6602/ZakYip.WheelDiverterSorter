namespace ZakYip.WheelDiverterSorter.Core.Enums.Sensors;

/// <summary>
/// 传感器绑定类型
/// </summary>
public enum SensorBindingType
{
    /// <summary>
    /// 入口传感器
    /// </summary>
    Entry,

    /// <summary>
    /// 出口传感器
    /// </summary>
    Exit,

    /// <summary>
    /// 节点传感器（摆轮位置传感器）
    /// </summary>
    Node,

    /// <summary>
    /// 格口传感器
    /// </summary>
    Chute,

    /// <summary>
    /// 其他传感器
    /// </summary>
    Other
}
