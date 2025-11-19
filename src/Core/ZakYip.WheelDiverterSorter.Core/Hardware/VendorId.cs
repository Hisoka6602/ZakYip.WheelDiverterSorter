using System.ComponentModel;

namespace ZakYip.WheelDiverterSorter.Core.Hardware;

/// <summary>
/// 厂商标识符枚举
/// Vendor Identifier - Identifies hardware vendor/manufacturer
/// </summary>
public enum VendorId
{
    /// <summary>
    /// 未指定厂商
    /// </summary>
    [Description("未指定厂商")]
    Unspecified = 0,

    /// <summary>
    /// 模拟驱动器（用于测试和仿真）
    /// Simulated Driver - For testing and simulation
    /// </summary>
    [Description("模拟驱动器（用于测试和仿真）")]
    Simulated = 1,

    /// <summary>
    /// 雷赛智能（Leadshine）- 运动控制器厂商
    /// Leadshine Intelligent - Motion controller manufacturer
    /// </summary>
    [Description("雷赛智能（Leadshine）- 运动控制器")]
    Leadshine = 10,

    /// <summary>
    /// 西门子（Siemens）- PLC厂商
    /// Siemens - PLC manufacturer
    /// </summary>
    [Description("西门子（Siemens）- PLC")]
    Siemens = 20,

    /// <summary>
    /// 三菱电机（Mitsubishi）- PLC厂商
    /// Mitsubishi Electric - PLC manufacturer
    /// </summary>
    [Description("三菱电机（Mitsubishi）- PLC")]
    Mitsubishi = 30,

    /// <summary>
    /// 欧姆龙（Omron）- PLC和传感器厂商
    /// Omron - PLC and sensor manufacturer
    /// </summary>
    [Description("欧姆龙（Omron）- PLC和传感器")]
    Omron = 40,

    /// <summary>
    /// 施耐德电气（Schneider Electric）- 自动化厂商
    /// Schneider Electric - Automation manufacturer
    /// </summary>
    [Description("施耐德电气（Schneider Electric）- 自动化")]
    SchneiderElectric = 50,

    /// <summary>
    /// AB/罗克韦尔（Rockwell Automation）- 自动化厂商
    /// Rockwell Automation (Allen-Bradley) - Automation manufacturer
    /// </summary>
    [Description("AB/罗克韦尔（Rockwell Automation）- 自动化")]
    RockwellAutomation = 60,

    /// <summary>
    /// 汇川技术（Inovance）- 国产自动化厂商
    /// Inovance - Domestic automation manufacturer
    /// </summary>
    [Description("汇川技术（Inovance）- 国产自动化")]
    Inovance = 70,

    /// <summary>
    /// 台达电子（Delta）- 自动化厂商
    /// Delta Electronics - Automation manufacturer
    /// </summary>
    [Description("台达电子（Delta）- 自动化")]
    Delta = 80
}
