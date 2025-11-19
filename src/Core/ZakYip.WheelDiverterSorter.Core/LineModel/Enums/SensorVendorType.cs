using System.ComponentModel;

namespace ZakYip.WheelDiverterSorter.Core.LineModel.Enums;

/// <summary>
/// 传感器厂商类型
/// </summary>
public enum SensorVendorType
{
    /// <summary>
    /// 模拟传感器（用于测试）
    /// </summary>
    [Description("模拟传感器（用于测试）")]
    Mock = 0,

    /// <summary>
    /// 雷赛（Leadshine）传感器
    /// </summary>
    [Description("雷赛（Leadshine）传感器")]
    Leadshine = 1,

    /// <summary>
    /// 西门子（Siemens）传感器
    /// </summary>
    [Description("西门子（Siemens）传感器")]
    Siemens = 2,

    /// <summary>
    /// 三菱（Mitsubishi）传感器
    /// </summary>
    [Description("三菱（Mitsubishi）传感器")]
    Mitsubishi = 3,

    /// <summary>
    /// 欧姆龙（Omron）传感器
    /// </summary>
    [Description("欧姆龙（Omron）传感器")]
    Omron = 4
}
