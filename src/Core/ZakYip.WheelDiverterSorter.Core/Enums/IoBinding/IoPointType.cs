using System.ComponentModel;

namespace ZakYip.WheelDiverterSorter.Core.Enums.IoBinding;

/// <summary>
/// IO点位类型
/// </summary>
public enum IoPointType
{
    /// <summary>
    /// 数字输入
    /// </summary>
    [Description("数字输入")]
    DigitalInput,

    /// <summary>
    /// 数字输出
    /// </summary>
    [Description("数字输出")]
    DigitalOutput,

    /// <summary>
    /// 模拟输入
    /// </summary>
    [Description("模拟输入")]
    AnalogInput,

    /// <summary>
    /// 模拟输出
    /// </summary>
    [Description("模拟输出")]
    AnalogOutput
}
