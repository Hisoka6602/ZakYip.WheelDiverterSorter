using System.ComponentModel;

namespace ZakYip.WheelDiverterSorter.Core.LineModel.Enums;

/// <summary>
/// 表示分拣方向的枚举
/// </summary>
public enum DiverterSide
{
    /// <summary>
    /// 直行
    /// </summary>
    [Description("直行")]
    Straight = 0,

    /// <summary>
    /// 左转
    /// </summary>
    [Description("左转")]
    Left = 1,

    /// <summary>
    /// 右转
    /// </summary>
    [Description("右转")]
    Right = 2
}
