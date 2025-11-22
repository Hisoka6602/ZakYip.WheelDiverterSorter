using System.ComponentModel;

namespace ZakYip.WheelDiverterSorter.Core.Enums.Hardware;

/// <summary>
/// S7 CPU类型
/// </summary>
public enum S7CpuType
{
    /// <summary>
    /// S7-1200 系列
    /// </summary>
    [Description("S7-1200")]
    S71200,

    /// <summary>
    /// S7-1500 系列
    /// </summary>
    [Description("S7-1500")]
    S71500,

    /// <summary>
    /// S7-300 系列
    /// </summary>
    [Description("S7-300")]
    S7300,

    /// <summary>
    /// S7-400 系列
    /// </summary>
    [Description("S7-400")]
    S7400
}
