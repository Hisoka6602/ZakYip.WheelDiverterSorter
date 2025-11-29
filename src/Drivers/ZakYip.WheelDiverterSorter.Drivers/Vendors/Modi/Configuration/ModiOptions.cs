namespace ZakYip.WheelDiverterSorter.Drivers.Vendors.Modi.Configuration;

/// <summary>
/// 莫迪摆轮配置选项
/// </summary>
/// <remarks>
/// 用于配置莫迪摆轮驱动器的TCP连接参数和设备列表。
/// 此配置从 WheelDiverterConfiguration.Modi 中提取并在 DI 中注册。
/// </remarks>
public class ModiOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "WheelDiverter:Modi";

    /// <summary>
    /// 是否启用仿真模式
    /// </summary>
    public bool UseSimulation { get; set; } = false;

    /// <summary>
    /// 默认TCP连接超时（毫秒）
    /// </summary>
    public int ConnectionTimeoutMs { get; set; } = 5000;

    /// <summary>
    /// 默认命令发送超时（毫秒）
    /// </summary>
    public int CommandTimeoutMs { get; set; } = 3000;

    /// <summary>
    /// 重连间隔（毫秒）
    /// </summary>
    public int ReconnectIntervalMs { get; set; } = 2000;
}
