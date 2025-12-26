using ZakYip.WheelDiverterSorter.Drivers.Vendors.Leadshine.Configuration;

namespace ZakYip.WheelDiverterSorter.Drivers;

/// <summary>
/// 驱动器配置选项
/// Driver Configuration Options
/// </summary>
/// <remarks>
/// <para>
/// 此配置类用于从 appsettings.json 中绑定驱动器配置。
/// 推荐使用 DI 扩展方法直接注册厂商实现：
/// - 雷赛 IO: AddLeadshineIo
/// - 数递鸟摆轮: AddShuDiNiaoWheelDiverter
/// - 莫迪摆轮: AddModiWheelDiverter
/// </para>
/// <para>
/// This configuration class is used to bind driver configuration from appsettings.json.
/// Use DI extension methods to register vendor implementations directly.
/// </para>
/// </remarks>
public class DriverOptions
{
    /// <summary>
    /// 雷赛控制器配置
    /// </summary>
    public LeadshineOptions Leadshine { get; set; } = new();

    /// <summary>
    /// 雷赛传感器配置
    /// </summary>
    public LeadshineSensorOptions? Sensor { get; set; }
}
