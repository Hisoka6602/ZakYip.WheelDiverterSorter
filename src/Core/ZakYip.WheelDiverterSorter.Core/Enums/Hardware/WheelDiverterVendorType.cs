using System.ComponentModel;

namespace ZakYip.WheelDiverterSorter.Core.Enums.Hardware;

/// <summary>
/// 摆轮厂商类型
/// </summary>
/// <remarks>
/// 摆轮设备通过TCP协议直接控制摆轮转向，与IO驱动器是不同的概念。
/// - 摆轮控制器：控制摆轮转向（左转、右转、回中）
/// - IO驱动器：操作IO端点（输入/输出位）
/// 
/// 注意：仿真模式通过各厂商配置中的 UseSimulation 属性控制，不再使用单独的 Mock 厂商类型。
/// </remarks>
public enum WheelDiverterVendorType
{
    /// <summary>
    /// 数递鸟（ShuDiNiao）摆轮设备（默认）
    /// </summary>
    [Description("数递鸟（ShuDiNiao）摆轮设备")]
    ShuDiNiao = 0,

    /// <summary>
    /// 莫迪（Modi）摆轮设备
    /// </summary>
    [Description("莫迪（Modi）摆轮设备")]
    Modi = 1
}
