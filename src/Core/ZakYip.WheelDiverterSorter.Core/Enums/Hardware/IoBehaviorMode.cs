using System.ComponentModel;

namespace ZakYip.WheelDiverterSorter.Core.Enums.Hardware;

/// <summary>
/// IO 行为模式
/// </summary>
/// <remarks>
/// PR-40: 定义 IO 层的仿真模式
/// </remarks>
public enum IoBehaviorMode
{
    /// <summary>理想模式：无抖动、无丢包、无延迟</summary>
    [Description("理想模式")]
    Ideal = 0,

    /// <summary>混沌模式：带抖动、随机延迟、偶发丢失</summary>
    [Description("混沌模式")]
    Chaos = 1
}
