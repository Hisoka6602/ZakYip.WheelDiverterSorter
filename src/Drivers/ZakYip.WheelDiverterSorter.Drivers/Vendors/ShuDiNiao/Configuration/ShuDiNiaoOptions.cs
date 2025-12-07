using ZakYip.WheelDiverterSorter.Core.Enums.Hardware.Vendors;

namespace ZakYip.WheelDiverterSorter.Drivers.Vendors.ShuDiNiao.Configuration;

/// <summary>
/// 数递鸟摆轮配置选项
/// </summary>
/// <remarks>
/// 用于配置数递鸟摆轮驱动器的TCP连接参数和设备列表。
/// 此配置从 WheelDiverterConfiguration.ShuDiNiao 中提取并在 DI 中注册。
/// 支持客户端模式（主动连接设备）和服务端模式（监听设备连接）。
/// </remarks>
public class ShuDiNiaoOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "WheelDiverter:ShuDiNiao";

    /// <summary>
    /// 通信模式（Client=客户端模式，Server=服务端模式）
    /// </summary>
    /// <remarks>
    /// - Client（默认）: 系统作为客户端，主动连接到摆轮设备
    /// - Server: 系统作为服务端，监听设备连接
    /// </remarks>
    public ShuDiNiaoMode Mode { get; set; } = ShuDiNiaoMode.Client;

    /// <summary>
    /// 服务端模式下的监听地址（默认 "0.0.0.0" 表示监听所有网卡）
    /// </summary>
    /// <remarks>
    /// 仅在 Mode=Server 时生效
    /// </remarks>
    public string ServerListenAddress { get; set; } = "0.0.0.0";

    /// <summary>
    /// 服务端模式下的监听端口
    /// </summary>
    /// <remarks>
    /// 仅在 Mode=Server 时生效
    /// </remarks>
    public int ServerListenPort { get; set; } = 8888;

    /// <summary>
    /// 默认TCP连接超时（毫秒）
    /// </summary>
    /// <remarks>
    /// 仅在 Mode=Client 时生效
    /// </remarks>
    public int ConnectionTimeoutMs { get; set; } = 5000;

    /// <summary>
    /// 默认命令发送超时（毫秒）
    /// </summary>
    public int CommandTimeoutMs { get; set; } = 3000;

    /// <summary>
    /// 重连间隔（毫秒）
    /// </summary>
    /// <remarks>
    /// 仅在 Mode=Client 时生效
    /// </remarks>
    public int ReconnectIntervalMs { get; set; } = 2000;
}

