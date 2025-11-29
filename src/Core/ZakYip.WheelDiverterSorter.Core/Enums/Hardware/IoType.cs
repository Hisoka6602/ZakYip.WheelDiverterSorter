using System.ComponentModel;

namespace ZakYip.WheelDiverterSorter.Core.Enums.Hardware;

/// <summary>
/// IO 类型枚举
/// </summary>
/// <remarks>
/// 定义 IO 点的类型分类：输入或输出。
/// 此枚举属于 Core 层的 Hardware 枚举目录。
/// </remarks>
public enum IoType
{
    /// <summary>
    /// 输入 IO（传感器、按钮等）
    /// </summary>
    [Description("输入")]
    Input = 0,

    /// <summary>
    /// 输出 IO（指示灯、继电器等）
    /// </summary>
    [Description("输出")]
    Output = 1
}
