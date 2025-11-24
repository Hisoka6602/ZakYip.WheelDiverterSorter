using System.ComponentModel;

namespace ZakYip.WheelDiverterSorter.Core.Enums.Communication;

/// <summary>
/// 熔断器状态
/// </summary>
public enum CircuitState
{
    /// <summary>
    /// 关闭状态（正常工作）
    /// </summary>
    [Description("关闭")]
    Closed,

    /// <summary>
    /// 打开状态（熔断中）
    /// </summary>
    [Description("打开")]
    Open,

    /// <summary>
    /// 半开状态（尝试恢复）
    /// </summary>
    [Description("半开")]
    HalfOpen
}
