using System.ComponentModel;

namespace ZakYip.WheelDiverterSorter.Core.Enums.Hardware;

/// <summary>
/// 驱动器类型枚举
/// </summary>
/// <remarks>
/// 定义系统中不同类型的驱动器分类。
/// 此枚举属于 Core 层的 Hardware 枚举目录。
/// </remarks>
public enum DriverCategory
{
    /// <summary>
    /// IO驱动器（用于传感器和继电器控制）
    /// </summary>
    [Description("IO驱动器")]
    IoDriver,
    
    /// <summary>
    /// 摆轮驱动器（用于摆轮转向控制）
    /// </summary>
    [Description("摆轮驱动器")]
    WheelDiverter
}
