using System.ComponentModel;

namespace ZakYip.WheelDiverterSorter.Core.Sorting.Runtime;

/// <summary>
/// 线体拥堵等级。
/// </summary>
public enum CongestionLevel
{
    /// <summary>
    /// 正常。
    /// </summary>
    [Description("正常")]
    Normal = 0,

    /// <summary>
    /// 接近极限，延迟和在途数量偏高。
    /// </summary>
    [Description("拥堵预警")]
    Warning = 1,

    /// <summary>
    /// 已严重拥堵，大量包裹难以及时分拣。
    /// </summary>
    [Description("严重拥堵")]
    Severe = 2
}
