namespace ZakYip.WheelDiverterSorter.Core.Configuration;

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
    Formal = 0,

    /// <summary>
    /// 指定落格分拣模式
    /// </summary>
    /// <remarks>
    /// 可设置固定格口落格（异常除外），每次都只在指定的格口ID落格，不在乎是否已连接了上游
    /// </remarks>
    FixedChute = 1,

    /// <summary>
    /// 循环格口落格模式
    /// </summary>
    /// <remarks>
    /// 第一个包裹落格口1，第二个包裹落格口2，第三个包裹落格口三以此类推
    /// </remarks>
    RoundRobin = 2
}
