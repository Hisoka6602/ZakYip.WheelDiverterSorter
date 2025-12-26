using System.ComponentModel;

namespace ZakYip.WheelDiverterSorter.Core.Enums.Hardware;

/// <summary>
/// 驱动器厂商类型（IO板卡、PLC、摆轮驱动器等硬件驱动厂商）
/// </summary>
/// <remarks>
/// <para>此枚举定义各类硬件驱动器的厂商类型，用于配置和选择硬件驱动实现。</para>
/// <para>⚠️ 注意：此枚举与 <see cref="SensorVendorType"/> 成员集合相同但语义不同：</para>
/// <list type="bullet">
/// <item><description><b>DriverVendorType</b>：标识驱动器厂商（IO板卡、PLC、摆轮驱动等）</description></item>
/// <item><description><b>SensorVendorType</b>：标识传感器厂商（光电传感器、接近开关等）</description></item>
/// </list>
/// <para>虽然目前支持的厂商列表重叠，但两个枚举分别用于不同的硬件类型配置，保持独立有助于未来扩展。</para>
/// </remarks>
public enum DriverVendorType
{
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
    Omron = 4
}
