using System.ComponentModel;

namespace ZakYip.WheelDiverterSorter.Core.Enums.Hardware;

/// <summary>
/// 传感器厂商类型（光电传感器、接近开关等传感器厂商）
/// </summary>
/// <remarks>
/// <para>此枚举定义传感器硬件的厂商类型，用于配置和选择传感器驱动实现。</para>
/// <para>⚠️ 注意：此枚举与 <see cref="DriverVendorType"/> 成员集合相同但语义不同：</para>
/// <list type="bullet">
/// <item><description><b>SensorVendorType</b>：标识传感器厂商（光电传感器、接近开关等）</description></item>
/// <item><description><b>DriverVendorType</b>：标识驱动器厂商（IO板卡、PLC、摆轮驱动等）</description></item>
/// </list>
/// <para>虽然目前支持的厂商列表重叠，但两个枚举分别用于不同的硬件类型配置，保持独立有助于未来扩展。</para>
/// </remarks>
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
