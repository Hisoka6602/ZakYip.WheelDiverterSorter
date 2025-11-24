using System.ComponentModel;

namespace ZakYip.WheelDiverterSorter.Core.Enums.Hardware;

/// <summary>
/// 表示摆轮转向方向的枚举
/// </summary>
/// <remarks>
/// 在直线拓扑结构中，摆轮只有转向方向，不存在具体的转向角度。
/// 每个摆轮可以将包裹分流到左侧格口、右侧格口，或者让包裹直行通过。
/// </remarks>
public enum DiverterDirection
{
    /// <summary>
    /// 直行通过
    /// </summary>
    [Description("直行")]
    Straight = 0,

    /// <summary>
    /// 转向左侧格口
    /// </summary>
    [Description("左")]
    Left = 1,

    /// <summary>
    /// 转向右侧格口
    /// </summary>
    [Description("右")]
    Right = 2
}
