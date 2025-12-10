namespace ZakYip.WheelDiverterSorter.Core.Hardware.Providers;

/// <summary>
/// 厂商无关的传感器配置提供者接口
/// </summary>
/// <remarks>
/// <para><b>HAL 层定位</b>：本接口是传感器配置三层架构中的 HAL 抽象层。</para>
/// 
/// <para><b>三层架构说明</b>：</para>
/// <list type="number">
///   <item>
///     <term>厂商 Options 层</term>
///     <description>
///       位于 Drivers/Vendors/{Vendor}/Configuration/，包含厂商特定的配置类型
///       （如 LeadshineSensorOptions、LeadshineSensorConfigDto）。
///       这些类型直接对应厂商硬件的配置结构。
///     </description>
///   </item>
///   <item>
///     <term>HAL 抽象层（本接口）</term>
///     <description>
///       位于 Core/Hardware/Providers/，定义厂商无关的传感器配置访问协议。
///       厂商实现（如 LeadshineSensorVendorConfigProvider）负责将厂商 Options 转换为
///       通用的 <see cref="SensorConfigEntry"/>，实现厂商隔离。
///     </description>
///   </item>
///   <item>
///     <term>消费层（Ingress）</term>
///     <description>
///       位于 Ingress/Sensors/，如 LeadshineSensorFactory。
///       只依赖本接口和 <see cref="SensorConfigEntry"/>，不依赖具体厂商配置类型。
///     </description>
///   </item>
/// </list>
/// 
/// <para><b>架构原则</b>：</para>
/// <list type="bullet">
///   <item>系统默认使用真实硬件传感器，不需要配置开关</item>
///   <item>只有在仿真模式下（ISimulationModeProvider.IsSimulationMode() == true）才使用Mock传感器</item>
///   <item>通过 POST /api/simulation/run-scenario-e 等仿真端点进入仿真模式</item>
/// </list>
/// 
/// <para><b>为什么不是简单的 Options 包装器</b>：</para>
/// <list type="bullet">
///   <item>本接口进行了类型转换：将厂商特定的配置（如 LeadshineSensorConfigDto）
///         转换为通用的 <see cref="SensorConfigEntry"/>。</item>
///   <item>本接口实现厂商解耦：Ingress 层无需 using Drivers.Vendors.* 命名空间。</item>
///   <item>本接口支持运行时切换：DI 容器可以根据配置注入不同厂商的实现。</item>
/// </list>
/// 
/// <para><b>相关类型</b>：</para>
/// <list type="bullet">
///   <item><see cref="SensorConfigEntry"/> - 厂商无关的传感器配置条目</item>
///   <item>LeadshineSensorVendorConfigProvider - 雷赛实现（Drivers 层）</item>
///   <item>LeadshineSensorFactory - Ingress 层消费者</item>
/// </list>
/// </remarks>
public interface ISensorVendorConfigProvider
{
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

    /// <summary>
    /// 传感器轮询间隔（毫秒）
    /// </summary>
    /// <remarks>
    /// 如果为 null，则使用全局默认值 (SensorOptions.PollingIntervalMs = 10ms)。
    /// 建议范围：5ms - 50ms。
    /// </remarks>
    public int? PollingIntervalMs { get; init; }
}
