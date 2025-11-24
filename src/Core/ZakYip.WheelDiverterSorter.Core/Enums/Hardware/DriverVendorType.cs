using System.ComponentModel;

namespace ZakYip.WheelDiverterSorter.Core.Enums.Hardware;

/// <summary>
/// 驱动器厂商类型
/// </summary>
public enum DriverVendorType
{
    /// <summary>
    /// 模拟驱动器（用于测试）
    /// </summary>
    [Description("模拟驱动器（用于测试）")]
    Mock = 0,

    /// <summary>
    /// 雷赛（Leadshine）控制器
    /// </summary>
    [Description("雷赛（Leadshine）控制器")]
    Leadshine = 1,

    /// <summary>
    /// 西门子（Siemens）PLC
    /// </summary>
    [Description("西门子（Siemens）PLC")]
    Siemens = 2,

    /// <summary>
    /// 三菱（Mitsubishi）PLC
    /// </summary>
    [Description("三菱（Mitsubishi）PLC")]
    Mitsubishi = 3,

    /// <summary>
    /// 欧姆龙（Omron）PLC
    /// </summary>
    [Description("欧姆龙（Omron）PLC")]
    Omron = 4,

    /// <summary>
    /// 数递鸟（ShuDiNiao）摆轮设备
    /// </summary>
    [Description("数递鸟（ShuDiNiao）摆轮设备")]
    ShuDiNiao = 5,

    /// <summary>
    /// 莫迪（Modi）摆轮设备
    /// </summary>
    [Description("莫迪（Modi）摆轮设备")]
    Modi = 6
}
