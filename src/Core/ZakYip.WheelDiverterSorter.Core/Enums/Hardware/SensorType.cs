using System.ComponentModel;

namespace ZakYip.WheelDiverterSorter.Core.Enums.Hardware;

/// <summary>
/// 传感器类型
/// </summary>
public enum SensorType
{
    /// <summary>
    /// 光电传感器
    /// </summary>
    [Description("光电传感器")]
    Photoelectric,

    /// <summary>
    /// 激光传感器
    /// </summary>
    [Description("激光传感器")]
    Laser
}
