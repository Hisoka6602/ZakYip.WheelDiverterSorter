namespace ZakYip.WheelDiverterSorter.Core.Hardware.Providers;

/// <summary>
/// 厂商无关的传感器配置提供者接口
/// </summary>
/// <remarks>
/// <para>本接口属于 HAL（硬件抽象层），定义获取传感器配置的厂商无关抽象，允许 Ingress 层只依赖此接口而不依赖具体的厂商配置类型。</para>
/// <para>具体实现位于 Drivers 层，通过依赖注入在运行时提供具体厂商的配置。</para>
/// <para>
/// <b>HAL 角色</b>：为 Ingress 提供 Vendor 无关的传感器映射，隐藏厂商特定的配置细节。
/// </para>
/// </remarks>
public interface ISensorVendorConfigProvider
{
    /// <summary>
    /// 是否使用硬件传感器（false则使用模拟传感器）
    /// </summary>
    bool UseHardwareSensor { get; }

    /// <summary>
    /// 传感器厂商类型名称（如 "Leadshine", "Siemens", "Mock" 等）
    /// </summary>
    string VendorTypeName { get; }

    /// <summary>
    /// 控制器卡号（适用于支持卡号的厂商）
    /// </summary>
    ushort CardNo { get; }

    /// <summary>
    /// 获取传感器配置列表
    /// </summary>
    /// <returns>传感器配置条目列表</returns>
    IReadOnlyList<SensorConfigEntry> GetSensorConfigs();
}

/// <summary>
/// 厂商无关的传感器配置条目
/// </summary>
/// <remarks>
/// 此记录定义了传感器的通用配置属性，不包含厂商特定的字段。
/// </remarks>
public sealed record SensorConfigEntry
{
    /// <summary>
    /// 传感器ID
    /// </summary>
    public required string SensorId { get; init; }

    /// <summary>
    /// 传感器类型名称
    /// </summary>
    public required string SensorTypeName { get; init; }

    /// <summary>
    /// 输入位编号（适用于基于IO板卡的传感器）
    /// </summary>
    public int InputBit { get; init; }

    /// <summary>
    /// 是否启用该传感器
    /// </summary>
    public bool IsEnabled { get; init; } = true;
}
