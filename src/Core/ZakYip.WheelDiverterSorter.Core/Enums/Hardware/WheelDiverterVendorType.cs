using System.ComponentModel;

namespace ZakYip.WheelDiverterSorter.Core.Enums.Hardware;

/// <summary>
/// 摆轮厂商类型
/// </summary>
/// <remarks>
/// 摆轮设备通过TCP协议直接控制摆轮转向，与IO驱动器是不同的概念。
/// - 摆轮控制器：控制摆轮转向（左转、右转、回中）
/// - IO驱动器：操作IO端点（输入/输出位）
/// </remarks>
public enum WheelDiverterVendorType
{
    /// <summary>
    /// 模拟摆轮（用于测试）
    /// </summary>
    [Description("模拟摆轮（用于测试）")]
    Mock = 0,

    /// <summary>
    /// 数递鸟（ShuDiNiao）摆轮设备
    /// </summary>
    [Description("数递鸟（ShuDiNiao）摆轮设备")]
    ShuDiNiao = 1,

    /// <summary>
    /// 莫迪（Modi）摆轮设备
    /// </summary>
    [Description("莫迪（Modi）摆轮设备")]
    Modi = 2
}
