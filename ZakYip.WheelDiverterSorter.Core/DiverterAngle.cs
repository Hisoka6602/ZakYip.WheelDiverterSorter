using System.ComponentModel;

namespace ZakYip.WheelDiverterSorter.Core;

/// <summary>
/// 表示摆轮角度的枚举
/// </summary>
public enum DiverterAngle
{
    /// <summary>
    /// 0度角
    /// </summary>
    [Description("0度")]
    Angle0 = 0,

    /// <summary>
    /// 30度角
    /// </summary>
    [Description("30度")]
    Angle30 = 30,

    /// <summary>
    /// 45度角
    /// </summary>
    [Description("45度")]
    Angle45 = 45,

    /// <summary>
    /// 90度角
    /// </summary>
    [Description("90度")]
    Angle90 = 90
}
