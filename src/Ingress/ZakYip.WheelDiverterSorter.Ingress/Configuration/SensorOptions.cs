using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;

namespace ZakYip.WheelDiverterSorter.Ingress.Configuration;

/// <summary>
/// 传感器配置选项
/// </summary>
/// <remarks>
/// 此类是用于配置绑定的 DTO，不直接引用具体厂商类型。
/// 具体的厂商配置通过 ISensorVendorConfigProvider 抽象获取。
/// </remarks>
public class SensorOptions {

    /// <summary>
    /// 传感器厂商类型
    /// </summary>
    public SensorVendorType VendorType { get; set; } = SensorVendorType.Leadshine;

    /// <summary>
    /// 传感器轮询间隔（毫秒）
    /// </summary>
    /// <remarks>
    /// 默认值为 10ms，确保能及时检测包裹通过。
    /// 建议最大不超过 50ms，以保证检测精度。
    /// </remarks>
    public int PollingIntervalMs { get; set; } = 10;
}