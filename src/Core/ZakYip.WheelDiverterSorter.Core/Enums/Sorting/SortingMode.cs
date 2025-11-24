using System.ComponentModel;

namespace ZakYip.WheelDiverterSorter.Core.Enums.Sorting;

/// <summary>
/// 分拣模式枚举
/// </summary>
/// <remarks>
/// 定义系统支持的三种分拣模式
/// </remarks>
public enum SortingMode
{
    /// <summary>
    /// 正式分拣模式（默认）
    /// </summary>
    /// <remarks>
    /// 由上游 Sorting.RuleEngine 给出格口分配
    /// </remarks>
    [Description("正式分拣模式")]
    Formal = 0,

    /// <summary>
    /// 指定落格分拣模式
    /// </summary>
    /// <remarks>
    /// 可设置固定格口落格（异常除外），每次都只在指定的格口ID落格，不在乎是否已连接了上游
    /// </remarks>
    [Description("指定落格分拣模式")]
    FixedChute = 1,

    /// <summary>
    /// 循环格口落格模式
    /// </summary>
    /// <remarks>
    /// 第一个包裹落格口1，第二个包裹落格口2，第三个包裹落格口三以此类推
    /// </remarks>
    [Description("循环格口落格模式")]
    RoundRobin = 2
}
