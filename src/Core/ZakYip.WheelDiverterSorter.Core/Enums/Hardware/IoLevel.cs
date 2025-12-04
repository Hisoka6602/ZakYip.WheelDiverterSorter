using System.ComponentModel;

namespace ZakYip.WheelDiverterSorter.Core.Enums.Hardware;

/// <summary>
/// IO 电平状态枚举
/// IO Level enumeration
/// </summary>
/// <remarks>
/// 用于表示 IO 点的逻辑电平状态（高电平/低电平）。
/// Used to represent the logical level state of IO points (High/Low).
/// </remarks>
public enum IoLevel
{
    /// <summary>
    /// 低电平状态
    /// Low level state
    /// </summary>
    [Description("低电平")]
    Low = 0,

    /// <summary>
    /// 高电平状态
    /// High level state
    /// </summary>
    [Description("高电平")]
    High = 1
}
