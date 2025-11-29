namespace ZakYip.WheelDiverterSorter.Drivers.Vendors.ShuDiNiao.Configuration;

/// <summary>
/// 数递鸟摆轮配置选项
/// </summary>
/// <remarks>
/// 用于配置数递鸟摆轮驱动器的TCP连接参数和设备列表。
/// 此配置从 WheelDiverterConfiguration.ShuDiNiao 中提取并在 DI 中注册。
/// </remarks>
public class ShuDiNiaoOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "WheelDiverter:ShuDiNiao";

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
